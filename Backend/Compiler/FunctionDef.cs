using analyzer;
using Antlr4.Runtime;

public class FunctionDefinition : Invocable
{
    public List<Parameter> Parameters { get; set; }
    public GolightParser.FuncionContext FunctionContext { get; private set; }
    public Environment DeclarationEnvironment { get; private set; }
    public string Name { get; private set; }
    public string ReturnType { get; set; }
    public Environment ParameterEnvironment { get; set; }

    public FunctionDefinition(GolightParser.FuncionContext context, Environment env, string name)
    {
        FunctionContext = context;
        DeclarationEnvironment = env;
        Name = name;
        Parameters = new List<Parameter>();

        //Tabla de simbolos
        IToken token = context.ID().Symbol;
        env.RegisterFunction(name, ReturnType ?? "nil", token);
    }

    public int Arity()
    {
        return Parameters.Count;
    }

    public ValueWrapper Invoke(List<ValueWrapper> args, CompilerVisitor visitor)
    {
        Environment functionEnv = new Environment(DeclarationEnvironment, Name);
        Environment previousEnv = visitor.currentEnvironment;
        visitor.currentEnvironment = functionEnv;

        if (DeclarationEnvironment == null)
        {
            throw new SemanticError($"Error interno: Environment es nulo para función '{Name}'", FunctionContext.Start);
        }

        try
        {
            Console.WriteLine($"Llamando a método {Name} con {args.Count} argumentos");

            if (args.Count != Parameters.Count)
            {
                throw new SemanticError($"Función {Name} espera {Parameters.Count} argumentos pero recibió {args.Count}", FunctionContext.Start);
            }

            for (int i = 0; i < Parameters.Count; i++)
            {
                try
                {
                    // Validar argumentos y declararlos en el environment de la función
                    ValueWrapper convertedArg = Utilities.ValidateArgType(args[i], Parameters[i].Type, i);
                    functionEnv.DeclareVariable(Parameters[i].Name, convertedArg, FunctionContext.Start);
                    //Tabla de simbolos
                    functionEnv.RegisterParameter(Parameters[i].Name, Parameters[i].Type, FunctionContext.Start);
                    Console.WriteLine($"  Parameter {i}: {Parameters[i].Name} = {Utilities.FormatValue(convertedArg)}");
                }
                catch (SemanticError ex)
                {
                    throw new SemanticError($"Error en parámetro {i + 1} '{Parameters[i].Name}': {ex.Message}", FunctionContext.Start);
                }
            }

            // Ejecutar cuerpo de la función
            foreach (var instruccion in FunctionContext.instruccion())
            {
                try
                {
                    visitor.Visit(instruccion);
                }
                catch (ReturnException returnEx)
                {
                    Console.WriteLine($"  Return caught: {Utilities.FormatValue(returnEx.Value)}");

                    if (ReturnType != null && returnEx.Value != null)
                    {
                        try
                        {
                            ValueWrapper convertedReturn = Utilities.ConvertToType(returnEx.Value, ReturnType);
                            return convertedReturn;
                        }
                        catch (SemanticError ex)
                        {
                            throw new SemanticError($"Error en retorno: {ex.Message}", returnEx.Token ?? FunctionContext.Start);
                        }
                    }

                    return returnEx.Value ?? visitor.defaultVoid;
                }
            }

            // Verificar si se requiere un valor de retorno
            if (ReturnType != null && ReturnType != "nil")
            {
                throw new SemanticError($"Función '{Name}' debe retornar un valor de tipo '{ReturnType}'", FunctionContext.Stop);
            }

            return visitor.defaultVoid;
        }
        finally
        {
            visitor.currentEnvironment = previousEnv;
        }
    }
}

public class Parameter
{
    public string Name { get; set; }
    public string Type { get; set; }
}