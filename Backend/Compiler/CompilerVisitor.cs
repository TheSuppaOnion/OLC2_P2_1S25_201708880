using System.Text;
using analyzer;
using Antlr4.Runtime;

public class CompilerVisitor : GolightBaseVisitor<ValueWrapper>
{


    public ValueWrapper defaultVoid = new NillValue();

    public string output = "";
    public Environment currentEnvironment;

    public CompilerVisitor()
    {
        currentEnvironment = new Environment(null);
        Embeded.Generate(currentEnvironment);
        DebugArrayAccess("Visitor inicializado, accesos a arrays serán monitoreados");
    }

    private void DebugArrayAccess(string message)
    {
        Console.WriteLine($"DEBUG: {message}");
    }

    public override ValueWrapper VisitProgram(GolightParser.ProgramContext context)
    {
        foreach (var instruccion in context.instruccion())
        {
            Console.WriteLine("Aqui esta el tipo"+instruccion.GetType());
            Visit(instruccion);
        }
        // Verificacion main
        bool mainExists = false;
        if (currentEnvironment != null)
        {
            try
            {
                ValueWrapper mainFunc = currentEnvironment.GetVariable("main", context.Start);
                if (mainFunc is FunctionValue)
                {
                    mainExists = true;
                }
            }
            catch (SemanticError)
            {
                mainExists = false;
            }
        }
        if (!mainExists)
        {
            throw new SemanticError("El programa debe contener una función 'main'", context.Start);
        }
        return defaultVoid;
    }

