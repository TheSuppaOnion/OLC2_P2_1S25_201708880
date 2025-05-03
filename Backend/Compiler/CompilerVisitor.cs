using System.Text;
using analyzer;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

public class CompilerVisitor : GolightBaseVisitor<Object>
{

    public ARM64Generator c = new ARM64Generator();
    public ValueWrapper defaultVoid = new NillValue();
    public Environment currentEnvironment; // Environment del ASemantic
    private int _ifCounter = 0;
    private int _logicCounter = 0;
    private int _sliceCounter = 0;   
    private Stack<string> _breakLabels    = new Stack<string>();
    private Stack<string> _continueLabels = new Stack<string>();

    public CompilerVisitor(Environment environment)
    {
        this.currentEnvironment = environment;
    }

    public override Object? VisitProgram(GolightParser.ProgramContext context)
    {
        c.Comment("Prologue main: reservar frame");
        c.EmitFunctionProlog();
        foreach (var instruccion in context.instruccion())
        {
            // Debuggeo!!
            //c.Comment($"Instrucción tipo: {instruccion.GetChild(0).GetType().Name}");
            Visit(instruccion);
        }
        c.Comment("Fin del programa");
        c.EndProgram();  // Añadir instrucciones de finalización
        return null;
    }

    public override Object? VisitInstruccion(GolightParser.InstruccionContext context)
    {
        Console.WriteLine("Visitando Instruccion" + context.GetChild(0).GetType());
        // Determinar qué tipo de instrucción es y visitarla apropiadamente
        if (context.slices() != null)
            return Visit(context.slices());
        else if (context.declaration() != null)
            return Visit(context.declaration());
        else if (context.print() != null)
            return Visit(context.print());
        else if (context.incredecre() != null)
            return Visit(context.incredecre());
        else if (context.funcembebidas() != null)
            return Visit(context.funcembebidas());
        else if (context.@struct() != null) //ELIMINADO EN PROYECTO 2!
            return Visit(context.@struct());
        else if (context.seccontrol() != null)
            return Visit(context.seccontrol());
        else if (context.sentenciastransfer() != null)
            return Visit(context.sentenciastransfer());
        else if (context.funcion() != null)
            return Visit(context.funcion());
        else if (context.bloquessentencias() != null)
            return Visit(context.bloquessentencias());
        /*else if (context.expression() != null)
            return Visit(context.expression());*/
        else
            return null;
    }

public override object? VisitSlices(GolightParser.SlicesContext ctx)
{
    string id   = ctx.ID().GetText();
    var elems   = FlattenListaValores(ctx).Select(e => int.Parse(e.GetText())).ToArray();
    int count   = elems.Length;
    // 1) Registrar el array en .data
    string label = c.RegisterIntSlice($"slice_{id}", elems);
    // 2) Asignar espacio en el stack para la variable
    c.AllocateVariable(id);
    // 3) Cargar la dirección de datos en X0 y guardarla
    c.Adr(Register.X0, label);
    c.StoreVariable(id);
    // 3) Solo actualizamos el wrapper que puso el semántico:
    var wrapper = (ArrayValue)currentEnvironment
                   .GetVariable(id, ctx.ID().Symbol);
    wrapper.Label = label;      // para que EmitPrintSliceInt lo sepa
    wrapper.Count = count;      // si el semántico no lo puso
    wrapper.Name  = id;         // opcional: para usar en dinámicos
    return null;
}

private List<GolightParser.ExpressionContext> FlattenListaValores(IParseTree node)
{
    var list = new List<GolightParser.ExpressionContext>();
    // Si el nodo es una expresión, la agregamos
    if (node is GolightParser.ExpressionContext expr)
    {
        list.Add(expr);
    }
    // Recorremos recursivamente sus hijos
    for (int i = 0; i < node.ChildCount; i++)
    {
        var child = node.GetChild(i);
        // Si es una coma, la ignoramos (la usaremos solamente para separar)
        if (child.GetText() == ",") continue;
        list.AddRange(FlattenListaValores(child));
    }
    return list;
}

public override Object? VisitDeclaration(GolightParser.DeclarationContext context)
{
    c.Comment("Procesando declaración o asignación");
    string id = context.ID().GetText();
    string tipo;
    if (context.TIPO() != null) {
       tipo = context.TIPO().GetText();
    } else {
       // inferencia: por ejemplo, int si no hay punto en la expresión
       tipo = GetExpressionType(context.expression()[0]);
    }

    // Caso 1: ID ASSIGN_SHORT expression
    if (context.ASSIGN_SHORT() != null && context.ID() != null && context.TIPO() == null)
    {
        c.Comment($"Declaración con asignación corta: {id} := <expresión>");
        
        if (context.expression().Length > 0)
        {
            // Evaluar la expresión
            Visit(context.expression()[0]);
            
            // Cargar el valor evaluado (desde la pila)
            c.Pop(Register.X0);
            
            // Reservar espacio y guardar valor
            c.AllocateVariable(id);
            c.StoreVariable(id);
        }
    }
    
    // Caso 2: ID ASSIGN expression (puntos = 0)
    else if (context.ASSIGN() != null && context.ID() != null && context.TIPO() == null)
    {
        c.Comment($"Asignación: {id} = <expresión>");
        // primero generamos la expresión
        Visit(context.expression()[0]);
        // recuperamos el tipo de la variable
        string type = GetTypeFromEnvironment(id, context.ID().Symbol);
        
        if (type == "float64")
        {
            c.PopFloat();
            c.StoreVariableFloat(id);
        }
        else
        {
            c.Pop(Register.X0);
            c.StoreVariable(id);
        }
    }
    
    // Caso 3: += (puntos += 5)
    else if (context.PLUS_ASSIGN() != null && context.ID() != null)
    {
        c.Comment($"Operación += para variable: {id}");
        
        // Cargar valor actual
        c.LoadVariable(Register.X0, id);
        c.Push(Register.X0);
        
        // Evaluar expresión
        Visit(context.expression()[0]);
        c.Pop(Register.X1);  // valor original en X1
        
        // X0 contiene el valor a sumar, X1 contiene el valor original
        c.Add(Register.X0, Register.X1, Register.X0);
        
        // Guardar resultado
        c.StoreVariable(id);
    }
    
    // Caso 4: -= (puntos -= 5)
    else if (context.MINUS_ASSIGN() != null && context.ID() != null)
    {
        c.Comment($"Operación -= para variable: {id}");
        
        // Cargar valor actual
        c.LoadVariable(Register.X0, id);
        c.Push(Register.X0);
        
        // Evaluar expresión
        Visit(context.expression()[0]);
        c.Pop(Register.X1);  // valor original en X1
        c.Mov(Register.X2, Register.X0);  // valor a restar en X2
        
        // X1 contiene el valor original, X2 contiene el valor a restar
        c.Sub(Register.X0, Register.X1, Register.X2);
        
        // Guardar resultado
        c.StoreVariable(id);
    }
    
    // Caso 5: var ID TIPO = expression (var entero int = 42)
    else if (context.GetText().StartsWith("var") && context.ID() != null && context.TIPO() != null)
    {
        c.Comment($"Declaración con tipo: var {id} {tipo}");
        
        // Reservar espacio para la variable
        c.AllocateVariable(id);
        
        // Si hay expresión de asignación
        if (context.expression() != null && context.expression().Length > 0)
        {
            c.Comment($"Asignando valor a {id}");
            Visit(context.expression()[0]);  // Esto dejará el valor en la pila
            
            if (tipo == "float64")
            {
                c.PopFloat();                       // carga d0 desde la pila
                c.StoreVariableFloat(id);           // guarda d0 en [sp,#offset]
            }
            else
            {
                c.Pop(Register.X0);     // Obtener valor de la pila
                c.StoreVariable(id);    // Almacenar valor
            }
        }
        else
        {
            // Inicializar con valor por defecto
            InitializeWithDefaultValue(id, tipo);
        }
    }
    
    return null;
}

// Método auxiliar para inicializar variables con valores por defecto
private void InitializeWithDefaultValue(string varName, string type)
{
    c.Comment($"Inicializando {varName} con valor por defecto para tipo {type}");
    switch (type)
    {
        case "int":
            c.Mov(Register.X0, 0);
            c.Push(Register.X0);
            c.Pop(Register.X0);
            c.StoreVariable(varName);
            break;
            
        case "float64":
            c.LoadFloatValue(0.0);
            c.PushFloat();
            c.PopFloat();
            c.StoreVariableFloat(varName);
            break;
            
        case "string":
            // etiqueta única para cadena vacía
            string emptyLbl = c.RegisterString("");
            c.Adr(Register.X0, emptyLbl);
            c.Push(Register.X0);
            c.Pop(Register.X0);
            c.StoreVariable(varName);
            break;
            
        case "bool":
            c.Mov(Register.X0, 0);
            c.Push(Register.X0);
            c.Pop(Register.X0);
            c.StoreVariable(varName);
            break;
            
        case "rune":
            c.Mov(Register.X0, 0);
            c.Push(Register.X0);
            c.Pop(Register.X0);
            c.StoreVariable(varName);
            break;
            
        default:
            c.Mov(Register.X0, 0);
            c.Push(Register.X0);
            c.Pop(Register.X0);
            c.StoreVariable(varName);
            break;
    }
}

public override object? VisitIncredecre(GolightParser.IncredecreContext ctx)
{
    // nombre de la variable
    string id = ctx.ID().GetText();

    if (ctx.GetText().Contains("++"))   // x++
    {
        // cargar x en X0
        c.LoadVariable(Register.X0, id);
        // X0 = X0 + 1
        c.Add(Register.X0, Register.X0, "1");
        // guardar de nuevo en x
        c.StoreVariable(id);
    }
    else if (ctx.GetText().Contains("--")) // x--
    {
        c.LoadVariable(Register.X0, id);
        c.Sub(Register.X0, Register.X0, "1");
        c.StoreVariable(id);
    }

    return null;
}

