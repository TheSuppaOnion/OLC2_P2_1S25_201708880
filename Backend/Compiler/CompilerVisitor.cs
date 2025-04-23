using System.Text;
using analyzer;
using Antlr4.Runtime;

public class CompilerVisitor : GolightBaseVisitor<Object>
{

    public ARM64Generator c = new ARM64Generator();
    public Environment currentEnvironment; // Environment del ASemantic
    public CompilerVisitor(Environment environment)
    {
        this.currentEnvironment = environment;
    }

    public override Object? VisitProgram(GolightParser.ProgramContext context)
    {
        c.Comment("Generando código ARM64-201708880");
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
        else if (context.expression() != null)
            return Visit(context.expression());
        else
            return null;
    }

    //De los peores visitors en el sentido de orden, mucha debugeada y no estoy para ordenarlo ya que funciona como esta
    public override Object? VisitSlices(GolightParser.SlicesContext context)
    {   
        return null;
    }

    public override Object? VisitDeclaration(GolightParser.DeclarationContext context)
    {
        return null;
    }

    public override Object? VisitIncredecre(GolightParser.IncredecreContext context)
    {
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


    public override Object? VisitPrint(GolightParser.PrintContext context)
    {   
    c.Comment("Print statement");
    
    if (context.concatenacion() == null)
    {
        // Solo imprimir un salto de línea
        c.PrintNewLine();
        return null;
    }
    
    // Examinar la concatenación para determinar qué imprimir
    var concatContext = context.concatenacion();
    
    // Si hay una expresión directa en la concatenación
    if (concatContext.expression() != null)
    {
        var expr = concatContext.expression();
        
        // Cadena literal: verificar directamente por el texto
        if (expr.GetText().StartsWith("\"") && expr.GetText().EndsWith("\""))
        {
            string text = expr.GetText();
            text = text.Substring(1, text.Length - 2); // Eliminar comillas
            
            c.Comment($"Imprimiendo cadena: {text}");
            string label = c.RegisterString(text);
            c.Adr(Register.X0, label);
            c.Bl("print_string");
        }
        else
        {
            // Otros tipos...
            // Implementa según la necesidad
        }
    }
    
    c.PrintNewLine();
    return null;
    }

    // Método auxiliar para inferir el tipo de una expresión
private string InferTypeFromExpression(GolightParser.ExpressionContext expr)
{
    // Usar GetChild(0) o patrones específicos para verificar el tipo
    if (expr.GetType().Name == "IdContext") // Usar verificación por nombre de la clase
    {
        // Si es una variable, consultar su tipo en el entorno
        string id = expr.GetText(); // Obtenemos el nombre de la variable del texto
        try 
        {
            // Buscamos una forma de obtener el token para pasar al GetVariable
            // Podemos usar reflection para acceder al campo ID si es necesario
            var idField = expr.GetType().GetProperty("ID");
            if (idField != null)
            {
                var idToken = idField.GetValue(expr) as IToken;
                if (idToken != null)
                {
                    ValueWrapper value = currentEnvironment.GetVariable(id, idToken);
                    if (value is IntValue) return "int";
                    if (value is FloatValue) return "float64";
                    if (value is StringValue) return "string";
                    if (value is BoolValue) return "bool";
                    if (value is RuneValue) return "rune";
                }
            }
        }
        catch (SemanticError)
        {
            // Variable no encontrada
        }
    }
    else if (expr.GetType().Name == "StringContext")
    {
        return "string";
    }
    else if (expr.GetType().Name == "IntContext")
    {
        return "int";
    }
    else if (expr.GetType().Name == "Float64Context")
    {
        return "float64";
    }
    else if (expr.GetType().Name == "BOOLEANOContext")
    {
        return "bool";
    }
    else if (expr.GetType().Name == "RUNEContext")
    {
        return "rune";
    }
    
    // También podemos verificar el texto directamente
    string text = expr.GetText();
    if (text.StartsWith("\"") && text.EndsWith("\""))
        return "string";
    else if (int.TryParse(text, out _))
        return "int";
    else if (float.TryParse(text, out _))
        return "float64";
    else if (text == "true" || text == "false")
        return "bool";
    
    // Tipo predeterminado si no podemos inferir
    return "unknown";
}

    public override Object? VisitConcatenacion(GolightParser.ConcatenacionContext context)
    {
    c.Comment("Procesando concatenación");
    
    if (context.concatenacion() != null && context.expression() != null)
    {
        // Concatenación recursiva: primero procesar la parte izquierda
        Visit(context.concatenacion());
        c.Push(Register.X0);  // Guardar el resultado parcial
        
        // Luego procesar la expresión de la derecha
        Visit(context.expression());
        c.Mov(Register.X1, Register.X0);  // Mover el segundo valor a X1
        c.Pop(Register.X0);  // Recuperar el primer valor en X0
        
        // Concatenar ambos valores (X0 y X1) - el resultado quedará en X0
        c.Bl("concatenate_strings");
    }
    else if (context.expression() != null)
    {
        // Solo hay una expresión, visitarla
        Visit(context.expression());
        
        // Si no es una cadena, convertirla a cadena
        var expr = context.expression();
        string type = InferTypeFromExpression(expr);
        
        if (type != "string")
        {
            // Llamar a una función que convierta el valor al formato de cadena
            c.Bl($"convert_{type}_to_string");
        }
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
        return null;
    }

    public override Object? VisitIf(GolightParser.IfContext context)
    {
        return null;
    }

    public override Object? VisitElseIf(GolightParser.ElseIfContext context)
    {
        return null;
    }

    public override Object? VisitElseBlock(GolightParser.ElseBlockContext context)
    {
        return null;
    }

    public override Object? VisitFor(GolightParser.ForContext context)
    {
        return null;
    }

    public override Object? VisitSwitch(GolightParser.SwitchContext context)
    {
        return null;
    }

    public override Object? VisitSentenciastransfer(GolightParser.SentenciastransferContext context)
    {
        return null;
    }

    public override Object? VisitFuncion(GolightParser.FuncionContext context)
    {
        string functionName = context.ID().GetText();
        c.Comment($"Función: {functionName}");

        if (functionName == "main")
        {
            c.Comment($"Función principal: {functionName}");

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

    public override Object? VisitBloquessentencias(GolightParser.BloquessentenciasContext context)
    {
        return null;
    }

    public override Object? VisitExpression(GolightParser.ExpressionContext context)
    {
        return null;
    }

    public override Object? VisitLogical_AND(GolightParser.Logical_ANDContext context)
    {
        return null;
    }

    public override Object? VisitEquality(GolightParser.EqualityContext context)
    {
        return null;
    }

    public override Object? VisitRelational(GolightParser.RelationalContext context)
    {
        return null;
    }

    public override Object? VisitAddSub(GolightParser.AddSubContext context)
    {
        var operation = context.GetChild(1).GetText();
        Visit(context.expr(0));
        Visit(context.expr(1));
        c.Pop(Register.X1);
        c.Pop(Register.X0);
        if (operation == "+")
        {
            c.Add(Register.X0, Register.X0, Register.X1);
        }
        else if (operation == "-")
        {
            c.Sub(Register.X0, Register.X0, Register.X1);
        }
        return null;
    }

    public override Object? VisitMulDiv(GolightParser.MulDivContext context)
    {
        return null;
    }

    public override Object? VisitMod(GolightParser.ModContext context)
    {
        return null;
    }   

    public override Object? VisitUnario(GolightParser.UnarioContext context)
    {
        return null;
    }   

    public override Object? VisitNot(GolightParser.NotContext context)
    {
        return null;
    }

    public override Object? VisitAgrupacion(GolightParser.AgrupacionContext context)
    {
        return null;
    }

    public override Object? VisitAgrupacionCorchetes(GolightParser.AgrupacionCorchetesContext context)
    {
        return null;
    }

    public override Object? VisitConcat(GolightParser.ConcatContext context)
    {
        return null;
    }

    public override Object? VisitSTRING(GolightParser.STRINGContext context)
    {
        return null;
    }

    public override Object? VisitINT(GolightParser.INTContext context)
    {
        return null;
    }

    public override Object? VisitFLOAT64(GolightParser.FLOAT64Context context)
    {
        return null;
    }

    public override Object? VisitBOOLEANO(GolightParser.BOOLEANOContext context)
    {
        return null;
    }

    public override Object? VisitRUNE(GolightParser.RUNEContext context)
    {
        return null;
    }

    public override Object? VisitNil(GolightParser.NilContext context)
    {
        return null;
    }

    public override Object? VisitID(GolightParser.IDContext context)
    {
        return null;
    }

    public override Object? VisitInt(GolightParser.IntContext context)
    {
        var value = context.INT().GetText();
        c.Comment($"Valor entero: {value}");
        c.Mov(Register.X0, int.Parse(value));
        c.Push(Register.X0);
        return null;
    }

    public override Object? VisitFloat64(GolightParser.Float64Context context)
    {
        return null;
    }

    public override Object? VisitString(GolightParser.StringContext context)
    {
        string text = context.STRING().GetText();
        // Eliminar comillas
        text = text.Substring(1, text.Length - 2);

        c.Comment($"String literal: {text}");
        // Registrar la cadena y obtener su etiqueta
        string label = c.RegisterString(text);
        // Cargar la dirección de la cadena en X0 (argumento para print_string)
        c.Adr(Register.X0, label);

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
        string id = context.ID().GetText();
        c.Comment($"Cargando variable: {id}");

        try {
            ValueWrapper value = currentEnvironment.GetVariable(id, context.ID().Symbol);

            // Generar código según el tipo de la variable
            if (value is IntValue)
                c.LoadVariable(Register.X0, id);
            else if (value is StringValue)
                c.LoadStringVariable(Register.X0, id);
            // etc, para otros tipos
        }
        catch (SemanticError) {
            c.Comment($"Variable {id} no encontrada");
        }

        return null;
    }

}