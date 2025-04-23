using System;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using analyzer;
using static SemanticVisitor;

public static class Utilities
{
    public static bool AreValuesEqual(ValueWrapper a, ValueWrapper b)
    {
        if (a is IntValue aInt && b is IntValue bInt)
        return aInt.Value == bInt.Value;
        
        if (a is FloatValue aFloat && b is FloatValue bFloat)
            return aFloat.Value == bFloat.Value;
            
        if (a is StringValue aStr && b is StringValue bStr)
            return aStr.Value == bStr.Value;
            
        if (a is BoolValue aBool && b is BoolValue bBool)
            return aBool.Value == bBool.Value;
            
        if (a is ArrayValue aArray && b is ArrayValue bArray)
        {
            // Si los arrays tienen diferente tamaño, no son iguales
            if (aArray.Elements.Count != bArray.Elements.Count)
                return false;
                
            // Comparar elemento por elemento
            for (int i = 0; i < aArray.Elements.Count; i++)
            {
                if (!AreValuesEqual(aArray.Elements[i], bArray.Elements[i]))
                    return false;
            }
            return true;
        }
        if (a is StructValue aStruct && b is StructValue bStruct)
        {
            return AreStructsEqual(aStruct, bStruct);
        }
        
        if (a is NillValue && !(b is NillValue))
            return false;
        if (!(a is NillValue) && b is NillValue)
            return false;
        
        if (a is NillValue && b is NillValue)
            return true;
        
        // Si los tipos son diferentes, intentar conversiones implícitas
        if (a is IntValue aInt2 && b is FloatValue bFloat2)
            return aInt2.Value == bFloat2.Value;
            
        if (a is FloatValue aFloat2 && b is IntValue bInt2)
            return aFloat2.Value == bInt2.Value;
            
        return false;
    }

    public static string FormatValue(ValueWrapper value)
    {
        if (value is IntValue i)
            return i.Value.ToString();
        else if (value is FloatValue f)
            return f.Value.ToString("0.0####");
        else if (value is StringValue s)
            return s.Value;
        else if (value is BoolValue b)
            return b.Value.ToString().ToLower();
        else if (value is RuneValue r)
            return r.Value.ToString();
        else if (value is NillValue)
            return "nil";
        else if (value is FunctionValue funcVal)
        {
            return funcVal.ToString();
        }
        else if (value is ArrayValue arr)
        {
            // Formatear correctamente los elementos del array
            var formattedElements = new List<string>();
            foreach (var element in arr.Elements)
            {
                formattedElements.Add(FormatValue(element));
            }
            return "[" + string.Join(" ", formattedElements) + "]";
        }
        else if (value is StructValue structVal)
        {
            // Formatear el struct con todos sus campos
            var fields = structVal.Fields
                .Select(f => $"{f.Key}:{FormatValue(f.Value)}")
                .ToList();

            return $"{{{string.Join(" ", fields)}}}";
        }
        else
            return "undefined";
    }

    public static string ProcessEscapeSequences(string text)
    {
        return text.Replace("\\n", "\n")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
    }

    public static ValueWrapper ConvertToType(ValueWrapper value, string targetType)
    {
        if (value == null)
        return CreateDefaultForType(targetType);

        if ((targetType == "int" && value is IntValue) ||
            (targetType == "float64" && value is FloatValue) ||
            (targetType == "string" && value is StringValue) ||
            (targetType == "bool" && value is BoolValue) ||
            (targetType == "rune" && value is RuneValue))
        {
            return value;
        }
    
        switch (targetType)
        {
            case "float64":
                if (value is IntValue intVal)
                    return new FloatValue(intVal.Value);
                else if (value is StringValue strValFloat && float.TryParse(strValFloat.Value, out float fResult))
                    return new FloatValue(fResult);
                break;
                
            case "int":
                if (value is FloatValue floatVal)
                    return new IntValue((int)floatVal.Value);
                else if (value is StringValue strValInt && int.TryParse(strValInt.Value, out int iResult))
                    return new IntValue(iResult);
                break;
                
            case "string":
                return new StringValue(FormatValue(value));
                
            case "bool":
                if (value is StringValue strValBool)
                {
                    if (string.Equals(strValBool.Value, "true", StringComparison.OrdinalIgnoreCase))
                        return new BoolValue(true);
                    else if (string.Equals(strValBool.Value, "false", StringComparison.OrdinalIgnoreCase))
                        return new BoolValue(false);
                }
                // También permitir convertir int a bool (0 = false, !0 = true)
                else if (value is IntValue intValBool)
                {
                    return new BoolValue(intValBool.Value != 0);
                }
                break;
                
            case "rune":
                if (value is StringValue strValRune && strValRune.Value.Length > 0)
                    return new RuneValue(strValRune.Value[0]);
                else if (value is IntValue intValRune)
                    return new RuneValue((char)intValRune.Value);
                break;
        }

        return value;
    }