    public override Object? VisitFuncembebidas(GolightParser.FuncembebidasContext context)
    {
        return null;
    }

    public override Object? VisitLlamadaFuncion(GolightParser.LlamadaFuncionContext context)
    {
        return null;
    }

private string GetTypeFromEnvironment(string variableName, IToken errorToken)
{
    try {
        ValueWrapper value = currentEnvironment.GetVariable(variableName, errorToken);
        return Utilities.GetTypeName(value);
    }
    catch (Exception) {
        c.Comment($"Error: No se pudo obtener el tipo de {variableName} del environment");
        return "unknown";
    }
}

private string GetExpressionType(GolightParser.ExprContext expr)
{
    string exprText = expr.GetText();
    
    // Si es una expresión aritmética, verificar sus operandos
    if (expr is GolightParser.AddSubContext || 
        expr is GolightParser.MulDivContext)
    {
        // Si alguno de los operandos es float, el resultado es float
        var leftExpr = (expr as GolightParser.AddSubContext)?.expr(0) ?? 
                      (expr as GolightParser.MulDivContext)?.expr(0);
        var rightExpr = (expr as GolightParser.AddSubContext)?.expr(1) ?? 
                       (expr as GolightParser.MulDivContext)?.expr(1);

        if (leftExpr != null && rightExpr != null)
        {
            string leftType = GetExpressionType(leftExpr);
            string rightType = GetExpressionType(rightExpr);
            
            if (leftType == "float64" || rightType == "float64" || 
                exprText.Contains("."))
            {
                return "float64";
            }
        }
    }
    
    // Si es un literal flotante
    if (expr is GolightParser.Float64Context || 
        (exprText.Contains(".") && !exprText.StartsWith("\"")))
    {
        return "float64";
    }
    // Si es un literal entero
    else if (expr is GolightParser.INTContext || 
             (exprText.All(char.IsDigit) && !exprText.Contains(".")))
    {
        return "int";
    }
    // Si es un literal string
    else if (expr is GolightParser.STRINGContext || 
            (exprText.StartsWith("\"") && exprText.EndsWith("\"")))
    {
        return "string";
    }
    // Si es un literal booleano
    else if (expr is GolightParser.BOOLEANOContext || 
            exprText == "true" || exprText == "false")
    {
        return "bool";
    }
    // Si es una variable o expresión más compleja
    else if (expr.GetChild(0) is GolightParser.IdContext)
    {
        return GetTypeFromEnvironment(exprText, expr.Start);
    }
    
    // Por defecto, intentar obtener del environment
    return GetTypeFromEnvironment(exprText, expr.Start);
}

// helper para aplanar la lista de args
private List<GolightParser.ExpressionContext> FlattenConcatenacion(GolightParser.ConcatenacionContext ctx)
{
    var list = new List<GolightParser.ExpressionContext>();
    if (ctx.concatenacion() != null)
        list.AddRange(FlattenConcatenacion(ctx.concatenacion()));
    if (ctx.expression() != null)
        list.Add(ctx.expression());
    return list;
}

// Sobre carga para que GetExpressionType reciba ExpressionContext
private string GetExpressionType(GolightParser.ExpressionContext ctx)
{
    // en tu gramática: expression : expr ;
    return GetExpressionType(ctx.expr());
}

public override Object? VisitPrint(GolightParser.PrintContext context)
{
    c.Comment("Print statement");
    var concatCtx = context.concatenacion();
    if (concatCtx == null) return null;

    // obtenemos los expr en orden
    var args = FlattenConcatenacion(concatCtx);
    for (int i = 0; i < args.Count; i++)
    {
        var expr = args[i];
        string txt = expr.GetText();

        // si literal string
        if (txt.StartsWith("\"") && txt.EndsWith("\""))
        {
            string lit = txt.Substring(1, txt.Length - 2);
            var lbl = c.RegisterString(lit);
            c.Adr(Register.X0, lbl);
            c.Bl("printf");
        }
                else
        {
            // no literal: evaluamos/push
            Visit(expr);
            string type = GetExpressionType(expr);
            // detectar slices (tipo "[]int", "[]string", etc.)
            if (type=="slice")
            {
                var value = currentEnvironment.GetVariable(txt, expr.Start);
                var slice = (ArrayValue)value;
                EmitPrintSliceInt(slice);
                continue;
            }
            else
            {
            c.Comment($"Imprimiendo valor de tipo: {type}");
            switch (type)
            {
                case "float64":
                    var ffmt = c.RegisterString("%.6f");
                    c.Adr(Register.X0, ffmt);
                    c.PopFloat();
                    c.Fmov(Register.X1, Register.D0);
                    c.Bl("printf");
                    break;

                case "int":
                    var ifmt = c.RegisterString("%d");
                    c.Adr(Register.X0, ifmt);
                    c.Pop(Register.X1);
                    c.Bl("printf");
                    break;

                case "bool":
                    var bfmt   = c.RegisterString("%s");
                    var trueLbl  = c.RegisterString("true");
                    var falseLbl = c.RegisterString("false");
                    c.Pop(Register.X1);
                    c.Cmp(Register.X1, 0);
                    c.Adr(Register.X0, bfmt);
                    c.Adr(Register.X1, falseLbl);
                    c.Adr(Register.X2, trueLbl);
                    c.Csel(Register.X1, Register.X2, Register.X1, "ne");
                    c.Bl("printf");
                    break;

                case "string":
                  {
                    var fmt = c.RegisterString("%s");
                    c.Adr(Register.X0, fmt);
                    c.Pop(Register.X1);       // dirección de cadena en X1
                    c.Bl("printf");
                  }
                  break;

                case "rune":
                  {
                    var fmt = c.RegisterString("%c");
                    c.Adr(Register.X0, fmt);
                    c.Pop(Register.X1);       // código Unicode en X1
                    c.Bl("printf");
                  }
                  break;

                default:
                    var dfmt = c.RegisterString("%d");
                    c.Adr(Register.X0, dfmt);
                    c.Pop(Register.X1);
                    c.Bl("printf");
                    break;
            }
            }
        }

        // espacio entre args
        if (i < args.Count - 1)
        {
            var sp = c.RegisterString(" ");
            c.Adr(Register.X0, sp);
            c.Bl("printf");
        }
    }

    // siempre imprimimos un newline al final
    var nl = c.RegisterString("\n");
    c.Adr(Register.X0, nl);
    c.Bl("printf");

    return null;
}

private void EmitPrintSliceInt(ArrayValue slice)
{
    int id        = _sliceCounter++;
    string loopLbl  = $"SLICE_LOOP_{id}";
    string noSepLbl = $"SLICE_NOSEP_{id}";
    string endLbl   = $"SLICE_END_{id}";
    string sepFmt   = c.RegisterString(", ");
    string openFmt  = c.RegisterString("[");
    string closeFmt = c.RegisterString("]");

    // preserve X1–X5
    c.Push(Register.X1); c.Push(Register.X2);
    c.Push(Register.X3); c.Push(Register.X4);
    c.Push(Register.X5);

    // X1 = ptr a datos
    if (slice.Label != null)
    {
        // slice estático
        c.Adr(Register.X1, slice.Label);
    }
    else
    {
        // slice dinámico: la variable en stack apunta al inicio
        c.LoadVariable(Register.X1, slice.Name);
    }

    // X2 = count
    c.Mov(Register.X2, slice.Count);

    // print “[”
    c.Adr(Register.X0, openFmt); c.Bl("printf");

    // bucle i = 0 .. count-1
    c.Mov(Register.X3, 0);
    c.EmitLabel(loopLbl);
    c.Cmp(Register.X3, Register.X2);
    c.B("ge", endLbl);

    // si i>0 => print “, ”
    c.Cmp(Register.X3, 0);
    c.B("eq", noSepLbl);
    c.Adr(Register.X0, sepFmt); c.Bl("printf");
    c.EmitLabel(noSepLbl);

    // cargar w5 = *x1 (elemento 32‑bits)
    c.Ldr("w5", Register.X1, 0);

    // printf("%d", w5)
    var intFmt = c.RegisterString("%d");
    c.Adr(Register.X0, intFmt);
    c.Mov("w1", "w5");
    c.Bl("printf");

    // avanzamos puntero e índice
    c.Add(Register.X1, Register.X1, 4);
    c.Add(Register.X3, Register.X3, 1);
    c.B(loopLbl);

    // fin bucle → print “]”
    c.EmitLabel(endLbl);
    c.Adr(Register.X0, closeFmt); c.Bl("printf");

    // restore X5–X1
    c.Pop(Register.X5); c.Pop(Register.X4);
    c.Pop(Register.X3); c.Pop(Register.X2);
    c.Pop(Register.X1);
}

