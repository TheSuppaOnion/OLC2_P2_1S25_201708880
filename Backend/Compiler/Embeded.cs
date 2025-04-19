
public class Embeded
{
    public static void Generate(Environment env)
    {
        env.DeclareVariable("strconv.Atoi", new FunctionValue(new StrconvAtoiEmbeded(), "strconv.Atoi"), null, true);
        env.DeclareVariable("strconv.ParseFloat", new FunctionValue(new StrconvParseFloatEmbeded(), "strconv.ParseFloat"), null, true);
        env.DeclareVariable("reflect.TypeOf", new FunctionValue(new ReflectTypeofEmbeded(), "reflect.TypeOf"), null, true);
        env.DeclareVariable("slices.Index", new FunctionValue(new SlicesIndexEmbeded(), "slices.Index"), null, true);
        env.DeclareVariable("strings.Join", new FunctionValue(new StringsJoinEmbeded(), "strings.Join"), null, true);
        env.DeclareVariable("len", new FunctionValue(new LenEmbeded(), "len"), null, true);
        env.DeclareVariable("append", new FunctionValue(new AppendEmbeded(), "append"), null, true);
    }
}

public class StrconvAtoiEmbeded : Invocable
{
    public int Arity()
    {
        return 1;
    }

    public ValueWrapper Invoke(List<ValueWrapper> args, CompilerVisitor visitor)
    {
        if (args.Count != 1)
            throw new SemanticError($"strconv.Atoi espera 1 argumento y obtuvo {args.Count}", null);
            
        if (args[0] is StringValue strVal)
        {
            string value = strVal.Value.Trim();
            
            if (value.StartsWith("-"))
                value = value.Substring(1);
                
            // Verificar que todos los caracteres sean dígitos
            foreach (char c in value)
            {
                if (!char.IsDigit(c))
                {
                    throw new SemanticError($"strconv.Atoi: no se puede convertir '{strVal.Value}' a un valor entero", null);
                }
            }
            
            // Intentar la conversión
            if (int.TryParse(strVal.Value, out int result))
                return new IntValue(result);
            else
                throw new SemanticError($"strconv.Atoi: no se puede convertir '{strVal.Value}' a un valor entero", null);
        }
        
        throw new SemanticError("strconv.Atoi requiere argumento string", null);
    }
}

public class StrconvParseFloatEmbeded : Invocable
{
    public int Arity()
    {
        return 1;
    }

    public ValueWrapper Invoke(List<ValueWrapper> args, CompilerVisitor visitor)
    {
        if (args.Count != 1)
            throw new SemanticError($"strconv.ParseFloat espera 1 argumento, obtuvo {args.Count}", null);
            
        if (args[0] is StringValue strVal)
        {
            if (float.TryParse(strVal.Value, out float result))
                return new FloatValue(result);
            else
                throw new SemanticError($"strconv.ParseFloat: no se puede convertir '{strVal.Value}' a un valor float64", null);
        }
        
        throw new SemanticError("strconv.ParseFloat requiere un argumento string", null);
    }
}

public class ReflectTypeofEmbeded : Invocable
{
    public int Arity()
    {
        return 1;
    }

    public ValueWrapper Invoke(List<ValueWrapper> args, CompilerVisitor visitor)
    {
        if (args.Count != 1)
            throw new SemanticError($"reflect.TypeOf espera 1 argumento, obtuvo {args.Count}", null);
            
        ValueWrapper arg = args[0];
        string type = arg switch
        {
            IntValue _ => "int",
            FloatValue _ => "float64",
            StringValue _ => "string",
            BoolValue _ => "bool",
            RuneValue _ => "rune",
            NillValue _ => "nil",
            ArrayValue arr => $"[]{ GetArrayElementType(arr) }",
            _ => "undefined"
        };
        
        return new StringValue(type);
    }