    public static ValueWrapper CreateDefaultForType(string tipo)
    {
        switch (tipo)
        {
            case "int": return new IntValue(0);
            case "float64": return new FloatValue(0.0f);
            case "string": return new StringValue("");
            case "bool": return new BoolValue(false);
            case "rune": return new RuneValue('\0');
            default: return new NillValue();
        }
    }

    public static ValueWrapper PerformAddition(ValueWrapper left, ValueWrapper right, IToken errorToken)
    {
        // Suma de enteros
        if (left is IntValue leftInt && right is IntValue rightInt)
            return new IntValue(leftInt.Value + rightInt.Value);
    
        // Suma de flotantes
        if (left is FloatValue leftFloat && right is FloatValue rightFloat)
            return new FloatValue(leftFloat.Value + rightFloat.Value);
    
        // Suma de strings (concatenación)
        if (left is StringValue leftStr && right is StringValue rightStr)
            return new StringValue(leftStr.Value + rightStr.Value);
    
        // Conversión implícita int a float
        if (left is IntValue leftInt2 && right is FloatValue rightFloat2)
            return new FloatValue(leftInt2.Value + rightFloat2.Value);
        if (left is FloatValue leftFloat2 && right is IntValue rightInt2)
            return new FloatValue(leftFloat2.Value + rightInt2.Value);
    
        // Conversión automática para strings
        if (left is StringValue leftStr2)
            return new StringValue(leftStr2.Value + FormatValue(right));
        if (right is StringValue rightStr2)
            return new StringValue(FormatValue(left) + rightStr2.Value);
    
        throw new SemanticError($"No se puede realizar la operación '+=' con los tipos {left.GetType().Name} y {right.GetType().Name}", errorToken);
    }

    public static ValueWrapper PerformSubtraction(ValueWrapper left, ValueWrapper right, IToken errorToken)
    {
        // Resta de enteros
        if (left is IntValue leftInt && right is IntValue rightInt)
            return new IntValue(leftInt.Value - rightInt.Value);
    
        // Resta de flotantes
        if (left is FloatValue leftFloat && right is FloatValue rightFloat)
            return new FloatValue(leftFloat.Value - rightFloat.Value);
    
        // Conversión implícita int a float
        if (left is IntValue leftInt2 && right is FloatValue rightFloat2)
            return new FloatValue(leftInt2.Value - rightFloat2.Value);
        if (left is FloatValue leftFloat2 && right is IntValue rightInt2)
            return new FloatValue(leftFloat2.Value - rightInt2.Value);
    
        throw new SemanticError($"No se puede realizar la operación '-=' con los tipos {left.GetType().Name} y {right.GetType().Name}", errorToken);
    }

    //lista_valores
    public static List<ValueWrapper> ProcessListaValores(GolightParser.Lista_valoresContext context, SemanticVisitor visitor)
    {
        List<ValueWrapper> valores = new List<ValueWrapper>();

        if (context.lista_valores() != null)
        {
            valores.AddRange(ProcessListaValores(context.lista_valores(), visitor));
        }

        if (context.expression() != null)
        {
            ValueWrapper valor = visitor.Visit(context.expression());
            valores.Add(valor);
        }

        return valores;
    }

    public static List<GolightParser.CaseContext> GetAllCases(GolightParser.Lista_casesContext listaCases)
    {
        var result = new List<GolightParser.CaseContext>();
    
        // Base case: si listaCases es null, retornamos lista vacía
        if (listaCases == null)
            return result;
    
        // Añadir el caso actual si existe
        if (listaCases.@case() != null)
            result.Add(listaCases.@case());
    
        // Añadir recursivamente los casos de lista_cases anidados
        if (listaCases.lista_cases() != null)
            result.AddRange(GetAllCases(listaCases.lista_cases()));
    
        return result;
    }