    public override Object? VisitConcatenacion(GolightParser.ConcatenacionContext context)
{
    c.Comment("Procesando concatenación");
    
    // Procesar la primera expresión
    if (context.expression() != null)
    {
        Visit(context.expression());
    }
    
    // Si hay más concatenaciones, procesarlas
    if (context.concatenacion() != null)
    {
        Visit(context.concatenacion());
    }
    
    return null;
}

    // FUNCION ELMINADA EN PROYECTO 2
    public override Object? VisitStruct(GolightParser.StructContext context)
    {
        Console.WriteLine("Error: No hay structs gracias a Dios");
        throw new SemanticError("No hay soporte para structs en Proyercto 2, Proyecto 1 si lo soporta", context.Start);
    }

    public override Object? VisitLlamadaMetodo(GolightParser.LlamadaMetodoContext context)
    {
       return null;
    }

public override Object? VisitSeccontrol(GolightParser.SeccontrolContext context)
{
    if (context.@if()?.Length > 0)
    {
        Visit(context.@if()[0]);
    }
    else if (context.@for() != null && context.@for().Length > 0)
    {
        foreach (var forCtx in context.@for())
            Visit(forCtx);
    }
    else if (context.@switch() != null && context.@switch().Length > 0)
    {
        foreach (var swCtx in context.@switch())
            Visit(swCtx);
    }
    return null;
}

public override Object? VisitIf(GolightParser.IfContext context)
{
    // 1) Creamos un sub‐entorno nuevo
    var prevEnv = currentEnvironment;
    currentEnvironment = new Environment(prevEnv, prevEnv.Scope);

    c.Comment("If statement");
    int id = _ifCounter++;
    string elseLbl = $"Lelse{id}";
    string endLbl  = $"Lend{id}";

    // 1) Evaluar condición (deja 0/1 en pila → X0)
    Visit(context.expression(0));
    c.Pop(Register.X0);
    c.Cmp(Register.X0, 0);
    // Si es falso, saltar al else/else‑if (ó al end si no hay else)
    if (context.@else() != null)
        c.Beq(elseLbl);
    else
        c.Beq(endLbl);

    // Bloque THEN
    foreach (var instr in context.instruccion())
        Visit(instr);
    c.B(endLbl);

    // Else‑if / else
    if (context.@else() != null)
    {
        c.EmitLabel(elseLbl);
        var e = context.@else();
        if (e is GolightParser.ElseBlockContext)              // else { … }
        {
            VisitElseBlock((GolightParser.ElseBlockContext)e);
        }
        else if (e is GolightParser.ElseIfContext)             // else if (…) { … }
        {
            // Reusa VisitIf para la rama “else if”
            var nestedIf = ((GolightParser.ElseIfContext)e).@if();
            VisitIf(nestedIf);
        }
    }
    c.EmitLabel(endLbl);
    // Restauramos el entorno anterior
    currentEnvironment = prevEnv;
    return null;
}

public override Object? VisitElseBlock(GolightParser.ElseBlockContext context)
{
    c.Comment("Else block");
    foreach (var instr in context.instruccion())
        Visit(instr);
    return null;
}

