using System.Collections.Generic;
using Antlr4.Runtime;

namespace Proyecto_2.Services
{
    public class ErrorListener : Antlr4.Runtime.BaseErrorListener
    {
        public List<string> Errors { get; } = new List<string>();
        public bool HasErrors => Errors.Count > 0;

        public override void SyntaxError(
            System.IO.TextWriter output, 
            IRecognizer recognizer, 
            Antlr4.Runtime.IToken offendingSymbol, 
            int line, 
            int charPositionInLine, 
            string msg, 
            Antlr4.Runtime.RecognitionException e)
        {
            Errors.Add($"Línea {line}:{charPositionInLine} - {msg}");
        }
    }

    public class LexicoErrorListener : Antlr4.Runtime.IAntlrErrorListener<int>
    {
        public List<string> Errors { get; } = new List<string>();
        public bool HasErrors => Errors.Count > 0;

        public void SyntaxError(
            System.IO.TextWriter output, 
            IRecognizer recognizer, 
            int offendingSymbol, 
            int line, 
            int charPositionInLine, 
            string msg, 
            Antlr4.Runtime.RecognitionException e)
        {
            Errors.Add($"Línea {line}:{charPositionInLine} - {msg}");
        }
    }
}