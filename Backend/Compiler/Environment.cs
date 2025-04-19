using Proyecto2;

public class Environment
{

    public Dictionary<string, ValueWrapper> variables = new Dictionary<string, ValueWrapper>();
    public Dictionary<string, StructTypeDefinition> structDefinitions = new Dictionary<string, StructTypeDefinition>();
    public Dictionary<string, Symbol> symbols = new Dictionary<string, Symbol>();
    private Environment? parent;
    private string scope;
    public List<Environment> Children { get; set; } = new List<Environment>();

    public Environment(Environment? parent, string scope = "Global")
    {
        this.parent = parent;
        this.scope = scope;
        if (parent != null)
        {
            parent.Children.Add(this);
        }
    }
    public Environment? Enclosing { get { return parent; } }
    public string Scope { get { return scope; } }

    public ValueWrapper GetVariable(string id, Antlr4.Runtime.IToken token)
    {
        if (variables.ContainsKey(id))
        {
            return variables[id];
        }

        if (parent != null)
        {
            return parent.GetVariable(id, token);
        }

        throw new SemanticError("Variable " + id + " no encontrada", token);
    }

    public void DeclareVariable(string id, ValueWrapper value, Antlr4.Runtime.IToken? token, bool esEmbebida = false)
    {
        if (variables.ContainsKey(id))
        {
            if (token != null) throw new SemanticError("Variable " + id + " ya declarada", token);
            else throw new SemanticError($"Variable '{id}' ya declarada en este ámbito", null);
        }
        else
        {
            variables[id] = value;
            string tipoDato = Utilities.GetTypeName(value);
        
            if (value is StructValue structVal)
                tipoDato = structVal.StructName;
            else if (value is ArrayValue arrayVal)
                tipoDato = "[]" + (arrayVal.ElementType ?? "any");
            
            symbols[id] = new Symbol(id, "Variable", tipoDato, scope, token, value) 
            { 
                EsFuncionEmbebida = esEmbebida 
            };
        }
    }

    public ValueWrapper AssignVariable(string id, ValueWrapper value, Antlr4.Runtime.IToken token)
    {
        if (variables.ContainsKey(id))
        {
            ValueWrapper currentValue = variables[id];
            string currentType = Utilities.GetTypeName(currentValue);
            string newValueType = Utilities.GetTypeName(value);

            if (!Utilities.AreTypesCompatible(newValueType, currentType))
            {
                throw new SemanticError($"No se puede asignar un valor de tipo '{newValueType}' a una variable de tipo '{currentType}'", token);
            }

            ValueWrapper convertedValue = Utilities.ConvertToType(value, currentType);
            variables[id] = convertedValue;
            return convertedValue;
        }

        if (parent != null)
        {
            return parent.AssignVariable(id, value, token);
        }

        throw new SemanticError("Variable " + id + " no encontrada", token);
    }

    public void DeclareStructType(string name, StructTypeDefinition definition)
    {
        if (structDefinitions.ContainsKey(name))
        {
            throw new SemanticError($"El tipo de struct '{name}' ya está definido", null);
        }

        structDefinitions[name] = definition;
    }
    public StructTypeDefinition GetStructDefinition(string name, Antlr4.Runtime.IToken errorToken)
    {
        if (structDefinitions.TryGetValue(name, out var definition))
        {
            return definition;
        }

        if (parent != null)
        {
            try
            {
                return parent.GetStructDefinition(name, errorToken);
            }
            catch (SemanticError)
            {
                // Seguir buscando
            }
        }

        throw new SemanticError($"Tipo de struct '{name}' no definido", errorToken);
    }

    public bool StructTypeExists(string name)
    {
        if (structDefinitions.ContainsKey(name))
        {
            return true;
        }

        return parent != null && parent.StructTypeExists(name);
    }

        public void RegisterFunction(string id, string returnType, Antlr4.Runtime.IToken token, bool isMethod = false)
    {
        string tipoSimbolo = isMethod ? "Método" : "Función";
        symbols[id] = new Symbol(id, tipoSimbolo, returnType ?? "void", scope, token);
    }

    public void RegisterParameter(string id, string typeName, Antlr4.Runtime.IToken token)
    {
        symbols[id] = new Symbol(id, "Parámetro", typeName, scope, token);
    }

        public void RegisterStruct(string id, Antlr4.Runtime.IToken token)
    {
        symbols[id] = new Symbol(id, "Struct", id, scope, token);
    }
    
    public List<Symbol> GetAllSymbols()
    {
        var result = new List<Symbol>();
    
        // Agregar los símbolos de este entorno
        foreach (var symbol in symbols.Values)
        {
            result.Add(symbol);
        }
        
        // Recorrer recursivamente los entornos hijos
        // Me llevo rato entender que eran lo hijos que tenia que recorrer no el padre...
        foreach (var child in Children)
        {
            var childSymbols = child.GetAllSymbols();
            foreach (var symbol in childSymbols)
            {
                // Evitar duplicados
                bool isDuplicate = result.Any(s => s.Id == symbol.Id && s.Ambito == symbol.Ambito);
                if (!isDuplicate)
                {
                    result.Add(symbol);
                }
            }
        }
            
            return result;
        }
    }