 public override object? VisitFor(GolightParser.ForContext ctx)
{
    var prevEnv = currentEnvironment;
    currentEnvironment = new Environment(prevEnv, prevEnv.Scope);

    int id       = _ifCounter++;
    string start = $"Lfor_{id}";
    string cond  = $"Lfor_cond{id}";
    string end   = $"Lfor_end{id}";

    _breakLabels.Push(end);
    _continueLabels.Push(start);

    // while‐style: for <expr> { … }
    if (ctx.declaration() == null 
        && ctx.ASSIGN_SHORT() == null 
        && ctx.expression().Length == 1)
    {
        _continueLabels.Push(cond);

        c.EmitLabel(cond);
        Visit(ctx.expression(0));
        c.Pop(Register.X0);
        c.Cmp(Register.X0, 0);
        c.Beq(end);

        foreach (var instr in ctx.instruccion())
            Visit(instr);

        c.B(cond);
        c.EmitLabel(end);
    }
    // C‑style: for init; cond; post { … }
    else if (ctx.declaration() != null && ctx.expression().Length == 1)
    {
        // Extraer el identificador (sin volver a llamar a la declaración más abajo)
        string idvar = ctx.declaration().ID().GetText();

        // 1. Init
        Visit(ctx.declaration()); 

        // 2. Loop start 
        c.EmitLabel(start);

        // 3. Condition
        Visit(ctx.expression(0));
        c.Pop(Register.X0);
        c.Cmp(Register.X0, 0);
        c.Beq(end);

        // 4. Body
        foreach (var instr in ctx.instruccion())
        {
            Visit(instr);
        }

        // 5. Post-increment (i++)
        if (ctx.GetText().Contains("++"))
        {
            c.LoadVariable(Register.X0, idvar);
            c.Add(Register.X0, Register.X0, "1");
            c.StoreVariable(idvar);
        }

        // 6. Jump back to start
        c.B(start);

        // 7. End label
        c.EmitLabel(end);
    }
    // range‐style: for i,j := range expr { … }
    else
    {
        _continueLabels.Push(cond);

        string id1 = ctx.expression(0).GetText();
        string id2 = ctx.expression(1).GetText();

        Visit(ctx.expression(2));
        c.Pop(Register.X1);    // largo

        c.Mov(Register.X0, 0);
        c.StoreVariable(id1);
        c.Mov(Register.X0, Register.X1);
        c.StoreVariable(id2);

        c.EmitLabel(cond);
        c.LoadVariable(Register.X0, id1);
        c.LoadVariable(Register.X1, id2);
        c.Cmp(Register.X0, Register.X1);
        c.Bge(end);

        foreach (var instr in ctx.instruccion())
            Visit(instr);

        c.LoadVariable(Register.X0, id1);
        c.Add(Register.X0, Register.X0, "1");
        c.StoreVariable(id1);
        c.B(cond);

        c.EmitLabel(end);
    }

    _breakLabels.Pop();
    _continueLabels.Pop();
    currentEnvironment = prevEnv;
    return null;
}

public override object? VisitSwitch(GolightParser.SwitchContext ctx)
{
    int id      = _ifCounter++;
    string endL = $"Lswitch_end{id}";

    // 1) evaluamos el scrutinee en X0
    // ctx.expression() es ExpressionContext[], tomamos el primero
    Visit(ctx.expression());
    c.Pop(Register.X0);

    // 2) recogemos todos los CaseContext usando el helper semántico
    var caseList = new List<GolightParser.CaseContext>();
    foreach (var lc in ctx.lista_cases())
        caseList.AddRange(Utilities.GetAllCases(lc));

    // 3) generamos etiquetas
    bool hasDefault = caseList.Any(cs => cs.GetText().StartsWith("default"));
    var labels = caseList
                   .Select((_, i) => $"Lswitch{id}_case{i}")
                   .ToArray();

    // 4) primer pase: comparar cada case
    for (int i = 0; i < caseList.Count; i++)
    {
        var cs = caseList[i];
        if (cs.GetText().StartsWith("case"))
        {
            Visit(cs.expression());
            c.Pop(Register.X1);
            c.Cmp(Register.X0, Register.X1);
            c.Beq(labels[i]);
        }
    }

    // 5) salto a default o fin
    if (hasDefault)
    {
        int defIdx = caseList.FindIndex(cs => cs.GetText().StartsWith("default"));
        c.B(labels[defIdx]);
    }
    else
    {
        c.B(endL);
    }

    // 6) emitimos cuerpos y saltamos al end
    for (int i = 0; i < caseList.Count; i++)
    {
        c.EmitLabel(labels[i]);
        foreach (var instr in caseList[i].instruccion())
            Visit(instr);
        c.B(endL);
    }

    c.EmitLabel(endL);
    return null;
}