    public static List<GolightParser.ExprContext> GetAllExpressions(GolightParser.Lista_expresionesContext context)
    {
        List<GolightParser.ExprContext> expressions = new List<GolightParser.ExprContext>();

        if (context.expr() != null)
        {
            expressions.Add(context.expr());
        }

        if (context.lista_expresiones() != null)
        {
            expressions.InsertRange(0, GetAllExpressions(context.lista_expresiones()));
        }

        return expressions;
    }

    public static bool VariableExists(string id, IToken errorToken, Environment currentEnvironment)
    {
        try
        {
            currentEnvironment.GetVariable(id, errorToken);
            return true;
        }
        catch (SemanticError)
        {
            return false;
        }
    }

    public static bool IsTypeMatch(ValueWrapper value, string expectedType)
    {
        if (value == null) return false;
        
        string actualType = GetTypeName(value);
        return actualType == expectedType;
    }

    public static string GetTypeName(ValueWrapper value)
    {
        if (value is IntValue) return "int";
        if (value is FloatValue) return "float64";
        if (value is StringValue) return "string";
        if (value is BoolValue) return "bool";
        if (value is RuneValue) return "rune";
        if (value is ArrayValue) return "slice";
        if (value is FunctionValue) return "function";
        if (value is NillValue) return "nil";
        if (value is StructValue structVal) return structVal.StructName;

        return "undefined";
    }
    
    public static bool FunctionExists(string functionName, IToken errorToken, Environment currentEnvironment)
    {
        try
        {
            ValueWrapper funcVal = currentEnvironment.GetVariable(functionName, errorToken);
            return funcVal is FunctionValue;
        }
        catch (SemanticError)
        {
            return false;
        }
    }

    public static bool AreTypesCompatible(string actualType, string expectedType)
    {
        if (actualType == expectedType)
            return true;

        // Casos especiales de compatibilidad
        if (expectedType == "float64" && actualType == "int")
            return true; // int puede convertirse a float64

        // nil puede asignarse a cualquier tipo como valor especial
        if (actualType == "nil")
            return true;

        // Para arrays, verificar tipo de elementos
        if (actualType.StartsWith("[]") && expectedType.StartsWith("[]"))
        {
            string actualElemType = actualType.Substring(2);
            string expectedElemType = expectedType.Substring(2);
            return AreTypesCompatible(actualElemType, expectedElemType);
        }

        return false;
    }

    public static ValueWrapper ValidateArgType(ValueWrapper arg, string expectedType, int paramIndex, Environment environment = null)
    {
        string actualType = GetTypeName(arg);

        Console.WriteLine($"Validando argumento {paramIndex}: tipo esperado '{expectedType}', tipo actual '{actualType}'");

        // Verificar si los tipos son compatibles
        if (!AreTypesCompatible(actualType, expectedType))
        {
            throw new SemanticError($"Se esperaba un argumento de tipo '{expectedType}' pero se recibió '{actualType}'", null);
        }

        if (expectedType == "int" || expectedType == "float64" || expectedType == "string" || 
            expectedType == "bool" || expectedType == "rune")
        {
            try
            {
                return ConvertToType(arg, expectedType);
            }
            catch (SemanticError)
            {
                throw new SemanticError($"No se puede convertir de '{actualType}' a '{expectedType}'", null);
            }
        }

        if (arg is StructValue structVal)
        {

            if (structVal.StructName != expectedType && expectedType != "any")
            {
                throw new SemanticError($"Se esperaba un struct de tipo '{expectedType}' pero se recibió '{actualType}'", null);
            }

            return arg;
        }

        if (actualType == "nil" && expectedType != "int" && expectedType != "float64" && 
            expectedType != "string" && expectedType != "bool" && expectedType != "rune")
        {
            return arg;
        }

        return arg;
    }

    public static bool AreStructsEqual(StructValue a, StructValue b)
    {
        // Verificar si son del mismo tipo
        if (a.StructName != b.StructName)
            return false;

        // Verificar si tienen los mismos campos
        if (!a.Fields.Keys.SequenceEqual(b.Fields.Keys))
            return false;

        // Comparar cada campo
        foreach (var key in a.Fields.Keys)
        {
            if (!AreValuesEqual(a.Fields[key], b.Fields[key]))
                return false;
        }

        return true;
    }
}