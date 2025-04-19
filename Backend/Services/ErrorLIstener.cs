using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

public class SemanticError : Exception
{

    private string message;
    private Antlr4.Runtime.IToken token;

    public SemanticError(string message, Antlr4.Runtime.IToken token)
    {
        this.message = message;
        this.token = token;
    }

    public override string Message
    {
        get
        {
            if (token != null)
            {
                return $"{message} en línea {token.Line}, columna {token.Column}";
            }
            else
            {
                return message;
            }
        }
    }
}