    public override object? VisitSentenciastransfer(GolightParser.SentenciastransferContext ctx)
{
    var txt = ctx.GetText();
    if (txt == "break")
        c.B(_breakLabels.Peek());
    else if (txt == "continue")
        c.B(_continueLabels.Peek());
    else if (txt.StartsWith("return"))
    {
        // aquí tu lógica de return: extraer expr, push, y salto al epílogo
    }
    return null;
}

    public override Object? VisitFuncion(GolightParser.FuncionContext context)
    {
    string functionName = context.ID().GetText();
    c.Comment($"Función: {functionName}");
    
    // Guardar el entorno actual para restaurarlo después
    Environment previousEnvironment = currentEnvironment;
    
    // Buscar el entorno de esta función
    Environment functionEnvironment = FindFunctionEnvironment(currentEnvironment, functionName);
    if (functionEnvironment != null) {
        // Si lo encontramos, usar este entorno
        currentEnvironment = functionEnvironment;
    }

    if (functionName == "main")
    {
        // Visitar las instrucciones dentro del cuerpo de la función main
        foreach (var instruccion in context.instruccion())
        {
            Visit(instruccion);
        }
    }
    else
    {
            // Para otras funciones, generar una etiqueta y código de prólogo/epílogo
            c.EmitLabel(functionName);
            c.Comment($"Prólogo de función {functionName}");
            c.EmitFunctionProlog();

            // Visitar el cuerpo de la función
            foreach (var instruccion in context.instruccion())
            {
                Visit(instruccion);
            }

            c.Comment($"Epílogo de función {functionName}");
            c.EmitFunctionEpilog();
            c.Ret();
        }

        return null;
    }

// Método auxiliar para encontrar el entorno de una función
private Environment FindFunctionEnvironment(Environment env, string functionName)
{
    // Buscar en los entornos hijos
    foreach (var child in env.Children)
    {
        if (child.Scope == functionName)
            return child;
            
        // Búsqueda recursiva en hijos
        var found = FindFunctionEnvironment(child, functionName);
        if (found != null)
            return found;
    }
    
    return null;
}

