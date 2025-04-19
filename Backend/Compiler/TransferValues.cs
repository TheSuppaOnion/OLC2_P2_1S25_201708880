
public class BreakException : Exception
{
    public BreakException() : base("Break statement")
    {
    }
}

public class ContinueException : Exception
{
    public ContinueException() : base("Continue statement")
    {
    }
}

public class ReturnException : Exception
{
    public ValueWrapper Value { get; private set; }
    public Antlr4.Runtime.IToken Token { get; private set; }

    public ReturnException(ValueWrapper value, Antlr4.Runtime.IToken token = null) : base("Sentencia return")
    {
        Value = value;
        Token = token;
    }
}