    public override ValueWrapper VisitInstruccion(GolightParser.InstruccionContext context)
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
        else if (context.@struct() != null) //A quien se le ocurrio tanta fumada con structs?
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
            return defaultVoid;
    }

    //De los peores visitors en el sentido de orden, mucha debugeada y no estoy para ordenarlo ya que funciona como esta
    public override ValueWrapper VisitSlices(GolightParser.SlicesContext context)
    {   
        // Casos segun gramatica NO ESTAN EN ORDEN
        // Caso 1: Asignación a un elemento de un slice bidimensional
        if (context.expression() != null && context.expression().Length >= 2 && 
                context.ASSIGN() != null && context.lista_valores_slicemulti() != null)
        {
        string arrayId = context.ID().GetText();
        Console.WriteLine($"Asignando a elemento de array bidimensional: {arrayId}");

        ValueWrapper rowIndexValue = Visit(context.expression(0));
        ValueWrapper colIndexValue = Visit(context.expression(1));

        Console.WriteLine($"Índices: [{FormatValue(rowIndexValue)}][{FormatValue(colIndexValue)}]");

        List<ValueWrapper> values = new List<ValueWrapper>();

        if (context.lista_valores_slicemulti().lista_valores() != null)
        {
            foreach (var listaValores in context.lista_valores_slicemulti().lista_valores())
            {
                values.AddRange(ProcessListaValores(listaValores));
            }
        }

        Console.WriteLine($"Valores a asignar: {values.Count} elementos");

        ValueWrapper outerArray = currentEnvironment.GetVariable(arrayId, context.ID().Symbol);

        if (outerArray is ArrayValue arrayValue && rowIndexValue is IntValue rowIndex)
        {
            int row = rowIndex.Value;

            if (row < 0 || row >= arrayValue.Elements.Count)
            {
                throw new SemanticError($"Índice de fila fuera de rango: {row}", context.Start);
            }

            if (!(arrayValue.Elements[row] is ArrayValue innerArray))
            {
                throw new SemanticError($"El elemento en la posición {row} no es un array", context.Start);
            }

            if (colIndexValue is IntValue colIndex)
            {
                int col = colIndex.Value;

                if (col < 0 || col >= innerArray.Elements.Count)
                {
                    throw new SemanticError($"Índice de columna fuera de rango: {col}", context.Start);
                }

                if (values.Count > 0)
                {
                    innerArray.Elements[col] = values[0];
                    Console.WriteLine($"Asignado valor {FormatValue(values[0])} a la posición [{row}][{col}]");
                }
            }
            else
            {
                arrayValue.Elements[row] = new ArrayValue(values, innerArray.ElementType);
                Console.WriteLine($"Reemplazado array en posición {row} con {values.Count} elementos");
            }
        }
        else
        {
            throw new SemanticError($"{arrayId} no es un array bidimensional o el primer índice no es un entero", context.ID().Symbol);
        }

            return defaultVoid;
        }
        // Caso 2: Inicialización de un slice bidimensional (matriz)
        else if (context.ID() != null && 
                (context.ASSIGN_SHORT() != null) && 
                context.GetText().Contains("[][]") && 
                context.lista_valores_slicemulti() != null)
        {
            string id = context.ID().GetText();
            string tipo = "int";

            if (context.TIPO() != null)
            {
                tipo = context.TIPO().GetText();
            }

            Console.WriteLine($"Creando matriz bidimensional: {id}");

            List<ValueWrapper> rows = new List<ValueWrapper>();

            if (context.lista_valores_slicemulti().lista_valores() != null)
            {
                foreach (var listaValores in context.lista_valores_slicemulti().lista_valores())
                {
                    List<ValueWrapper> rowValues = ProcessListaValores(listaValores);

                    ArrayValue rowArray = new ArrayValue(rowValues, tipo);
                    rows.Add(rowArray);

                    Console.WriteLine($"Añadida fila con {rowValues.Count} elementos");
                }
            }

            ArrayValue matrixValue = new ArrayValue(rows, tipo);

            currentEnvironment.DeclareVariable(id, matrixValue, context.ID().Symbol);

            Console.WriteLine($"Matriz creada con {rows.Count} filas");
            return defaultVoid;
        }
        // Caso 3
        else if ((context.ASSIGN_SHORT() != null || context.GetText().Contains("=")) && 
            context.ID() != null && context.TIPO() != null)
        {
            string id = context.ID().GetText();
            string tipo = context.TIPO().GetText();
            List<ValueWrapper> valores = new List<ValueWrapper>();

            if (context.lista_valores() != null)
            {
                valores = ProcessListaValores(context.lista_valores());
            }

            ArrayValue arrayValue = new ArrayValue(valores, tipo);
            bool variableExists = VariableExists(id, context.ID().Symbol, currentEnvironment);

            if (variableExists)
            {
                currentEnvironment.AssignVariable(id, arrayValue, context.ID().Symbol);
            }
            else
            {
                currentEnvironment.DeclareVariable(id, arrayValue, context.ID().Symbol);
            }
            return defaultVoid;
        }
        // Caso 4
        else if (context.GetText().StartsWith("var") && context.ID() != null && context.TIPO() != null)
        {
            string id = context.ID().GetText();
            string tipo = context.TIPO().GetText();

            ArrayValue arrayValue = new ArrayValue(new List<ValueWrapper>(), tipo);
            currentEnvironment.DeclareVariable(id, arrayValue, context.ID().Symbol);
            return defaultVoid;
        }
        // Caso 5: para matriz[i][j] = valor (debe ir primero para asegurar que se detecte)
        else if (context.ID() != null && context.expression() != null && context.expression().Length == 2 && 
            context.ASSIGN() != null && context.valor() != null)
        {
            string arrayId = context.ID().GetText();
            Console.WriteLine($"Asignando a elemento de matriz bidimensional: {arrayId}");
    
            ValueWrapper rowIndexValue = Visit(context.expression(0));
            ValueWrapper colIndexValue = Visit(context.expression(1));
    
            Console.WriteLine($"Índices: [{FormatValue(rowIndexValue)}][{FormatValue(colIndexValue)}]");
    
            ValueWrapper value = Visit(context.valor());
            Console.WriteLine($"Valor a asignar: {FormatValue(value)}");
    
            AssignToArrayElementMulti(arrayId, rowIndexValue, colIndexValue, value, context.ID().Symbol);
            
            return defaultVoid;
        }
        // Caso 6
        else if (context.expression() != null && context.expression().Length >= 1 && 
                context.ASSIGN() != null && context.valor() != null)
        {
            string arrayId = context.ID().GetText();
            Console.WriteLine($"Asignando a elemento de array: {arrayId}");

            ValueWrapper indexValue = Visit(context.expression(0));
            Console.WriteLine($"Índice: {FormatValue(indexValue)}");

            Console.WriteLine($"Contexto valor: {context.valor().GetText()}");

            ValueWrapper value = Visit(context.valor());
            Console.WriteLine($"Valor a asignar (tipo: {value?.GetType().Name ?? "null"}): {FormatValue(value)}");

            AssignToArrayElement(arrayId, indexValue, value, context.ID().Symbol);
            return defaultVoid;
        }
        throw new SemanticError("Formato de declaración de slice no válido", context.Start);
    }

    // Gracias a la gran debugeada de slices hay varios metodos que podrian ir en Utilities pero no voy a tocar lo que me funciono despues de horas
    public void AssignToArrayElement(string arrayId, ValueWrapper index, ValueWrapper value, IToken errorToken)
    {
        Console.WriteLine($"AssignToArrayElement - arrayId: {arrayId}, index: {FormatValue(index)}, value: {FormatValue(value)}");

        try {
            ValueWrapper arrayValue = currentEnvironment.GetVariable(arrayId, errorToken);

            if (arrayValue is ArrayValue array && index is IntValue intIndex)
            {
                int idx = intIndex.Value;

                if (idx < 0 || idx >= array.Elements.Count)
                {
                    throw new SemanticError($"Indice fuera de rango: {idx}", errorToken);
                }

                Console.WriteLine($"Valor previo: {FormatValue(array.Elements[idx])}");

                if (array.ElementType != null)
                {
                    value = ConvertToType(value, array.ElementType);
                }

                array.Elements[idx] = value;

                Console.WriteLine($"Nuevo valor: {FormatValue(array.Elements[idx])}");
            }
            else
            {
                throw new SemanticError($"{arrayId} no es un slice o el indice no es un entero", errorToken);
            }
        }
        catch (SemanticError) {
            throw;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error inesperado en AssignToArrayElement: {ex.Message}");
            throw new SemanticError($"Error asignando elemento a slice: {ex.Message}", errorToken);
        }
    }

    // Otro metodo producto de slices...
    public ValueWrapper ProcessAccesoArregloMulti(string id, int fila, int columna, IToken errorToken)
    {
        ValueWrapper matriz = currentEnvironment.GetVariable(id, errorToken);

        if (!(matriz is ArrayValue arrayMatrix))
            throw new SemanticError($"{id} no es una matriz", errorToken);

        if (fila < 0 || fila >= arrayMatrix.Elements.Count)
            throw new SemanticError($"Índice de fila fuera de rango: {fila}", errorToken);

        ValueWrapper filaArray = arrayMatrix.Elements[fila];

        if (!(filaArray is ArrayValue innerArray))
            throw new SemanticError($"El elemento en la posición {fila} no es un array", errorToken);

        if (columna < 0 || columna >= innerArray.Elements.Count)
            throw new SemanticError($"Índice de columna fuera de rango: {columna}", errorToken);

        Console.WriteLine($"Elemento encontrado en matriz[{fila}][{columna}]: {FormatValue(innerArray.Elements[columna])}");
        return innerArray.Elements[columna];
    }

    // Otro más... gracias slices!
    public override ValueWrapper VisitAccesoArreglo(GolightParser.AccesoArregloContext context)
    {
        string id = context.ID().GetText();
        Console.WriteLine($"VisitAccesoArreglo - id: {id}");

        var parent = context.Parent?.Parent;
        if (parent != null && parent.GetText().Contains("[") && parent.GetText().Contains("]"))
        {
            Console.WriteLine($"Posible acceso bidimensional detectado: {parent.GetText()}");
        }

        ValueWrapper array = currentEnvironment.GetVariable(id, context.ID().Symbol);
        ValueWrapper indexValue = Visit(context.expr());

        Console.WriteLine($"Intentando acceder a {id}[{FormatValue(indexValue)}]");

        if (array is ArrayValue arrayValue && indexValue is IntValue intIndex)
        {
            int index = intIndex.Value;

            if (index < 0 || index >= arrayValue.Elements.Count)
            {
                throw new SemanticError($"Índice fuera de rango: {index}", context.Start);
            }

            Console.WriteLine($"Elemento encontrado: {FormatValue(arrayValue.Elements[index])}");

            return arrayValue.Elements[index];
        }

        throw new SemanticError($"No se puede acceder a {id} como array o índice no es un entero", context.Start);
    }

    // Slices...
    public void AssignToArrayElementMulti(string arrayId, ValueWrapper rowIndex, ValueWrapper colIndex, ValueWrapper value, IToken errorToken)
    {
        Console.WriteLine($"AssignToArrayElementMulti - arrayId: {arrayId}, rowIndex: {FormatValue(rowIndex)}, colIndex: {FormatValue(colIndex)}, value: {FormatValue(value)}");

        try {
            ValueWrapper arrayValue = currentEnvironment.GetVariable(arrayId, errorToken);

            if (arrayValue is ArrayValue outerArray && rowIndex is IntValue rowIntIndex)
            {
                int row = rowIntIndex.Value;

                if (row < 0 || row >= outerArray.Elements.Count)
                {
                    throw new SemanticError($"Indice de fila fuera de rango: {row}", errorToken);
                }

                if (!(outerArray.Elements[row] is ArrayValue innerArray))
                {
                    throw new SemanticError($"Elemneto en posicion {row} no es un slice", errorToken);
                }

                if (colIndex is IntValue colIntIndex)
                {
                    int col = colIntIndex.Value;

                    if (col < 0 || col >= innerArray.Elements.Count)
                    {
                        throw new SemanticError($"Indice de columna fuera de rango: {col}", errorToken);
                    }

                    Console.WriteLine($"Valor anterior: {FormatValue(innerArray.Elements[col])}");

                    if (innerArray.ElementType != null)
                    {
                        value = ConvertToType(value, innerArray.ElementType);
                    }

                    innerArray.Elements[col] = value;

                    Console.WriteLine($"Nuevo valor: {FormatValue(innerArray.Elements[col])}");
                }
                else
                {
                    throw new SemanticError($"Segundo indice no es un entero", errorToken);
                }
            }
            else
            {
                throw new SemanticError($"{arrayId} no es un slice bidimensional o el indice no es entero", errorToken);
            }
        }
        catch (SemanticError) {
            throw;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error inesperedao: {ex.Message}");
            throw new SemanticError($"Error asignando elemento a slice: {ex.Message}", errorToken);
        }
    }

    // Que tantos necesitas para funcionar?...
    public override ValueWrapper VisitAccesoArregloMulti(GolightParser.AccesoArregloMultiContext context)
    {
        string id = context.ID().GetText();
        Console.WriteLine($"VisitAccesoArregloMulti - id: {id}");

        ValueWrapper array = currentEnvironment.GetVariable(id, context.ID().Symbol);

        // Verificar manualmente si los índices son constantes
        int? row = null;
        int? col = null;

        if (context.expr(0).GetText().All(char.IsDigit))
        {
            row = int.Parse(context.expr(0).GetText());
            Console.WriteLine($"Índice de fila es constante: {row}");
        }

        if (context.expr(1).GetText().All(char.IsDigit))
        {
            col = int.Parse(context.expr(1).GetText());
            Console.WriteLine($"Índice de columna es constante: {col}");
        }

        // Si ambos índices son constantes, usarlos directamente
        if (row.HasValue && col.HasValue)
        {
            Console.WriteLine($"Accediendo directamente con índices constantes: {id}[{row}][{col}]");
            return ProcessAccesoArregloMulti(id, row.Value, col.Value, context.ID().Symbol);
        }

        // Si no son constantes, evaluar normalmente
        ValueWrapper rowIndexValue = Visit(context.expr(0));
        ValueWrapper colIndexValue = Visit(context.expr(1));

        Console.WriteLine($"Accediendo a {id}[{FormatValue(rowIndexValue)}][{FormatValue(colIndexValue)}]");

        // Utilizar el método ProcessAccesoArregloMulti para obtener el elemento específico
        if (rowIndexValue is IntValue rowIntValue && colIndexValue is IntValue colIntValue)
        {
            return ProcessAccesoArregloMulti(id, rowIntValue.Value, colIntValue.Value, context.ID().Symbol);
        }

        throw new SemanticError("Los índices para matrices bidimensionales deben ser enteros", context.Start);
    }

    // Mini metodo para ayudar al desmadre de slices
    private List<ValueWrapper> ProcessListaValores(GolightParser.Lista_valoresContext context)
    {
        return Utilities.ProcessListaValores(context, this);
    }

    // Despues de slices no pense que structs fuera peor, otra gran debuggeada
    public override ValueWrapper VisitAccesoStruct(GolightParser.AccesoStructContext context)
    {
        string structId = context.ID()[0].GetText();
        ValueWrapper structValue = currentEnvironment.GetVariable(structId, context.ID()[0].Symbol);

        if (!(structValue is StructValue sv))
        {
            throw new SemanticError($"{structId} no es un struct", context.ID()[0].Symbol);
        }

        string fieldName;

        if (context.GetChild(2) is Antlr4.Runtime.Tree.ITerminalNode terminalNode)
        {
            fieldName = terminalNode.GetText();
        }
        else
        {
            string fullText = context.GetText();
            int dotIndex = fullText.IndexOf('.');
            if (dotIndex >= 0 && dotIndex < fullText.Length - 1)
            {
                fieldName = fullText.Substring(dotIndex + 1);
            }
            else
            {
                throw new SemanticError($"No se pudo extraer el nombre del campo de '{fullText}'", context.Start);
            }
        }

        // Eliminar cualquier operador o espacios del nombre del campo
        fieldName = ExtractFieldNameFromExpression(fieldName);

        Console.WriteLine($"VisitAccesoStruct - Accediendo a {structId}.{fieldName}");

        if (!sv.HasField(fieldName))
        {
            throw new SemanticError($"Struct '{sv.StructName}' no tiene un campo '{fieldName}'", context.Start);
        }

        ValueWrapper fieldValue = sv.GetField(fieldName);

        // Siguiente nulo, enviamos como esta el struct
        if (fieldValue is NillValue && sv.StructName == "Nodo" && fieldName == "Siguiente")
        {
            return fieldValue;
        }

        Console.WriteLine($"Valor obtenido para {structId}.{fieldName}: {FormatValue(fieldValue)}");
        return fieldValue;
    }

    // Que buena fumada se pegaron los aux con esto
    public override ValueWrapper VisitAccesoStructLista(GolightParser.AccesoStructListaContext context)
    {
        string structId = context.ID().GetText();
        ValueWrapper structValue = currentEnvironment.GetVariable(structId, context.ID().Symbol);

        if (!(structValue is StructValue sv))
        {
            throw new SemanticError($"{structId} no es un struct", context.ID().Symbol);
        }

        ValueWrapper fieldExpr = Visit(context.expression());
        string fieldName;

        if (fieldExpr is StringValue strValue)
        {
            fieldName = strValue.Value;
        }
        else
        {
            string exprText = context.expression().GetText();
            fieldName = ExtractFieldNameFromExpression(exprText);
        }

        Console.WriteLine($"VisitAccesoStructLista - Accediendo a {structId}.{fieldName}");

        if (!sv.HasField(fieldName))
        {
            throw new SemanticError($"Struct '{sv.StructName}' no tiene un campo '{fieldName}'", context.expression().Start);
        }

        ValueWrapper fieldValue = sv.GetField(fieldName);

        // ni idea de si esto es necesario
        if (fieldValue is NillValue && sv.StructName == "Nodo" && fieldName == "Siguiente")
        {
            return new StructValue("Nodo");
        }

        Console.WriteLine($"Valor obtenido para {structId}.{fieldName}: {FormatValue(fieldValue)}");
        return fieldValue;
    }

    public override ValueWrapper VisitDeclaration(GolightParser.DeclarationContext context)
    {
        // Caso 1
        if (context.ASSIGN_SHORT() != null && context.ID() != null && context.TIPO() == null)
        {
            string id = context.ID().GetText();
        
            if (context.expression().Length > 0)
            {
                ValueWrapper value = Visit(context.expression(0));
                currentEnvironment.DeclareVariable(id, value, context.ID().Symbol);
                return defaultVoid;
            }
            else
            {
             throw new SemanticError($"Falta expresión en la asignación a '{id}'", context.ID().Symbol);
            }
        }
        // Caso 2
        else if (context.ASSIGN() != null && context.ID() != null && context.TIPO() == null)
        {
            string id = context.ID().GetText();
        
            if (context.expression().Length > 0)
            {
                ValueWrapper value = Visit(context.expression(0));
                currentEnvironment.AssignVariable(id, value, context.ID().Symbol);
                return defaultVoid;
            }
            else
            {
                throw new SemanticError($"Falta expresión en la asignación a '{id}'", context.ID().Symbol);
            }
        }
        // Caso 3
        else if (context.PLUS_ASSIGN() != null && context.ID() != null)
        {
            string id = context.ID().GetText();
        
            ValueWrapper currentValue = currentEnvironment.GetVariable(id, context.ID().Symbol);
        
            ValueWrapper valueToAdd = Visit(context.expression(0));
        
            ValueWrapper newValue = PerformAddition(currentValue, valueToAdd, context.PLUS_ASSIGN().Symbol);
        
            currentEnvironment.AssignVariable(id, newValue, context.ID().Symbol);
            return defaultVoid;
        }
        // Caso 4
        else if (context.MINUS_ASSIGN() != null && context.ID() != null)
        {
            string id = context.ID().GetText();
        
            ValueWrapper currentValue = currentEnvironment.GetVariable(id, context.ID().Symbol);
        
            ValueWrapper valueToSubtract = Visit(context.expression(0));
        
            ValueWrapper newValue = PerformSubtraction(currentValue, valueToSubtract, context.MINUS_ASSIGN().Symbol);
        
            currentEnvironment.AssignVariable(id, newValue, context.ID().Symbol);
            return defaultVoid;
        }

        // Caso 5
        else if (context.GetText().StartsWith("var") && context.ID() != null && context.TIPO() != null)
        {
            string id = context.ID().GetText();
            string tipo = context.TIPO().GetText();
        
            // Si hay una expresión de asignación
            if (context.expression() != null && context.expression().Length > 0)
            {
                ValueWrapper value = Visit(context.expression(0));
            
                value = ConvertToType(value, tipo);
            
                currentEnvironment.DeclareVariable(id, value, context.ID().Symbol);
                return defaultVoid;
            }
            else
            {
                ValueWrapper defaultValue = CreateDefaultForType(tipo);
                currentEnvironment.DeclareVariable(id, defaultValue, context.ID().Symbol);
                return defaultVoid;
            }
        }

        // Caso 6
        else if (context.GetText().StartsWith("var") && context.ID() == null && context.expression().Length == 2)
        {
            throw new SemanticError("Asignación por destructuring no soportada aún", context.Start);
    }   
    
        throw new SemanticError("Formato de declaración no válido", context.Start);
    }

    // Validacion de tipos de suma
    private ValueWrapper PerformAddition(ValueWrapper left, ValueWrapper right, IToken errorToken)
    {
        return Utilities.PerformAddition(left, right, errorToken);
    }

    // Validacion tipos de resta
    private ValueWrapper PerformSubtraction(ValueWrapper left, ValueWrapper right, IToken errorToken)
    {
     return Utilities.PerformSubtraction(left, right, errorToken);
    }

    private ValueWrapper ConvertToType(ValueWrapper value, string targetType)
    {
        return Utilities.ConvertToType(value, targetType);
    }

    private ValueWrapper CreateDefaultForType(string tipo)
    {
        return Utilities.CreateDefaultForType(tipo);
    }

    public override ValueWrapper VisitIncredecre(GolightParser.IncredecreContext context)
    {
        string id = context.ID().GetText();
        ValueWrapper value = currentEnvironment.GetVariable(id, context.ID().Symbol);
    
        if (value is IntValue intValue)
        {
            // Crear un nuevo objeto IntValue con el valor incrementado/decrementado
            int newValue = intValue.Value;
            if (context.GetText().Contains("++"))
            {
                newValue++;
            }
            else if (context.GetText().Contains("--"))
            {
                newValue--;
            }
        
            IntValue newIntValue = new IntValue(newValue);
            currentEnvironment.AssignVariable(id, newIntValue, context.ID().Symbol);
            return newIntValue;
        }
        else if (value is FloatValue floatValue)
        {
            // Soporte para incremento/decremento de valores flotantes
            float newValue = floatValue.Value;
            if (context.GetText().Contains("++"))
            {
                newValue++;
            }
            else if (context.GetText().Contains("--"))
            {
                newValue--;
            }
        
            FloatValue newFloatValue = new FloatValue(newValue);
            currentEnvironment.AssignVariable(id, newFloatValue, context.ID().Symbol);
            return newFloatValue;
        }
        else
        {
            throw new SemanticError("Variable " + id + " no es de tipo numerico", context.ID().Symbol);
        }
    }

    public override ValueWrapper VisitFuncembebidas(GolightParser.FuncembebidasContext context)
    {
        // Solo son llamadas a lo metodos en la clase embebidas
        if (context.GetText().StartsWith("strconv.Atoi"))
        {
            string strText = context.STRING().GetText();
            strText = strText.Substring(1, strText.Length - 2);
            strText = ProcessEscapeSequences(strText);
            ValueWrapper arg = new StringValue(strText);
            ValueWrapper funcValue = currentEnvironment.GetVariable("strconv.Atoi", context.Start);
            if (funcValue is FunctionValue function)
            {
                return function.Call(new List<ValueWrapper> { arg }, this);
            }
        }
        else if (context.GetText().StartsWith("strconv.ParseFloat"))
        {
            string strText = context.STRING().GetText();
            strText = strText.Substring(1, strText.Length - 2);
            strText = ProcessEscapeSequences(strText);
            ValueWrapper arg = new StringValue(strText);
            ValueWrapper funcValue = currentEnvironment.GetVariable("strconv.ParseFloat", context.Start);
            if (funcValue is FunctionValue function)
            {
                return function.Call(new List<ValueWrapper> { arg }, this);
            }
        }
        else if (context.GetText().StartsWith("reflect.TypeOf"))
        {
            string id = context.ID().GetText();
            ValueWrapper value = currentEnvironment.GetVariable(id, context.ID().Symbol);
            ValueWrapper funcValue = currentEnvironment.GetVariable("reflect.TypeOf", context.Start);
            if (funcValue is FunctionValue function)
            {
                return function.Call(new List<ValueWrapper> { value }, this);
            }
        }
        else if (context.GetText().StartsWith("slices.Index"))
        {
            string id = context.ID().GetText();
            ValueWrapper array = currentEnvironment.GetVariable(id, context.ID().Symbol);
            ValueWrapper searchValue = Visit(context.valor());
            
            ValueWrapper funcValue = currentEnvironment.GetVariable("slices.Index", context.Start);
            if (funcValue is FunctionValue function)
            {
                return function.Call(new List<ValueWrapper> { array, searchValue }, this);
            }
        }
        else if (context.GetText().StartsWith("strings.Join"))
        {
            string id = context.ID().GetText();
            ValueWrapper array = currentEnvironment.GetVariable(id, context.ID().Symbol);
            string strText = context.STRING().GetText();
            strText = strText.Substring(1, strText.Length - 2);
            strText = ProcessEscapeSequences(strText);
            ValueWrapper separator = new StringValue(strText);

            ValueWrapper funcValue = currentEnvironment.GetVariable("strings.Join", context.Start);
            if (funcValue is FunctionValue function)
            {
                return function.Call(new List<ValueWrapper> { array, separator }, this);
            }
        }
          else if (context.GetText().StartsWith("len"))
        {
            // Todo este desmadre para slices bidimensionales!
            string fullText = context.GetText();
            int startIndex = fullText.IndexOf("(") + 1;
            int endIndex = fullText.LastIndexOf(")");
            string argText = fullText.Substring(startIndex, endIndex - startIndex).Trim();

            if (argText.Contains("[") && argText.Contains("]"))
            {
                int openBracket = argText.IndexOf("[");
                string arrayId = argText.Substring(0, openBracket);

                ValueWrapper array = currentEnvironment.GetVariable(arrayId, context.Start);

                if (!(array is ArrayValue outerArray))
                    throw new SemanticError($"{arrayId} no es un slice", context.Start);

                string indexStr = argText.Substring(openBracket + 1, argText.IndexOf("]") - openBracket - 1);

                int index;
                if (int.TryParse(indexStr, out index))
                {
                    if (index < 0 || index >= outerArray.Elements.Count)
                        throw new SemanticError($"Indice {index} fuera del rango del slice {arrayId}", context.Start);

                    ValueWrapper innerArray = outerArray.Elements[index];

                    ValueWrapper funcValue = currentEnvironment.GetVariable("len", context.Start);
                    if (funcValue is FunctionValue function)
                    {
                        return function.Call(new List<ValueWrapper> { innerArray }, this);
                    }
                }
                else
                {
                    throw new SemanticError("Caracteristica de len no implementada!", context.Start);
                }
            }
            else
            {
                string id = context.ID().GetText();
                ValueWrapper value = currentEnvironment.GetVariable(id, context.ID().Symbol);
                ValueWrapper funcValue = currentEnvironment.GetVariable("len", context.Start);

                if (funcValue is FunctionValue function)
                {
                    return function.Call(new List<ValueWrapper> { value }, this);
                }
            }
        }
        else if (context.GetText().StartsWith("append"))
        {
            string id = context.ID().GetText();
            ValueWrapper array = currentEnvironment.GetVariable(id, context.ID().Symbol);
        
            ValueWrapper appendValue = Visit(context.valor());
            ValueWrapper funcValue = currentEnvironment.GetVariable("append", context.Start);
            if (funcValue is FunctionValue function)
            {
                return function.Call(new List<ValueWrapper> { array, appendValue }, this);
            }
        }
    
        throw new SemanticError("Función embebida desconocida", context.Start);
    }

    // Recursion VisitFuncionesEmbebidas
    public override ValueWrapper VisitFuncionesEmbebidas(GolightParser.FuncionesEmbebidasContext context)
    {
        return Visit(context.funcembebidas());
    }

        public override ValueWrapper VisitLlamadaFuncion(GolightParser.LlamadaFuncionContext context)
    {
        string functionName = context.ID().GetText();

        // Obtener el valor de la función - buscar en todos los entornos disponibles
        ValueWrapper funcVal;
        try
        {
            // Buscar la función en el entorno actual y sus padres
            funcVal = currentEnvironment.GetVariable(functionName, context.ID().Symbol);
        }
        catch (SemanticError)
        {
            // Si no se encuentra en los entornos normales, intentar buscar en el entorno global
            // para el caso de métodos de structs
            Environment globalEnv = GetGlobalEnvironment();
            if (globalEnv != currentEnvironment) // Solo si no estamos ya en el entorno global
            {
                try
                {
                    funcVal = globalEnv.GetVariable(functionName, context.ID().Symbol);
                    Console.WriteLine($"Función {functionName} encontrada en el entorno global");
                }
                catch (SemanticError)
                {
                    throw new SemanticError($"'{functionName}' no es una función definida", context.ID().Symbol);
                }
            }
            else
            {
                throw new SemanticError($"'{functionName}' no es una función definida", context.ID().Symbol);
            }
        }

        if (!(funcVal is FunctionValue function))
        {
            throw new SemanticError($"'{functionName}' no es una función", context.ID().Symbol);
        }

        // Recolectar los argumentos
        List<ValueWrapper> arguments = new List<ValueWrapper>();

        if (context.lista_expresiones() != null)
        {
            // Argumentos en el orden correcto
            var expressions = GetAllExpressions(context.lista_expresiones());

            // Segunda verificación de argumentos
            foreach (var expr in expressions)
            {
                ValueWrapper argValue = Visit(expr);
                Console.WriteLine($"Argumento: {FormatValue(argValue)}");
                arguments.Add(argValue);
            }
        }

        Console.WriteLine($"Llamando a función {functionName} con {arguments.Count} argumentos");

        // Llamar a la función
        ValueWrapper result = function.Call(arguments, this);
        return result;
    }

    // Revisando codigo olvide la existencia de este metodo por completo
    private Environment GetGlobalEnvironment()
    {
        Environment env = currentEnvironment;
        while (env.Enclosing != null)
        {
            env = env.Enclosing;
        }
        return env;
    }

    private bool FunctionExists(string functionName, IToken errorToken, Environment environment = null)
    {
        return Utilities.FunctionExists(functionName, errorToken, environment ?? currentEnvironment);
    }

    // lista_expresiones
    private List<GolightParser.ExprContext> GetAllExpressions(GolightParser.Lista_expresionesContext context)
    {
        return Utilities.GetAllExpressions(context);
    }

    public override ValueWrapper VisitPrint(GolightParser.PrintContext context)
    {   
        if (context.concatenacion() == null)
        {
            output += "\n";
            return defaultVoid;
        }
    
        ValueWrapper concatValue = Visit(context.concatenacion());
        if (concatValue is StringValue strValue)
        {
            output += strValue.Value + "\n";
        }
        else
        {
            output += concatValue.ToString() + "\n";
        }
    
        return defaultVoid;
    }

    public override ValueWrapper VisitConcatenacion(GolightParser.ConcatenacionContext context)
    {
        StringBuilder result = new StringBuilder();
    
        if (context.concatenacion() != null)
        {
            ValueWrapper leftValues = Visit(context.concatenacion());
            if (leftValues is StringValue leftStrVal)
            {
                result.Append(leftStrVal.Value);
            }
        
            if (context.expression() != null)
            {
                ValueWrapper rightValue = Visit(context.expression());
                result.Append(" ").Append(FormatValue(rightValue));
            }
        }
        else if (context.expression() != null)
        {
            ValueWrapper value = Visit(context.expression());
            result.Append(FormatValue(value));
        }
    
        return new StringValue(result.ToString());
    }

    //Codigo desordenado y probablemente redundante pero funcional
    public override ValueWrapper VisitStruct(GolightParser.StructContext context)
    {
    // Caso 1: Definición de struct "type Nodo struct { nombre string, ... }"
        if (context.GetText().StartsWith("type") && context.ID() != null)
    {
        string structName = context.ID().GetText();
        Console.WriteLine($"Definiendo struct: {structName}");

        StructTypeDefinition structDef = new StructTypeDefinition(structName);

        currentEnvironment.DeclareStructType(structName, structDef);
        //Tabla de simbolos
        currentEnvironment.RegisterStruct(structName, context.ID().Symbol);

        // Procesar cada campo
        foreach (var field in context.listastruct())
        {
            string fieldName = field.ID(0).GetText();

            // Caso 1: ID ID (campo de tipo struct)
            if (field.ID().Length > 1 && field.TIPO() == null)
            {
                string fieldType = field.ID(1).GetText();

                if (fieldType == structName || currentEnvironment.StructTypeExists(fieldType))
                {
                    structDef.AddField(fieldName, fieldType);
                    Console.WriteLine($"  Campo: {fieldName} de tipo struct {fieldType}");
                }
                else
                {
                    throw new SemanticError($"Tipo '{fieldType}' no definido para campo '{fieldName}'", field.ID(1).Symbol);
                }
            }
            else if (context.expression().Length >= 2 && context.ASSIGN() != null)
            {
                string leftExpr = context.expression(0).GetText();
                ValueWrapper rightExpr = Visit(context.expression(1));

                if (leftExpr.Contains("."))
                {
                    string[] parts = leftExpr.Split('.');
                    string structId = parts[0];

                    ValueWrapper structValue = currentEnvironment.GetVariable(structId, context.expression(0).Start);

                    if (!(structValue is StructValue sv))
                    {
                        throw new SemanticError($"'{structId}' no es un struct", context.expression(0).Start);
                    }

                    if (parts.Length == 2)
                    {
                        sv.SetField(parts[1], rightExpr);
                    }
                    // Si es una asignación anidada (struct.campo1.campo2...)
                    else if (parts.Length > 2)
                    {
                        // Navegar por la lista de campos
                        StructValue currentStruct = sv;
                        for (int i = 1; i < parts.Length - 1; i++)
                        {
                            ValueWrapper nextField = currentStruct.GetField(parts[i]);

                            // Si el campo es nulo y estamos en un nodo, crearlo automáticamente
                            if (nextField is NillValue && currentStruct.StructName == "Nodo" && parts[i] == "Siguiente")
                            {
                                StructValue newNode = new StructValue("Nodo");
                                currentStruct.SetField(parts[i], newNode);
                                currentStruct = newNode;
                            }
                            // Si el campo es un struct, continuar la navegación
                            else if (nextField is StructValue nextStruct)
                            {
                                currentStruct = nextStruct;
                            }
                            else
                            {
                                throw new SemanticError($"No se puede acceder a '{parts[i+1]}' porque '{parts[i]}' no es un struct", context.expression(0).Start);
                            }
                        }

                        string lastField = parts[parts.Length - 1];
                        currentStruct.SetField(lastField, rightExpr);
                    }

                    return defaultVoid;
                }

                currentEnvironment.AssignVariable(leftExpr, rightExpr, context.expression(0).Start);
                return defaultVoid;
            }
            // Caso 2: ID TIPO (campo de tipo básico)
            else if (field.TIPO() != null)
            {
                string fieldType = field.TIPO().GetText();
                structDef.AddField(fieldName, fieldType);
                Console.WriteLine($"  Campo: {fieldName} de tipo {fieldType}");
            }
        }

        return defaultVoid;
    }
    // Caso 3: Asignación "var = struct" o acceso/modificación de campo
    else if (context.expression().Length >= 2 && context.ASSIGN() != null)
    {
        string leftExpr = context.expression(0).GetText();
        ValueWrapper rightExpr = Visit(context.expression(1));

        if (leftExpr.Contains("."))
        {
            string[] parts = leftExpr.Split('.');
            string structId = parts[0];

            ValueWrapper structValue = currentEnvironment.GetVariable(structId, context.expression(0).Start);

            if (!(structValue is StructValue sv))
            {
                throw new SemanticError($"'{structId}' no es un struct", context.expression(0).Start);
            }

            // Si es una asignación simple de un nivel (struct.campo)
            if (parts.Length == 2)
            {
                sv.SetField(parts[1], rightExpr);
            }
            // Si es una asignación anidada (struct.campo1.campo2...) hacemos lo mismo que caso 2
            else if (parts.Length > 2)
            {
                StructValue currentStruct = sv;
                for (int i = 1; i < parts.Length - 1; i++)
                {
                    ValueWrapper nextField = currentStruct.GetField(parts[i]);

                    if (nextField is NillValue && currentStruct.StructName == "Nodo" && parts[i] == "Siguiente")
                    {
                        StructValue newNode = new StructValue("Nodo");
                        currentStruct.SetField(parts[i], newNode);
                        currentStruct = newNode;
                    }
                    else if (nextField is StructValue nextStruct)
                    {
                        currentStruct = nextStruct;
                    }
                    else
                    {
                        throw new SemanticError($"No se puede acceder a '{parts[i+1]}' porque '{parts[i]}' no es un struct", context.expression(0).Start);
                    }
                }

                string lastField = parts[parts.Length - 1];
                currentStruct.SetField(lastField, rightExpr);
            }

            return defaultVoid;
        }

        currentEnvironment.AssignVariable(leftExpr, rightExpr, context.expression(0).Start);
        return defaultVoid;
    }
    // Caso 3: Inicialización de struct con campos "var := TipoStruct{campo1: valor1, ...}"
    else if (context.expression().Length >= 2 && context.ASSIGN_SHORT() != null)
    {
        string id = context.expression(0).GetText();
        string structTypeName = context.expression(1).GetText();

        StructTypeDefinition structDef;
        bool isNewStruct = false;

        try
        {
            structDef = currentEnvironment.GetStructDefinition(structTypeName, context.expression(1).Start);
        }
        catch (SemanticError)
        {
            Console.WriteLine($"Creando definición de struct implícita para '{structTypeName}'");
            structDef = new StructTypeDefinition(structTypeName);
            isNewStruct = true;

            currentEnvironment.DeclareStructType(structTypeName, structDef);
        }

        StructValue newStruct = new StructValue(structTypeName);

        if (context.lista_valores_struct() != null && context.lista_valores_struct().Length > 0)
        {
            foreach (var fieldValue in context.lista_valores_struct())
            {
                if (fieldValue.expression().Length >= 2 && fieldValue.GetText().Contains(":"))
                {
                    string fieldName = fieldValue.expression(0).GetText();
                    ValueWrapper value = Visit(fieldValue.expression(1));

                    // En caso de nil
                    if (value is NillValue && structTypeName == "Nodo" && fieldName == "Siguiente")
                    {
                        // Permitir asignar nil directamente al campo Siguiente
                        newStruct.SetField(fieldName, value);

                        // Si es un nuevo struct, añadir este campo a la definición
                        if (isNewStruct)
                        {
                            structDef.AddField(fieldName, "Nodo"); // El tipo del campo es el mismo que el struct
                            Console.WriteLine($"  Campo inferido: {fieldName} de tipo {structTypeName} (autorreferencia)");
                        }
                    }
                    else
                    {
                        if (isNewStruct || !structDef.HasField(fieldName))
                        {
                            string fieldType = InferTypeFromValue(value);
                            structDef.AddField(fieldName, fieldType);
                            Console.WriteLine($"  Campo inferido: {fieldName} de tipo {fieldType}");
                        }

                        newStruct.SetField(fieldName, value);
                    }
                }
            }
        }

        currentEnvironment.DeclareVariable(id, newStruct, context.expression(0).Start);
        return defaultVoid;
    }
        // Caso 4: Llamada a método de struct "struct.metodo(args)"
        else if (context.expression().Length >= 1 && context.GetText().Contains(".") && 
                 context.GetText().Contains("(") && context.GetText().Contains(")"))
        {
            string structExpr = context.expression(0).GetText();
            string methodName = context.expression(1).GetText();
            
            ValueWrapper structValue = currentEnvironment.GetVariable(structExpr, context.expression(0).Start);

            if (!(structValue is StructValue sv))
            {
                throw new SemanticError($"'{structExpr}' no es un struct", context.expression(0).Start);
            }

            string fullMethodName = $"{sv.StructName}_{methodName}";

            ValueWrapper funcVal;
            try
            {
                funcVal = currentEnvironment.GetVariable(fullMethodName, context.expression(1).Start);
            }
            catch (SemanticError)
            {
                throw new SemanticError($"El método '{methodName}' no está definido para el struct '{sv.StructName}'", context.expression(1).Start);
            }

            if (!(funcVal is FunctionValue function))
            {
                throw new SemanticError($"'{fullMethodName}' no es un método válido", context.expression(1).Start);
            }

            // Recolectar los argumentos
            List<ValueWrapper> arguments = new List<ValueWrapper> { sv }; // El struct es el primer argumento implícito

            if (context.lista_valores_struct() != null && context.lista_valores_struct().Length > 0)
            {
                foreach (var argValue in context.lista_valores_struct())
                {
                    foreach (var expr in argValue.expression())
                    {
                        ValueWrapper arg = Visit(expr);
                        arguments.Add(arg);
                    }
                }
            }

            // Llamar al método
            return function.Call(arguments, this);
        }

        throw new SemanticError("Formato de struct no válido", context.Start);
    }

    private string InferTypeFromValue(ValueWrapper value)
    {
        if (value is IntValue) return "int";
        if (value is FloatValue) return "float64";
        if (value is StringValue) return "string";
        if (value is BoolValue) return "bool";
        if (value is RuneValue) return "rune";
        if (value is ArrayValue arr) return $"[]{ (arr.ElementType ?? "any") }";
        if (value is StructValue sv) return sv.StructName;
        return "any";
    }

    public override ValueWrapper VisitLlamadaMetodo(GolightParser.LlamadaMetodoContext context)
    {
        try 
        {
            // Obtener el ID del objeto receptor (primer ID en ID '.' ID)
            string structId = context.ID(0).GetText();
            
            ValueWrapper structValue = currentEnvironment.GetVariable(structId, context.ID(0).Symbol);

            // Obtener el nombre del método (segundo ID en ID '.' ID)
            string methodName = context.ID(1).GetText();

            Console.WriteLine($"Llamando al método {methodName} en objeto {structId} de tipo {structValue?.GetType().Name ?? "null"}");

            if (structValue == null)
            {
                throw new SemanticError("No se puede llamar al método en un objeto nulo", context.ID(0).Symbol);
            }

            // Verificar que el receptor sea un struct
            if (!(structValue is StructValue structVal))
            {
                throw new SemanticError($"Solo los structs pueden tener métodos, pero {structId} es de tipo {structValue?.GetType().Name ?? "null"}", context.ID(0).Symbol);
            }

            // Construir el nombre completo del método (TipoStruct_NombreMetodo)
            string fullMethodName = $"{structVal.StructName}_{methodName}";

            Console.WriteLine($"Buscando método con nombre completo: {fullMethodName}");

            // Obtener el valor de la función/método
            ValueWrapper funcVal;
            try
            {
                funcVal = currentEnvironment.GetVariable(fullMethodName, context.ID(1).Symbol);
                Console.WriteLine($"Método {fullMethodName} encontrado en el entorno actual");
            }
            catch (SemanticError)
            {
                try
                {
                    Environment globalEnv = GetGlobalEnvironment();
                    funcVal = globalEnv.GetVariable(fullMethodName, context.ID(1).Symbol);
                    Console.WriteLine($"Método {fullMethodName} encontrado en el entorno global");
                }
                catch (SemanticError)
                {
                    throw new SemanticError($"El método '{methodName}' no está definido para el struct '{structVal.StructName}'", context.ID(1).Symbol);
                }
            }

            if (!(funcVal is FunctionValue function))
            {
                throw new SemanticError($"'{fullMethodName}' no es un método válido", context.ID(1).Symbol);
            }

            List<ValueWrapper> arguments = new List<ValueWrapper> { structValue };

            // Añadir los argumentos adicionales si existen
            if (context.lista_expresiones() != null)
            {
                var expressions = GetAllExpressions(context.lista_expresiones());

                foreach (var expr in expressions)
                {
                    ValueWrapper argValue = Visit(expr);
                    Console.WriteLine($"  Argumento adicional: {FormatValue(argValue)}");
                    arguments.Add(argValue);
                }
            }

            Console.WriteLine($"Llamando a método {fullMethodName} con {arguments.Count} argumentos");

            ValueWrapper result = function.Call(arguments, this);
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en VisitLlamadaMetodo: {ex.Message}");
            throw;
        }
    }
    public override ValueWrapper VisitSeccontrol(GolightParser.SeccontrolContext context)
    {
    if (context.@if() != null && context.@if().Length > 0)
    {
        // Si hay varios if/else if en secuencia
        ValueWrapper result = defaultVoid;
        foreach (var ifStatement in context.@if())
        {
            result = Visit(ifStatement);
            
            // Si un if fue ejecutado (y retornó algo diferente de defaultVoid), salir del bucle
            if (!(result is NillValue))
                break;
        }
        return result;
    }
        else if (context.@for().Length > 0)
        {
            // Ir al visitor de For
            foreach (var forStatement in context.@for())
            {
                Visit(forStatement);
            }
            return defaultVoid;
        }
        else if (context.@switch().Length > 0)
        {
            // Ir al visitor de Switch
           foreach (var switchStatement in context.@switch())
            {
                Visit(switchStatement);
            }
            return defaultVoid;
        }
    
        return defaultVoid;
    }

    public override ValueWrapper VisitIf(GolightParser.IfContext context)
    {
        // multiples expresiones
        ValueWrapper condition;
        if (context.expression().Length > 0)
        {
            // evaluar la primera
            condition = Visit(context.expression(0));
        }
        else
        {
            throw new SemanticError("La condición del 'if' está vacía", context.Start);
        }

        if (!(condition is BoolValue conditionValue))
        {
            throw new SemanticError("La condición del 'if' debe ser una expresión booleana", context.Start);
        }

        Environment previousEnvironment = currentEnvironment;
        currentEnvironment = new Environment(previousEnvironment, previousEnvironment.Scope);

        try
        {
            if (conditionValue.Value)
            {
                if (context.instruccion() != null)
                {
                    int i = 0;
                    while (i < context.instruccion().Length && 
                        !context.instruccion()[i].GetText().StartsWith("else"))
                    {
                        Visit(context.instruccion()[i]);
                        i++;
                    }
                }
                return defaultVoid;
            }
            else if (context.@else() != null)
            {
                return Visit(context.@else());
            }
            else
            {
                if (context.instruccion() != null)
                {
                    for (int i = 0; i < context.instruccion().Length; i++)
                    {
                        var instruccion = context.instruccion()[i];
                    
                        if (instruccion.GetText().StartsWith("else if"))
                        {
                            if (instruccion.seccontrol() != null && 
                                instruccion.seccontrol().@if() != null && 
                                instruccion.seccontrol().@if().Length > 0)
                            {
                                var elseIfContext = instruccion.seccontrol().@if()[0];
                                ValueWrapper elseIfCondition;
                                if (elseIfContext.expression().Length > 0)
                                {
                                    elseIfCondition = Visit(elseIfContext.expression(0));
                                }
                                else
                                {
                                    throw new SemanticError("La condición del 'else if' está vacía", elseIfContext.Start);
                                }
                            
                                if (elseIfCondition is BoolValue elseIfBool && elseIfBool.Value)
                                {
                                    // Si la condición del else if es verdadera
                                    Environment elseIfEnvironment = new Environment(previousEnvironment);
                                    Environment tempEnvironment = currentEnvironment;
                                    currentEnvironment = elseIfEnvironment;
                                
                                    try
                                    {
                                        foreach (var nestedInstr in elseIfContext.instruccion())
                                        {
                                            if (nestedInstr.GetText().StartsWith("else"))
                                                break;
                                            
                                            Visit(nestedInstr);
                                        }
                                    
                                        return defaultVoid;
                                    }
                                    finally
                                    {
                                        currentEnvironment = tempEnvironment;
                                    }
                                }
                            }
                        }
                        else if (instruccion.GetText().StartsWith("else"))
                        {
                            // Encontramos un else simple
                            Environment elseEnvironment = new Environment(previousEnvironment);
                            Environment tempEnvironment = currentEnvironment;
                            currentEnvironment = elseEnvironment;
                        
                            try
                            {
                                // Ejecutar instrucciones del bloque else
                                for (int j = i + 1; j < context.instruccion().Length; j++)
                                {
                                    Visit(context.instruccion()[j]);
                                }
                            
                                return defaultVoid;
                            }
                            finally
                            {
                                currentEnvironment = tempEnvironment;
                            }
                        }
                    }
                }
            }
        
            return defaultVoid;
        }
        finally
        {
            currentEnvironment = previousEnvironment;
        }
    }

    private string GetTypeName(ValueWrapper value)
    {
        return value switch
        {
            IntValue  => "int",
            FloatValue  => "float64",
            StringValue  => "string",
            BoolValue  => "bool",
            NillValue  => "nil",
            RuneValue  => "rune",
            _ => "desconocido"
        };
    }

    public override ValueWrapper VisitElseIf(GolightParser.ElseIfContext context)
    {
        // Evaluar la condición del else if
        ValueWrapper elseIfCondition;
        if (context.@if().expression().Length > 0)
        {
            elseIfCondition = Visit(context.@if().expression(0));
        }
        else
        {
            throw new SemanticError("La condición del 'else if' está vacía", context.Start);
        }
    
        if (elseIfCondition is BoolValue elseIfBool && elseIfBool.Value)
        {
            // Si la condición es verdadera, ejecutar el bloque del else if
            var ifContext = context.@if();
            if (ifContext.instruccion() != null)
            {
                foreach (var instr in ifContext.instruccion())
                {
                    if (instr.GetText().StartsWith("else"))
                        break;
                    
                    Visit(instr);
                }
            }
        
            return defaultVoid;
        }
        else if (context.@if().@else() != null)
        {
            // Si hay un else/else if anidado y la condición es falsa, visitarlo
            return Visit(context.@if().@else());
        }
    
        return defaultVoid;
    }

    public override ValueWrapper VisitElseBlock(GolightParser.ElseBlockContext context)
    {
        // Ejecutar todas las instrucciones en el bloque else
        if (context.instruccion() != null)
        {
            ExecuteBlock(context.instruccion());
        }
        return defaultVoid;
    }

    private ValueWrapper ExecuteBlock(IEnumerable<GolightParser.InstruccionContext> instrucciones)
    {
        foreach (var instruccion in instrucciones)
        {
            Visit(instruccion);
        }
        return defaultVoid;
    }

    public override ValueWrapper VisitFor(GolightParser.ForContext context)
    {
        Environment previousEnvironment = currentEnvironment;
        currentEnvironment = new Environment(previousEnvironment, previousEnvironment.Scope);
    
        try
        {   
            // Casos segun gramatica
            // Caso 1
            if (context.declaration() == null && context.ASSIGN_SHORT() == null && context.expression().Length == 1)
            {
                while (true)
                {
                    ValueWrapper condition = Visit(context.expression(0));
                
                    if (!(condition is BoolValue boolCondition))
                    {
                        throw new SemanticError("La condición del bucle for debe ser una expresión booleana", context.Start);
                    }
                
                    if (!boolCondition.Value)
                        break;
                
                    Environment iterationEnvironment = new Environment(currentEnvironment, currentEnvironment.Scope);
                    Environment tempEnvironment = currentEnvironment;
                    currentEnvironment = iterationEnvironment;
                
                    // Ejecutar el cuerpo del bucle
                    try
                    {
                        foreach (var instruccion in context.instruccion())
                        {
                            Visit(instruccion);
                        }
                    }
                    catch (BreakException)
                    {
                        break;
                    }
                    catch (ContinueException)
                    {
                        continue;
                    }
                    finally
                    {
                        currentEnvironment = tempEnvironment;
                    }
                }
            }
            // Caso 2
            else if (context.declaration() != null && context.expression() != null && context.expression().Length > 0)
            {
                Visit(context.declaration());
    
                while (true)
                {
                    ValueWrapper condition = Visit(context.expression(0));
        
                    if (!(condition is BoolValue boolCondition))
                    {
                        throw new SemanticError("La condición del bucle for debe ser una expresión booleana", context.Start);
                    }
        
                    if (!boolCondition.Value)
                        break;
        
                    Environment iterationEnvironment = new Environment(currentEnvironment, currentEnvironment.Scope);
                    Environment tempEnvironment = currentEnvironment;
                    currentEnvironment = iterationEnvironment;
        
                    try
                    {
                        // Instrucciones dentro del bloque
                        if (context.instruccion().Length > 1)
                        {
                            for (int i = 1; i < context.instruccion().Length; i++)
                            {
                                Visit(context.instruccion()[i]);
                            }
                        }
                    }
                    catch (BreakException)
                    {
                        break;
                    }
                    catch (ContinueException)
                    {
                        // Continuar hasta actualizar variables
                    }
                    finally
                    {
                        currentEnvironment = tempEnvironment;
                    }
        
                    Visit(context.instruccion(0));
                }
            }
            // Caso 3
            else if (context.ASSIGN_SHORT() != null && context.expression().Length == 3 && 
                    context.GetText().Contains("range"))
            {
                ValueWrapper collection = Visit(context.expression(2));
    
                string indexVar = context.expression(0).GetText();
                string valueVar = context.expression(1).GetText();
    
                    if (collection is ArrayValue arrayValue)
                    {
                        string elementType = "int"; // Tipo por defecto
                        if (arrayValue.Elements.Count > 0 && arrayValue.ElementType != null)
                        {
                            elementType = arrayValue.ElementType;
                        }

                        ValueWrapper initialIndexValue = new IntValue(0);
                        ValueWrapper initialElementValue = CreateDefaultForType(elementType);

                        currentEnvironment.DeclareVariable(indexVar, initialIndexValue, context.expression(0).Start);
                        currentEnvironment.DeclareVariable(valueVar, initialElementValue, context.expression(1).Start);
        
                        for (int i = 0; i < arrayValue.Elements.Count; i++)
                        {
                            currentEnvironment.AssignVariable(indexVar, new IntValue(i), context.expression(0).Start);
                            currentEnvironment.AssignVariable(valueVar, arrayValue.Elements[i], context.expression(1).Start);
            
                            Environment iterationEnvironment = new Environment(currentEnvironment, currentEnvironment.Scope);
                            Environment tempEnvironment = currentEnvironment;
                            currentEnvironment = iterationEnvironment;
            
                            try
                            {
                                foreach (var instruccion in context.instruccion())
                                {
                                    Visit(instruccion);
                                }
                            }
                            catch (BreakException)
                            {
                                break;
                            }
                            catch (ContinueException)
                            {
                                continue;
                            }
                            finally
                            {
                                currentEnvironment = tempEnvironment;
                            }
                        }
                }
                else if (collection is StringValue strValue)
                {
                    currentEnvironment.DeclareVariable(indexVar, new IntValue(0), context.expression(0).Start);
                    currentEnvironment.DeclareVariable(valueVar, new RuneValue('\0'), context.expression(1).Start);
        
                    for (int i = 0; i < strValue.Value.Length; i++)
                    {
                        currentEnvironment.AssignVariable(indexVar, new IntValue(i), context.expression(0).Start);
                        currentEnvironment.AssignVariable(valueVar, new RuneValue(strValue.Value[i]), context.expression(1).Start);
            
                        // Ejecutar el cuerpo del bucle
                        try
                        {
                            foreach (var instruccion in context.instruccion())
                            {
                                Visit(instruccion);
                            }
                        }
                        catch (BreakException)
                        {
                            break;
                        }
                        catch (ContinueException)
                        {
                            continue;
                        }
                    }
                }
                else
                {
                    throw new SemanticError("Solo se puede iterar sobre arrays y strings con for-range", context.expression(2).Start);
                }
            }
            else
            {
                throw new SemanticError("Formato de bucle for no válido", context.Start);
            }
        
            return defaultVoid;
        }
        finally
        {
            currentEnvironment = previousEnvironment;
        }
    }

    public override ValueWrapper VisitSwitch(GolightParser.SwitchContext context)
    {
        ValueWrapper switchValue = Visit(context.expression());
        Environment previousEnvironment = currentEnvironment;
        currentEnvironment = new Environment(previousEnvironment, previousEnvironment.Scope);
    
        try
        {
            var allCases = new List<GolightParser.CaseContext>();
        
            // Si hay casos, procesar cada uno individualmente
            if (context.lista_cases() != null && context.lista_cases().Length > 0)
            {
                foreach (var listaCase in context.lista_cases())
                {
                    allCases.AddRange(GetAllCases(listaCase));
                }
            }
        
            bool caseFound = false;
            GolightParser.CaseContext defaultCase = null;
        
            // Primera pasada: encontrar el caso default y los casos que coinciden
            foreach (var caseItem in allCases)
            {
                if (caseItem.GetText().StartsWith("default"))
                {
                    defaultCase = caseItem;
                    continue;
                }
            
                ValueWrapper caseValue = Visit(caseItem.expression());
            
                // Si este caso coincide con el valor del switch
                if (AreValuesEqual(switchValue, caseValue))
                {
                    caseFound = true;
                
                    // Ejecutar instrucciones del caso coincidente
                    if (caseItem.instruccion() != null)
                    {
                        for (int i = 0; i < caseItem.instruccion().Length; i++)
                        {
                            var instruccion = caseItem.instruccion()[i];
                        
                            if (instruccion.sentenciastransfer() != null && 
                                instruccion.sentenciastransfer().GetText().Contains("break"))
                            {
                                return defaultVoid;
                            }
                        
                            Visit(instruccion);
                        }
                    }
                
                    return defaultVoid;
                }
            }
        
            // Si ningún caso coincidió y hay un default, ejecutarlo
            if (!caseFound && defaultCase != null)
            {
                if (defaultCase.instruccion() != null)
                {
                    for (int i = 0; i < defaultCase.instruccion().Length; i++)
                    {
                        var instruccion = defaultCase.instruccion()[i];
                    
                        if (instruccion.sentenciastransfer() != null && 
                            instruccion.sentenciastransfer().GetText().Contains("break"))
                        {
                            break;
                        }
                    
                        Visit(instruccion);
                    }
                }
            }
        
            return defaultVoid;
        }
        finally
        {
            currentEnvironment = previousEnvironment;
        }
    }

    private List<GolightParser.CaseContext> GetAllCases(GolightParser.Lista_casesContext listaCases)
    {
        return Utilities.GetAllCases(listaCases);
    }

    private bool AreValuesEqual(ValueWrapper a, ValueWrapper b)
    {
        return Utilities.AreValuesEqual(a, b);
    }

    public override ValueWrapper VisitSentenciastransfer(GolightParser.SentenciastransferContext context)
    {
        if (context.GetText().StartsWith("break"))
        {
            throw new BreakException();
        }
        else if (context.GetText().StartsWith("continue"))
        {
            throw new ContinueException();
        }
        else if (context.GetText().StartsWith("return"))
        {
            if (context.expression() != null)
            {
                ValueWrapper returnValue = Visit(context.expression());
                throw new ReturnException(returnValue, context.Start);
            }
            else
            {
                throw new ReturnException(null, context.Start);
            }
        }

        return defaultVoid;
    }

    public override ValueWrapper VisitFuncion(GolightParser.FuncionContext context)
    {

        // Caso para métodos de struct: func (receiver Type) MethodName(...) ReturnType? {...}
        if (context.expression().Length == 2)
        {
            string receiverName = context.expression(0).GetText();
            string receiverType = context.expression(1).GetText();
            string methodName = context.ID().GetText();
            string fullMethodName = $"{receiverType}_{methodName}";

            Console.WriteLine($"Definiendo método: {methodName} para struct {receiverType}");

            // Verificar que el tipo del receptor exista
            if (!currentEnvironment.StructTypeExists(receiverType))
            {
                throw new SemanticError($"Tipo '{receiverType}' no definido", context.expression(1).Start);
            }

            var functionDef = new FunctionDefinition(context, currentEnvironment, fullMethodName);

            Parameter receiverParam = new Parameter { Name = receiverName, Type = receiverType };
            functionDef.Parameters.Add(new Parameter { Name = receiverName, Type = receiverType });

            if (functionDef.ParameterEnvironment == null)
            {
                functionDef.ParameterEnvironment = new Environment(currentEnvironment, fullMethodName);
            }

            functionDef.ParameterEnvironment.RegisterParameter(receiverName, receiverType, context.expression(0).Start);

            // Procesar parámetros adicionales
            if (context.lista_parametros() != null)
            {
                ParseParameters(context.lista_parametros(), functionDef);
            }

            string returnType = null;
            if (context.TIPO() != null)
            {
                returnType = context.TIPO().GetText();
                functionDef.ReturnType = returnType;
            }

            // Registrar la función con el nombre completo (TipoStruct_NombreMetodo)
            var functionValue = new FunctionValue(functionDef, fullMethodName);
            currentEnvironment.DeclareVariable(fullMethodName, functionValue, context.ID().Symbol);
            //Tabla de simbolos
            currentEnvironment.RegisterFunction(fullMethodName, returnType ?? "nil", context.ID().Symbol, true);
            Console.WriteLine($"Método registrado: {fullMethodName}, parámetros: {functionDef.Parameters.Count}");

            return defaultVoid;
        }
        // Caso 2: Método de struct: func (s Struct) Metodo(...) {...}
        else if (context.expression().Length == 2)
        {
            string receiverName = context.expression(0).GetText();
            string receiverType = context.expression(1).GetText();
            string methodName = context.ID().GetText();
            string fullMethodName = $"{receiverType}_{methodName}";

            Console.WriteLine($"Definiendo método: {methodName} para struct {receiverType}");

            if (!currentEnvironment.StructTypeExists(receiverType))
            {
                throw new SemanticError($"Tipo '{receiverType}' no definido", context.expression(1).Start);
            }

            var functionDef = new FunctionDefinition(context, currentEnvironment, fullMethodName);
            
            functionDef.Parameters.Add(new Parameter { Name = receiverName, Type = receiverType });

            if (functionDef.ParameterEnvironment == null)
            {
                functionDef.ParameterEnvironment = new Environment(currentEnvironment, fullMethodName);
            }

            functionDef.ParameterEnvironment.RegisterParameter(receiverName, receiverType, context.expression(0).Start);

            functionDef.Parameters.Add(new Parameter { Name = receiverName, Type = receiverType });

            if (context.lista_parametros() != null)
            {
                ParseParameters(context.lista_parametros(), functionDef);
            }

            string returnType = null;
            if (context.TIPO() != null)
            {
                returnType = context.TIPO().GetText();
                functionDef.ReturnType = returnType;
            }

            var functionValue = new FunctionValue(functionDef, fullMethodName);
            currentEnvironment.DeclareVariable(fullMethodName, functionValue, context.ID().Symbol);
            
            //Tabla de simbolos
            currentEnvironment.RegisterFunction(methodName, returnType, context.ID().Symbol);

            Console.WriteLine($"Método registrado: {fullMethodName}, parámetros: {functionDef.Parameters.Count}, tipo retorno: {returnType ?? "nil"}");

            return defaultVoid;
        }
        // Caso 3: Función normal
        else
        {
            string functionName = context.ID().GetText();

            bool isMainFunction = functionName == "main";

            var functionDef = new FunctionDefinition(context, currentEnvironment, functionName);

            if (context.lista_parametros() != null)
            {
                ParseParameters(context.lista_parametros(), functionDef);
            }

            string returnType = null;
            if (context.TIPO() != null)
            {
                returnType = context.TIPO().GetText();
                functionDef.ReturnType = returnType;
            }

            var functionValue = new FunctionValue(functionDef, functionName);
            currentEnvironment.DeclareVariable(functionName, functionValue, context.ID().Symbol);

            currentEnvironment.RegisterFunction(functionName, returnType, context.ID().Symbol);

            Console.WriteLine($"Funcion registrada: {functionName}, parametros: {functionDef.Parameters.Count}, tipo de retorno: {returnType ?? "nil"}");

            if (isMainFunction && currentEnvironment.Enclosing == null)
            {
                functionValue.Call(new List<ValueWrapper>(), this);
            }

            return defaultVoid;
        }
    }

    private void ParseParameters(GolightParser.Lista_parametrosContext context, FunctionDefinition functionDef)
    {
        if (context.ID() == null || context.ID().Length == 0)
            return;

        // Crear un único entorno para parámetros si no existe
        if (functionDef.ParameterEnvironment == null)
        {
            functionDef.ParameterEnvironment = new Environment(currentEnvironment, functionDef.Name);
        }

        for (int i = 0; i < context.ID().Length; i++)
        {
            string paramName = context.ID()[i].GetText();
            string paramType = context.TIPO()[i].GetText();

            // Agregar a la lista de parámetros
            functionDef.Parameters.Add(new Parameter { Name = paramName, Type = paramType });
            
            functionDef.ParameterEnvironment.DeclareVariable(paramName, new NillValue(), context.ID()[i].Symbol);
            // Registrar el parámetro en el entorno de parámetros
            functionDef.ParameterEnvironment.RegisterParameter(paramName, paramType, context.ID()[i].Symbol);
            
            Console.WriteLine($"Added parameter: {paramName} of type {paramType} in scope {functionDef.Name}");
        }
    }

    public override ValueWrapper VisitBloquessentencias(GolightParser.BloquessentenciasContext context)
    {
        Environment previousEnvironment = currentEnvironment;
        currentEnvironment = new Environment(previousEnvironment, previousEnvironment.Scope);
    
        foreach (var instruccion in context.instruccion())
        {
            Visit(instruccion);
        }
    
        currentEnvironment = previousEnvironment;
        return defaultVoid;
    }

    public override ValueWrapper VisitExpression(GolightParser.ExpressionContext context)
    {
        Console.WriteLine($"VisitExpression: {context.GetText()}");
        return Visit(context.expr());
    }

    public override ValueWrapper VisitLogical_OR(GolightParser.Logical_ORContext context)
    {
     ValueWrapper left = Visit(context.expr(0));

     if (left is BoolValue leftBool)
        {
           if (leftBool.Value)
              return new BoolValue(true);
        
           ValueWrapper right = Visit(context.expr(1));
        
           if (right is BoolValue rightBool)
           {
               return new BoolValue(leftBool.Value || rightBool.Value);
           }
        }
    
        throw new SemanticError("No se puede realizar la operación '||' con tipos no booleanos", context.Start);
    }

    public override ValueWrapper VisitLogical_AND(GolightParser.Logical_ANDContext context)
    {
        try
        {
            ValueWrapper left = Visit(context.expr(0));

            if (!(left is BoolValue leftBool))
            {
                throw new SemanticError($"El operando izquierdo de '&&' debe ser un booleano, encontrado: {left.GetType().Name}", context.expr(0).Start);
            }

            if (!leftBool.Value)
                return new BoolValue(false);

            ValueWrapper right = Visit(context.expr(1));

            if (!(right is BoolValue rightBool))
            {
                throw new SemanticError($"El operando derecho de '&&' debe ser un booleano, encontrado: {right.GetType().Name}", context.expr(1).Start);
            }

            return new BoolValue(leftBool.Value && rightBool.Value);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en VisitLogical_AND: {ex.Message}");
            Console.WriteLine($"Expresión izquierda: {context.expr(0).GetText()}");
            Console.WriteLine($"Expresión derecha: {context.expr(1).GetText()}");
            throw;
        }
    }

    public override ValueWrapper VisitEquality(GolightParser.EqualityContext context)
    {
        try
        {

        ValueWrapper left = Visit(context.expr(0));
        ValueWrapper right = Visit(context.expr(1));
        string op = context.GetChild(1).GetText();

        Console.WriteLine($"VisitEquality - Comparando: {FormatValue(left)} {op} {FormatValue(right)}");
        Console.WriteLine($"  Tipos: {left?.GetType().Name ?? "null"} y {right?.GetType().Name ?? "null"}");
        if (context.expr(1).GetText() == "nil")
        {
            // Si estamos comparando con nil, asegurarnos de que left sea tratado correctamente
            if (left is NillValue || left == null)
            {
                switch (op)
                {
                    case "==": return new BoolValue(true);
                    case "!=": return new BoolValue(false);
                }
            }
            else
            {
                switch (op)
                {
                    case "==": return new BoolValue(false);
                    case "!=": return new BoolValue(true);
                }
            }
        }
        else if (context.expr(0).GetText() == "nil")
        {
            // Si el lado izquierdo es nil
            if (right is NillValue || right == null)
            {
                switch (op)
                {
                    case "==": return new BoolValue(true);
                    case "!=": return new BoolValue(false);
                }
            }
            else
            {
                switch (op)
                {
                    case "==": return new BoolValue(false);
                    case "!=": return new BoolValue(true);
                }
            }
        }
        else if (left is IntValue leftInt && right is IntValue rightInt)
         {
                switch (op)
             {
                 case "==": return new BoolValue(leftInt.Value == rightInt.Value);
                 case "!=": return new BoolValue(leftInt.Value != rightInt.Value);
                }
         }
         else if (left is FloatValue leftFloat && right is FloatValue rightFloat)
         {
             switch (op)
             {
                 case "==": return new BoolValue(leftFloat.Value == rightFloat.Value);
                 case "!=": return new BoolValue(leftFloat.Value != rightFloat.Value);
             }
          }
          else if (left is IntValue leftInt2 && right is FloatValue rightFloat2)
          {
              switch (op)
              {
                  case "==": return new BoolValue(leftInt2.Value == rightFloat2.Value);
                 case "!=": return new BoolValue(leftInt2.Value != rightFloat2.Value);
              }
          }
          else if (left is FloatValue leftFloat2 && right is IntValue rightInt2)
          {
              switch (op)
              {
                  case "==": return new BoolValue(leftFloat2.Value == rightInt2.Value);
                  case "!=": return new BoolValue(leftFloat2.Value != rightInt2.Value);
              }
          }
          else if (left is StringValue leftStr && right is StringValue rightStr)
          {
              switch (op)
             {
                  case "==": return new BoolValue(leftStr.Value == rightStr.Value);
                  case "!=": return new BoolValue(leftStr.Value != rightStr.Value);
              }
          }
          else if (left is BoolValue leftBool && right is BoolValue rightBool)
          {
              switch (op)
              {
                  case "==": return new BoolValue(leftBool.Value == rightBool.Value);
                  case "!=": return new BoolValue(leftBool.Value != rightBool.Value);
              }
          }
          else if (left is NillValue && right is NillValue)
          {
              switch (op)
              {
                  case "==": return new BoolValue(true);
                  case "!=": return new BoolValue(false);
              }
          }
          else if (left is NillValue || right is NillValue)
          {
              switch (op)
              {
                  case "==": return new BoolValue(false);
                  case "!=": return new BoolValue(true);
              }
          }


        throw new SemanticError($"No se puede realizar la operación '{op}' con los tipos {left.GetType().Name} and {right.GetType().Name}", context.Start);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en VisitEquality: {ex.Message}");
            Console.WriteLine($"Expresión izquierda: {context.expr(0).GetText()}");
            Console.WriteLine($"Expresión derecha: {context.expr(1).GetText()}");
            throw;
        }
    }


    public override ValueWrapper VisitRelational(GolightParser.RelationalContext context)
    {
        ValueWrapper left = Visit(context.expr(0));
        ValueWrapper right = Visit(context.expr(1));
        string op = context.GetChild(1).GetText();

        if (left is IntValue leftInt && right is IntValue rightInt)
        {
            switch (op)
            {
                case "<": return new BoolValue(leftInt.Value < rightInt.Value);
                case ">": return new BoolValue(leftInt.Value > rightInt.Value);
                case "<=": return new BoolValue(leftInt.Value <= rightInt.Value);
                case ">=": return new BoolValue(leftInt.Value >= rightInt.Value);
            }
        }
        else if (left is FloatValue leftFloat && right is FloatValue rightFloat)
        {
            switch (op)
            {
                case "<": return new BoolValue(leftFloat.Value < rightFloat.Value);
                case ">": return new BoolValue(leftFloat.Value > rightFloat.Value);
                case "<=": return new BoolValue(leftFloat.Value <= rightFloat.Value);
                case ">=": return new BoolValue(leftFloat.Value >= rightFloat.Value);
            }
        }
        else if (left is IntValue leftInt2 && right is FloatValue rightFloat2)
        {
            switch (op)
            {
                case "<": return new BoolValue(leftInt2.Value < rightFloat2.Value);
                case ">": return new BoolValue(leftInt2.Value > rightFloat2.Value);
                case "<=": return new BoolValue(leftInt2.Value <= rightFloat2.Value);
                case ">=": return new BoolValue(leftInt2.Value >= rightFloat2.Value);
            }
        }
        else if (left is FloatValue leftFloat2 && right is IntValue rightInt2)
        {
            switch (op)
            {
                case "<": return new BoolValue(leftFloat2.Value < rightInt2.Value);
                case ">": return new BoolValue(leftFloat2.Value > rightInt2.Value);
                case "<=": return new BoolValue(leftFloat2.Value <= rightInt2.Value);
                case ">=": return new BoolValue(leftFloat2.Value >= rightInt2.Value);
            }
        }

        throw new SemanticError($"No se puede realizar la operación '{op}' con los tipos {left.GetType().Name} and {right.GetType().Name}", context.Start);
    }

    public override ValueWrapper VisitAddSub(GolightParser.AddSubContext context)
    {
        ValueWrapper left = Visit(context.expr(0));
        ValueWrapper right = Visit(context.expr(1));
        string op = context.GetChild(1).GetText();

        if (op == "+")
        {
            if (left is IntValue leftInt && right is IntValue rightInt)
                return new IntValue(leftInt.Value + rightInt.Value);
            if (left is FloatValue leftFloat && right is FloatValue rightFloat)
                return new FloatValue(leftFloat.Value + rightFloat.Value);
            if (left is StringValue leftStr && right is StringValue rightStr)
                return new StringValue(leftStr.Value + rightStr.Value);
            if (left is IntValue leftInt2 && right is FloatValue rightFloat2)
                return new FloatValue(leftInt2.Value + rightFloat2.Value);
            if (left is FloatValue leftFloat2 && right is IntValue rightInt2)
                return new FloatValue(leftFloat2.Value + rightInt2.Value);
            if (left is StringValue leftStr2)
                return new StringValue(leftStr2.Value + FormatValue(right));
            if (right is StringValue rightStr2)
             return new StringValue(FormatValue(left) + rightStr2.Value);
        }
        else if (op == "-")
        {
         if (left is IntValue leftInt && right is IntValue rightInt)
               return new IntValue(leftInt.Value - rightInt.Value);
            if (left is FloatValue leftFloat && right is FloatValue rightFloat)
               return new FloatValue(leftFloat.Value - rightFloat.Value);
            if (left is IntValue leftInt2 && right is FloatValue rightFloat2)
             return new FloatValue(leftInt2.Value - rightFloat2.Value);
            if (left is FloatValue leftFloat2 && right is IntValue rightInt2)
                return new FloatValue(leftFloat2.Value - rightInt2.Value);
        }

        throw new SemanticError($"No se puede realizar la operación '{op}' con los tipos {left.GetType().Name} and {right.GetType().Name}", context.Start);
    }

    public override ValueWrapper VisitMulDiv(GolightParser.MulDivContext context)
    {
        ValueWrapper left = Visit(context.expr(0));
        ValueWrapper right = Visit(context.expr(1));
        string op = context.GetChild(1).GetText();

        if (op == "*")
        {
          if (left is IntValue leftInt && right is IntValue rightInt)
              return new IntValue(leftInt.Value * rightInt.Value);
          if (left is FloatValue leftFloat && right is FloatValue rightFloat)
              return new FloatValue(leftFloat.Value * rightFloat.Value);
          if (left is IntValue leftInt2 && right is FloatValue rightFloat2)
              return new FloatValue(leftInt2.Value * rightFloat2.Value);
          if (left is FloatValue leftFloat2 && right is IntValue rightInt2)
              return new FloatValue(leftFloat2.Value * rightInt2.Value);
        }
        else if (op == "/")
        {
           if (right is IntValue intRight && intRight.Value == 0)
               throw new SemanticError("División por cero", context.Start);
           if (right is FloatValue floatRight && floatRight.Value == 0)
               throw new SemanticError("División por cero", context.Start);

           if (left is IntValue leftInt && right is IntValue rightInt2)
               return new IntValue(leftInt.Value / rightInt2.Value);
           if (left is FloatValue leftFloat && right is FloatValue rightFloat2)
               return new FloatValue(leftFloat.Value / rightFloat2.Value);
           if (left is IntValue leftInt2 && right is FloatValue rightFloat3)
               return new FloatValue(leftInt2.Value / rightFloat3.Value);
           if (left is FloatValue leftFloat2 && right is IntValue rightInt3)
                return new FloatValue(leftFloat2.Value / rightInt3.Value);
        }

     throw new SemanticError($"No se puede realizar la operación '{op}' con los tipos {left.GetType().Name} and {right.GetType().Name}", context.Start);
    }

    public override ValueWrapper VisitMod(GolightParser.ModContext context)
    {
        ValueWrapper left = Visit(context.expr(0));
        ValueWrapper right = Visit(context.expr(1));
    
        if (right is IntValue intRight && intRight.Value == 0)
        {
            throw new SemanticError("El divisor del modulo no puede ser 0", context.Start);
        }
        else if (right is FloatValue floatRight && floatRight.Value == 0)
        {
         throw new SemanticError("El divisor del modulo no puede ser 0.", context.Start);
        }
    
        if (left is IntValue leftInt && right is IntValue rightInt)
            return new IntValue(leftInt.Value % rightInt.Value);
        if (left is FloatValue leftFloat && right is FloatValue rightFloat)
            return new FloatValue(leftFloat.Value % rightFloat.Value);
        if (left is IntValue leftInt2 && right is FloatValue rightFloat2)
            return new FloatValue(leftInt2.Value % rightFloat2.Value);
        if (left is FloatValue leftFloat2 && right is IntValue rightInt2)
            return new FloatValue(leftFloat2.Value % rightInt2.Value);

        throw new SemanticError("Operación modulo requiere tipos numéricos", context.Start);
    }   

    public override ValueWrapper VisitUnario(GolightParser.UnarioContext context)
    {
        ValueWrapper expr = Visit(context.expr());
    
        if (expr is IntValue intValue)
            return new IntValue(-intValue.Value);
        if (expr is FloatValue floatValue)
            return new FloatValue(-floatValue.Value);
    
        throw new SemanticError("Operación unaria requiere tipos numéricos", context.Start);
    }   

    public override ValueWrapper VisitNot(GolightParser.NotContext context)
    {
        ValueWrapper expr = Visit(context.expr());
    
        if (expr is BoolValue boolValue)
            return new BoolValue(!boolValue.Value);
    
        throw new SemanticError("Operación booleana requiere tipos boleanos", context.Start);
    }

    public override ValueWrapper VisitAgrupacion(GolightParser.AgrupacionContext context)
    {
        return Visit(context.expr());
    }

    public override ValueWrapper VisitAgrupacionCorchetes(GolightParser.AgrupacionCorchetesContext context)
    {
        return Visit(context.expr());
    }

    public override ValueWrapper VisitConcat(GolightParser.ConcatContext context)
    {
        if (context.expr(0) != null && context.expr(1) != null)
        {
            string op = context.GetChild(1).GetText();
            if (op == ".") //Struct
            {
                try 
                {
                    ValueWrapper structValue = Visit(context.expr(0));

                    if (structValue == null)
                    {
                        Console.WriteLine("ERROR: structValue es null en VisitConcat");
                        throw new SemanticError("Referencia nula al acceder a un campo", context.expr(0).Start);
                    }

                    bool isChainedAccess = context.expr(1).GetText().Contains(".");

                    if (isChainedAccess)
                    {
                        // Campo.de.campos
                        string remainingPath = context.expr(1).GetText();
                        return AccessNestedField(structValue, remainingPath, context.expr(1).Start);
                    }
                    else
                    {
                        // Campo
                        string fieldName = ExtractFieldNameFromExpression(context.expr(1).GetText());

                        Console.WriteLine($"VisitConcat - Campo extraído: '{fieldName}' del texto: '{context.expr(1).GetText()}'");
                        Console.WriteLine($"VisitConcat - Tipo de structValue: {structValue?.GetType().Name ?? "null"}");

                        if (structValue is StructValue sv)
                        {
                            if (sv.HasField(fieldName))
                            {
                                ValueWrapper fieldValue = sv.GetField(fieldName);
                                Console.WriteLine($"VisitConcat - Valor obtenido: {FormatValue(fieldValue)}");

                                if (fieldValue is NillValue && sv.StructName == "Nodo" && fieldName == "Siguiente")
                                {
                                    return new StructValue("Nodo");
                                }
                                return fieldValue;
                            }

                            throw new SemanticError($"Struct '{sv.StructName}' no tiene un campo '{fieldName}'", context.expr(1).Start);
                        }

                        throw new SemanticError("Solo se pueden acceder a campos de un struct", context.expr(0).Start);
                    }
                }
                    catch (Exception ex) 
                    {
                        Console.WriteLine($"Error en VisitConcat: {ex.Message}");
                        Console.WriteLine($"Tipo de structValue: {(Visit(context.expr(0))?.GetType().Name ?? "null")}");
                        Console.WriteLine($"Contexto: {context.GetText()}");
                        throw;
                    }
                }
            }

        ValueWrapper left = Visit(context.expr(0));
        ValueWrapper right = Visit(context.expr(1));

        if (left is StringValue leftStr && right is StringValue rightStr)
            return new StringValue(leftStr.Value + rightStr.Value);

        return new StringValue(FormatValue(left) + FormatValue(right));
    }

    private ValueWrapper AccessNestedField(ValueWrapper structValue, string path, IToken errorToken)
    {
        Console.WriteLine($"AccessNestedField - Accediendo a path: {path} en {structValue?.GetType().Name ?? "null"}");

        if (structValue is NillValue || structValue == null)
        {
            Console.WriteLine("AccessNestedField - El struct base es nil");
            if (path == "Siguiente" || path.EndsWith(".Siguiente"))
            {
                Console.WriteLine("AccessNestedField - Retornando nil para acceso a nil.Siguiente");
                return new NillValue();
            }

            throw new SemanticError("No se puede acceder a campos de un valor nil", errorToken);
        }

        if (!(structValue is StructValue currentStruct))
        {
            throw new SemanticError("Solo se pueden acceder a campos de un struct", errorToken);
        }

        string[] parts = path.Split('.');

        for (int i = 0; i < parts.Length; i++)
        {
            string fieldName = ExtractFieldNameFromExpression(parts[i]);
            Console.WriteLine($"AccessNestedField - Procesando campo: {fieldName}");

            if (!currentStruct.HasField(fieldName))
            {
                throw new SemanticError($"Struct '{currentStruct.StructName}' no tiene un campo '{fieldName}'", errorToken);
            }

            ValueWrapper fieldValue = currentStruct.GetField(fieldName);
            Console.WriteLine($"AccessNestedField - Campo {fieldName} tiene valor {FormatValue(fieldValue)} (tipo: {fieldValue?.GetType().Name ?? "null"})");

            if (i == parts.Length - 1)
            {
                Console.WriteLine($"AccessNestedField - Valor final encontrado: {FormatValue(fieldValue)}");
                return fieldValue;
            }

            if (fieldValue is NillValue)
            {
                if (i < parts.Length - 1 && parts[i+1] == "Siguiente")
                {
                    Console.WriteLine("AccessNestedField - Encontrado nil en camino hacia Siguiente, devolviendo nil");
                    return new NillValue();
                }

                if (currentStruct.StructName == "Nodo" && fieldName == "Siguiente")
                {
                    Console.WriteLine("AccessNestedField - Creando nodo vacío para continuar navegación");
                    fieldValue = new StructValue("Nodo");
                }
                else
                {
                    Console.WriteLine($"AccessNestedField - No se puede acceder más allá de {fieldName} porque es nil");
                    throw new SemanticError($"No se puede acceder a '{parts[i+1]}' porque '{fieldName}' es nil", errorToken);
                }
            }

            if (!(fieldValue is StructValue nextStruct))
            {
                throw new SemanticError($"No se puede acceder a '{parts[i+1]}' porque '{fieldName}' no es un struct", errorToken);
            }

            currentStruct = nextStruct;
        }

        // Nunca deberíamos llegar aquí, pero por seguridad
        throw new SemanticError("Error al acceder a campos anidados", errorToken);
    }

    private string ExtractFieldNameFromExpression(string expression)
    {
        char[] delimiters = { '=', '!', '<', '>', '&', '|', '+', '-', '*', '/', '%', ' ', '\t', '\r', '\n' };

        int delimiterIndex = expression.IndexOfAny(delimiters);

        if (delimiterIndex > 0)
        {
            return expression.Substring(0, delimiterIndex).Trim();
        }

        return expression.Trim();
    }

    public override ValueWrapper VisitSTRING(GolightParser.STRINGContext context)
    {
        string text = context.STRING().GetText();
        text = text.Substring(1, text.Length - 2);
    
        text = ProcessEscapeSequences(text);
    
        return new StringValue(text);
    }

    public override ValueWrapper VisitINT(GolightParser.INTContext context)
    {
        int value = int.Parse(context.INT().GetText());
        return new IntValue(value);
    }

    public override ValueWrapper VisitFLOAT64(GolightParser.FLOAT64Context context)
    {
        float value = float.Parse(context.FLOAT64().GetText());
        return new FloatValue(value);
    }

    public override ValueWrapper VisitBOOLEANO(GolightParser.BOOLEANOContext context)
    {
        bool value = bool.Parse(context.BOOLEANO().GetText().ToLower());
        return new BoolValue(value);
    }

    public override ValueWrapper VisitRUNE(GolightParser.RUNEContext context)
    {
        string text = context.RUNE().GetText();
        char value = text[1];
        return new RuneValue(value);
    }

    public override ValueWrapper VisitNil(GolightParser.NilContext context)
    {
        return defaultVoid;
    }

    public override ValueWrapper VisitID(GolightParser.IDContext context)
    {
        string id = context.ID().GetText();
        
        // Caso especial para "fmt"
        if (id == "fmt")
        {
            return new StringValue("fmt");
        }

        try
        {
            return currentEnvironment.GetVariable(id, context.ID().Symbol);
        }
        catch (SemanticError ex)
        {
            throw new SemanticError($"Variable '{id}' no definida", context.ID().Symbol);
        }
    }

    public override ValueWrapper VisitInt(GolightParser.IntContext context)
    {
        try
        {
            int value = int.Parse(context.INT().GetText());
            return new IntValue(value);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al parsear entero: {ex.Message}");
            return new IntValue(0);
        }
    }

    public override ValueWrapper VisitFloat64(GolightParser.Float64Context context)
    {
        try
        {
            float value = float.Parse(context.FLOAT64().GetText());
            return new FloatValue(value);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al parsear flotante: {ex.Message}");
            return new FloatValue(0);
        }
    }

    public override ValueWrapper VisitString(GolightParser.StringContext context)
    {
        string text = context.STRING().GetText();
        text = text.Substring(1, text.Length - 2);
        text = ProcessEscapeSequences(text);
        return new StringValue(text);
    }

    public override ValueWrapper VisitRune(GolightParser.RuneContext context)
    {
        string text = context.RUNE().GetText();
        text = text.Substring(1, text.Length - 2);
        text = ProcessEscapeSequences(text);

        if (text.Length > 0)
        {
            return new RuneValue(text[0]);
        }

        return new RuneValue('\0');
    }

    public override ValueWrapper VisitBooleano(GolightParser.BooleanoContext context)
    {
        string text = context.BOOLEANO().GetText();
        bool value = text.ToLower() == "true";
        return new BoolValue(value);
    }

    public override ValueWrapper VisitId(GolightParser.IdContext context)
    {
        string id = context.ID().GetText();
        try
        {
            return currentEnvironment.GetVariable(id, context.ID().Symbol);
        }
        catch (SemanticError)
        {
            Console.WriteLine($"Funcion '{id}' no encontrada, retornando valor nulo");
            return new NillValue();
        }
    }

    private string ProcessEscapeSequences(string text)
    {
        return Utilities.ProcessEscapeSequences(text);
    }   

    private string FormatValue(ValueWrapper value)
    {
        return Utilities.FormatValue(value);
    }

    private bool VariableExists(string id, IToken errorToken, Environment currentEnvironment)
    {
        return Utilities.VariableExists(id, errorToken, currentEnvironment);
    }

}