    public override Object? VisitBloquessentencias(GolightParser.BloquessentenciasContext ctx)
    {
        var prevEnv = currentEnvironment;
        currentEnvironment = new Environment(prevEnv, prevEnv.Scope);

        foreach (var instr in ctx.instruccion())
            Visit(instr);

        currentEnvironment = prevEnv;
        return null;
    }

    public override Object? VisitExpression(GolightParser.ExpressionContext context)
    {
        return Visit(context.expr());
    }
public override Object? VisitLogical_AND(GolightParser.Logical_ANDContext ctx)
{
    Visit(ctx.expr(0));
    Visit(ctx.expr(1));
    c.Pop(Register.X1);           // rhs
    c.Pop(Register.X0);           // lhs
    c.And(Register.X0, Register.X0, Register.X1);
    c.Cmp(Register.X0, 0);
    c.Cset(Register.X0, "ne");    // X0 = (X0 != 0) ? 1 : 0
    c.Push(Register.X0);
    return null;
}

public override Object? VisitLogical_OR(GolightParser.Logical_ORContext ctx)
{
    int id       = _logicCounter++;
    string trueL = $"Lor_true{id}";
    string endL  = $"Lor_end{id}";

    // 1) evalúa lhs
    Visit(ctx.expr(0));
    c.Pop(Register.X0);
    c.Cmp(Register.X0, 0);
    c.Bne(trueL);           // si es true salta a trueL

    // 2) evalúa rhs
    Visit(ctx.expr(1));
    c.Pop(Register.X0);
    c.Cmp(Register.X0, 0);
    c.Cset(Register.X0, "ne");   // X0 = rhs != 0
    c.B(endL);

    // 3) rama true
    c.EmitLabel(trueL);
    c.Mov(Register.X0, 1);

    // 4) fin
    c.EmitLabel(endL);
    c.Push(Register.X0);
    return null;
}
    public override Object? VisitEquality(GolightParser.EqualityContext context)
    {
        Visit(context.expr(0));
        Visit(context.expr(1));
        string type1 = GetExpressionType(context.expr(0));
        string type2 = GetExpressionType(context.expr(1));
        string op = context.GetChild(1).GetText(); // "==" o "!="

        if (type1 == "string" && type2 == "string")
        {
            // pop: rhs en X1, lhs en X0
            c.Pop(Register.X1);
            c.Pop(Register.X0);
            // Preparamos strcmp(lhs, rhs)
            // X0 ← lhs, X1 ← rhs
            c.Bl("strcmp");
            // strcmp devuelve 0 si iguales
            c.Cmp(Register.X0, 0);
            string cond = op == "==" ? "eq" : "ne";
            c.Cset(Register.X0, cond);
            c.Push(Register.X0);
            return null;
        }
        else if (type1 == "float64" || type2 == "float64")
        {
            c.PopFloat();
            c.Fmov(Register.D1, Register.D0);
            c.PopFloat();
            c.Fcmp(Register.D0, Register.D1);
            string cond = op == "==" ? "eq" : "ne";
            c.Cset(Register.X0, cond);
        }
        else
        {
            c.Pop(Register.X1);
            c.Pop(Register.X0);
            c.Cmp(Register.X0, Register.X1);
            string cond = op == "==" ? "eq" : "ne";
            c.Cset(Register.X0, cond);
        }
        c.Push(Register.X0);
        return null;
    }

