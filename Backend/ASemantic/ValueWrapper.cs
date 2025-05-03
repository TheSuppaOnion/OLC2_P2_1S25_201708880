

public abstract record ValueWrapper;


public record IntValue(int Value) : ValueWrapper;
public record FloatValue(float Value) : ValueWrapper;
public record StringValue(string Value) : ValueWrapper;
public record BoolValue(bool Value) : ValueWrapper;
public record RuneValue(char Value) : ValueWrapper;

public record FunctionValue(Invocable invocable, string name) : ValueWrapper
{
    public readonly Invocable function = invocable;
    public readonly string name = name;

    public int Arity()
    {
        return invocable.Arity();
    }

    public ValueWrapper Call(List<ValueWrapper> arguments, SemanticVisitor visitor)
    {
        return function.Invoke(arguments, visitor);
        
    }

    public override string ToString()
    {
        return $"<función {name}>";
    }
    public string Name
    {
        get { return name; }
    }

    public Invocable Callable
    {
        get { return invocable; }
    }
}

// Clase para representar un slice
public record ArrayValue : ValueWrapper
{
    public List<ValueWrapper> Elements { get; set; }
    public string Name        { get; set;}
    public string ElementType { get; set; }
    public int    Count       { get; set;}
    public string Label       { get; set;} 

    
    public ArrayValue(List<ValueWrapper> elements, string elementType)
    {
        Elements = elements;
        ElementType = elementType;
    }
    public ArrayValue(string name, int count, string label)
    {
        Name        = name;
        Count       = count;
        Label       = label;
    }
    
    public override string ToString()
    {
        return $"[{string.Join(", ", Elements.Select(Utilities.FormatValue))}]";
    }
}

public record StructValue : ValueWrapper
{
    public string StructName { get; init; }
    public Dictionary<string, ValueWrapper> Fields { get; init; }

    public StructValue(string structName)
    {
        StructName = structName;
        Fields = new Dictionary<string, ValueWrapper>();
    }

    public bool HasField(string fieldName)
    {
        return Fields.ContainsKey(fieldName);
    }

    public ValueWrapper GetField(string fieldName)
    {
        if (Fields.TryGetValue(fieldName, out var value))
        {
            return value;
        }
        
        return new NillValue();
    }

    public void SetField(string fieldName, ValueWrapper value)
    {
        // Para permitir asignación de nil a campos anidados de tipo struct
        if (value is NillValue && fieldName == "Siguiente")
        {
            Fields[fieldName] = value;
            return;
        }

        // Si el valor es un struct y el campo espera el mismo tipo de struct
        if (value is StructValue sv && fieldName == "Siguiente" && sv.StructName == this.StructName)
        {
            Fields[fieldName] = value;  // Asignar struct del mismo tipo (para nodos enlazados)
            return;
        }

        // Asignación normal
        Fields[fieldName] = value;
    }

    public override string ToString()
    {
        return $"<struct {StructName}>";
    }
}

public class StructTypeDefinition
{
    public string Name { get; }
    public Dictionary<string, string> FieldTypes { get; }

    public StructTypeDefinition(string name)
    {
        Name = name;
        FieldTypes = new Dictionary<string, string>();
    }

    public void AddField(string fieldName, string fieldType)
    {
        FieldTypes[fieldName] = fieldType;
    }

    public bool HasField(string fieldName)
    {
        return FieldTypes.ContainsKey(fieldName);
    }

    public string GetFieldType(string fieldName)
    {
        if (FieldTypes.TryGetValue(fieldName, out var type))
        {
            return type;
        }
        
        return null;
    }
}


public record NillValue : ValueWrapper;