    // Método para determinar el tipo de elementos de un slice
    private string GetArrayElementType(ArrayValue array)
    {
        if (!string.IsNullOrEmpty(array.ElementType))
            return array.ElementType;
            
        if (array.Elements.Count > 0)
        {
            var firstElement = array.Elements[0];
            return firstElement switch
            {
                IntValue _ => "int",
                FloatValue _ => "float64",
                StringValue _ => "string",
                BoolValue _ => "bool",
                RuneValue _ => "rune",
                ArrayValue _ => "[]", 
                _ => "any"
            };
        }
        
        return "any"; 
    }
}

public class SlicesIndexEmbeded : Invocable
{
    public int Arity()
    {
        return 2;
    }

    public ValueWrapper Invoke(List<ValueWrapper> args, CompilerVisitor visitor)
    {
        if (args.Count != 2)
            throw new SemanticError($"slices.Index espera 2 argumentos, se obtuvo {args.Count}", null);
        
        if (args[0] is ArrayValue arrayVal)
        {
            ValueWrapper searchValue = args[1];
        
            // Buscar el elemento en el slice
            for (int i = 0; i < arrayVal.Elements.Count; i++)
            {
                if (Utilities.AreValuesEqual(arrayVal.Elements[i], searchValue))
                {
                    return new IntValue(i);
                }
            }
        
            // Delvovemos -1 si no hay
            return new IntValue(-1);
        }
    
        throw new SemanticError("Primer argumento para slices.Index debe ser un slice", null);
    }
}

public class StringsJoinEmbeded : Invocable
{
    public int Arity()
    {
        return 2;
    }

    public ValueWrapper Invoke(List<ValueWrapper> args, CompilerVisitor visitor)
    {
        if (args.Count != 2)
            throw new SemanticError($"strings.Join espera 2 argumentos, se obtuvo {args.Count}", null);
        
        if (args[0] is ArrayValue arrayVal && args[1] is StringValue separator)
        {
            List<string> stringValues = new List<string>();
        
            // Convertir cada elemento del array a string
            foreach (var element in arrayVal.Elements)
            {
                if (element is StringValue strVal)
                {
                    stringValues.Add(strVal.Value);
                }
                else
                {
                    stringValues.Add(Utilities.FormatValue(element));
                }
            }
        
            string result = string.Join(separator.Value, stringValues);
            return new StringValue(result);
        }
    
        throw new SemanticError("strings.Join espara tener un slice de strings y un separador tipo string", null);
    }
}

public class LenEmbeded : Invocable
{
    public int Arity()
    {
        return 1;
    }

    public ValueWrapper Invoke(List<ValueWrapper> args, CompilerVisitor visitor)
    {
        if (args.Count != 1)
            throw new SemanticError($"len espera 1 argumento, se obtuvo {args.Count}", null);
            
        if (args[0] is StringValue strVal)
        {
            return new IntValue(strVal.Value.Length);
        }
        else if (args[0] is ArrayValue arrayVal)
        {
            return new IntValue(arrayVal.Elements.Count);
        }
        
        throw new SemanticError($"len soporta strings y slices solamente, se obtuvo {args[0].GetType().Name}", null);
    }
}

public class AppendEmbeded : Invocable
{
    public int Arity()
    {
        return 2; // Acepta al menos 2 argumentos (el slicey el elemento a añadir)
    }

    public ValueWrapper Invoke(List<ValueWrapper> args, CompilerVisitor visitor)
    {
        if (args.Count < 2)
            throw new SemanticError($"Append espera 2 argumentos, se obtuvo {args.Count}", null);
            
        if (args[0] is ArrayValue arrayVal)
        {
            // Crear una nueva lista con todos los elementos del array original
            List<ValueWrapper> newElements = new List<ValueWrapper>(arrayVal.Elements);
            
            if (args[1] is ArrayValue secondArray)
            {
                newElements.AddRange(secondArray.Elements);
            }
            else
            {
                newElements.Add(args[1]);
            }
            
            return new ArrayValue(newElements, arrayVal.ElementType);
        }
        
        throw new SemanticError("Primer argumento debe de ser un slice", null);
    }
}