    public override Object? VisitRelational(GolightParser.RelationalContext context)
    {
         // 1) evaluamos ambos operandos
        Visit(context.expr(0));
        Visit(context.expr(1));
        string type1 = GetExpressionType(context.expr(0));
        string type2 = GetExpressionType(context.expr(1));
        string op = context.GetChild(1).GetText(); // "<", ">", "<=", ">="

        if (type1 == "float64" || type2 == "float64")
        {
            // sacar segundo en D1, primero en D0
            c.PopFloat();
            c.Fmov(Register.D1, Register.D0);
            c.PopFloat();
            // comparar D0 vs D1
            c.Fcmp(Register.D0, Register.D1);
            // set X0 = flag según op
            string cond = op switch {
                ">"  => "gt",
                "<"  => "lt",
                ">=" => "ge",
                "<=" => "le",
                _    => "eq"
            };
            c.Cset(Register.X0, cond);
        }
        else
        {
            // sacar segundo en X1, primero en X0
            c.Pop(Register.X1);
            c.Pop(Register.X0);
            c.Cmp(Register.X0, Register.X1);
            string cond = op switch {
                ">"  => "gt",
                "<"  => "lt",
                ">=" => "ge",
                "<=" => "le",
                _    => "eq"
            };
            c.Cset(Register.X0, cond);
        }
        // empujar el bool
        c.Push(Register.X0);
        return null;
    }

private bool HasFloatOperand(GolightParser.ExprContext expr)
{
    // Check for direct float literals
    if (expr is GolightParser.Float64Context)
        return true;
        
    // Check for float in text representation
    string exprText = expr.GetText();
    if (exprText.Contains("."))
        return true;
        
    // Check if it's an identifier
    if (expr.GetChild(0) is GolightParser.IdContext)
    {
        string id = expr.GetChild(0).GetText();
        try
        {
            string type = GetTypeFromEnvironment(id, expr.Start);
            return type == "float64";
        }
        catch
        {
            return false;
        }
    }
    
    return false;
}

public override Object? VisitAddSub(GolightParser.AddSubContext context)
{
    var operation = context.GetChild(1).GetText();
    
    // Evaluar operandos
    Visit(context.expr(0));
    string type1 = GetExpressionType(context.expr(0));
    
    Visit(context.expr(1));
    string type2 = GetExpressionType(context.expr(1));

    c.Comment($"Operación: {type1} {operation} {type2}");

    if (operation == "+" && type1 == "string" && type2 == "string")
    {
        // Sacamos rhs y lhs de la pila
        c.Pop(Register.X1);   // rhs
        c.Pop(Register.X0);   // lhs

        // Reservar 16 bytes para el puntero resultante
        c.Sub(Register.SP, Register.SP, 16);

        // Movemos lhs/rhs a X2/X3 para los varargs
        c.Mov(Register.X2, Register.X0);
        c.Mov(Register.X3, Register.X1);

        // Primer argumento: &ptr
        c.Mov(Register.X0, Register.SP);

        // Segundo argumento: fmt = "%s%s"
        var fmtLbl = c.RegisterString("%s%s");
        c.Adr(Register.X1, fmtLbl);

        // Llamada: asprintf(&ptr, "%s%s", lhs, rhs)
        c.Bl("asprintf");

        // Recuperamos ptr generado en X0
        c.Ldr(Register.X0, Register.SP);

        // Liberamos el espacio
        c.Add(Register.SP, Register.SP, 16);

        // Devolvemos el char* concatenado
        c.Push(Register.X0);
        return null;
    }
    else if (type1 == "float64" || type2 == "float64") 
    {
        // Segundo operando
        if (type2 == "float64") 
        {
            c.PopFloat();  // D0
            c.Fmov(Register.D1, Register.D0);  // Mover a D1
        }
        else 
        {
            c.Pop(Register.X0);
            c.IntToFloat();  // Convierte a float en D0
            c.Fmov(Register.D1, Register.D0);  // Mover a D1
        }

        // Primer operando
        if (type1 == "float64") 
        {
            c.PopFloat();  // D0
        }
        else 
        {
            c.Pop(Register.X0);
            c.IntToFloat();  // Convierte a float en D0
        }

        // Realizar operación
        if (operation == "+") 
        {
            c.FAdd(Register.D0, Register.D0, Register.D1);
        }
        else 
        {
            c.FSub(Register.D0, Register.D0, Register.D1);
        }
        
        c.PushFloat();  // Guardar resultado
    }
    else  // Operación entre enteros
    {
        c.Pop(Register.X1);  // Segundo operando
        c.Pop(Register.X0);  // Primer operando
        
        if (operation == "+")
        {
            c.Add(Register.X0, Register.X0, Register.X1);
        }
        else
        {
            c.Sub(Register.X0, Register.X0, Register.X1);
        }
        
        c.Push(Register.X0);
    }
    
    return null;
}

public override Object? VisitMulDiv(GolightParser.MulDivContext context)
{
    var operation = context.GetChild(1).GetText();
    
    // Evaluar operandos
    Visit(context.expr(0));
    string type1 = GetExpressionType(context.expr(0));
    
    Visit(context.expr(1));
    string type2 = GetExpressionType(context.expr(1));

    c.Comment($"Operación: {type1} {operation} {type2}");

    if (type1 == "float64" || type2 == "float64") 
    {
        // Segundo operando
        if (type2 == "float64") 
        {
            c.PopFloat();  // D0
            c.Fmov(Register.D1, Register.D0);  // Mover a D1
        }
        else 
        {
            c.Pop(Register.X0);
            c.IntToFloat();  // Convierte a float en D0
            c.Fmov(Register.D1, Register.D0);  // Mover a D1
        }

        // Primer operando
        if (type1 == "float64") 
        {
            c.PopFloat();  // D0
        }
        else 
        {
            c.Pop(Register.X0);
            c.IntToFloat();  // Convierte a float en D0
        }

        // Realizar operación
        if (operation == "*") 
        {
            c.FMul(Register.D0, Register.D0, Register.D1);
        }
        else 
        {
            c.FDiv(Register.D0, Register.D0, Register.D1);
        }
        
        c.PushFloat();  // Guardar resultado
    }
    else  // Operación entre enteros
    {
        c.Pop(Register.X1);
        c.Pop(Register.X0);
        
        if (operation == "*")
        {
            c.Mul(Register.X0, Register.X0, Register.X1);
        }
        else
        {
            c.Div(Register.X0, Register.X0, Register.X1);
        }
        
        c.Push(Register.X0);
    }
    
    return null;
}

public override Object? VisitMod(GolightParser.ModContext context)
{
    Visit(context.expr(0));
    Visit(context.expr(1));
    
    c.Pop(Register.X1);  // Divisor
    c.Pop(Register.X0);  // Dividendo
    
    // Calcular módulo: dividendo - (divisor * (dividendo / divisor))
    c.Div(Register.X2, Register.X0, Register.X1);  // X2 = dividendo / divisor
    c.Mul(Register.X2, Register.X2, Register.X1);  // X2 = X2 * divisor
    c.Sub(Register.X0, Register.X0, Register.X2);  // resultado = dividendo - X2
    
    c.Push(Register.X0);
    return null;
}

public override Object? VisitUnario(GolightParser.UnarioContext ctx)
{
    Visit(ctx.expr());
    c.Pop(Register.X0);
    string op = ctx.GetChild(0).GetText();
    if (op == "-")
    {
        c.Neg(Register.X0, Register.X0);
    }
    // (! lo dejamos para VisitNot)
    c.Push(Register.X0);
    return null;
}

public override Object? VisitNot(GolightParser.NotContext context)
{
    // Genera código de la sub‐expresión, deja 0/1 en la pila
    Visit(context.expr());
    // la sacamos a X0
    c.Pop(Register.X0);
    // !X0  =>  X0 == 0  ? 1 : 0
    c.Cmp(Register.X0, 0);
    c.Cset(Register.X0, "eq");
    // volvemos a empujar
    c.Push(Register.X0);
    return null;
}

public override Object? VisitAgrupacion(GolightParser.AgrupacionContext context)
{
    // delega a la expr interna para que deje su valor en la pila
    return Visit(context.expr());
}
public override Object? VisitAgrupacionCorchetes(GolightParser.AgrupacionCorchetesContext context)
{
    return Visit(context.expr());
}

    public override Object? VisitConcat(GolightParser.ConcatContext context)
    {
        return null;
    }

    public override Object? VisitSTRING(GolightParser.STRINGContext context)
    {
        string text = context.STRING().GetText();
        text = text.Substring(1, text.Length - 2);
        text = Utilities.ProcessEscapeSequences(text);
        string strLabel = c.RegisterString(text);
        c.Adr(Register.X0, strLabel);
        c.Push(Register.X0);  // Guardar dirección del string en la pila

        return null;
    }

    public override Object? VisitINT(GolightParser.INTContext context)
{
     var value = context.INT().GetText();
    c.Comment($"Valor entero: {value}");

    if (int.TryParse(value, out int intValue))
    {
        c.Mov(Register.X0, intValue);
        c.Push(Register.X0);  // Solo guardamos el valor en la pila
    }
    else
    {
        c.Comment($"Error al convertir {value} a entero, usando 0");
        c.Mov(Register.X0, 0);
        c.Push(Register.X0);
    }

    return null;
}

public override Object? VisitFLOAT64(GolightParser.FLOAT64Context context)
{
    var flotanteStr = context.GetText();
    if (double.TryParse(flotanteStr, out double valor))
    {
        c.Comment($"Cargando flotante literal: {valor}");
        // Registrar el valor flotante como una constante
        string floatLabel = c.RegisterFloat(valor);
        c.Adr(Register.X0, floatLabel);
        c.Ldr(Register.D0, Register.X0);  // Cargar el valor en D0
        c.PushFloat();  // Guardar en la pila
    }
    else
    {
        c.Comment($"Error al convertir {flotanteStr} a flotante, usando 0.0");
        c.LoadFloatValue(0.0);
        c.PushFloat();
    }
    return null;
}

    public override Object? VisitBOOLEANO(GolightParser.BOOLEANOContext context)
    {
        bool value = bool.Parse(context.BOOLEANO().GetText().ToLower());
        c.Mov(Register.X0, value ? 1 : 0);
        c.Push(Register.X0);

        return null;
    }

    public override Object? VisitRUNE(GolightParser.RUNEContext context)
    {
        string text = context.RUNE().GetText();
        char value = text[1];
        c.Mov(Register.X0, (int)value);
        c.Push(Register.X0);

        return null;
    }

    public override Object? VisitNil(GolightParser.NilContext context)
    {
        return defaultVoid;
    }

    public override Object? VisitID(GolightParser.IDContext context)
    {
        string id = context.ID().GetText();
    c.Comment($"Cargando variable: {id}");
    
    try {
        // Verificamos que exista en el entorno semántico
        ValueWrapper value = currentEnvironment.GetVariable(id, context.ID().Symbol);
        string type = Utilities.GetTypeName(value);
        
        // Cargar según el tipo
        if (type == "float64")
        {
            c.LoadVariableFloat(id);    // Esto cargará en D0
            c.PushFloat();              // Guardar float en la pila
        }
        else
        {
            c.LoadVariable(Register.X0, id);
            c.Push(Register.X0);        // Guardar entero en la pila
        }
    }
    catch (SemanticError ex) {
        c.Comment($"Error: {ex.Message}");
    }
    
    return null;
    }

    public override Object? VisitInt(GolightParser.IntContext context)
    {
        return null;
    }

    public override Object? VisitString(GolightParser.StringContext context)
    {
        return null;
    }

    public override Object? VisitRune(GolightParser.RuneContext context)
    {
       return null;
    }

    public override Object? VisitBooleano(GolightParser.BooleanoContext context)
    {
        return null;
    }

    public override Object? VisitId(GolightParser.IdContext context)
    {
        return null;
    }
    public override Object? VisitFloat64(GolightParser.Float64Context context)
    {
        return null;
    }

}