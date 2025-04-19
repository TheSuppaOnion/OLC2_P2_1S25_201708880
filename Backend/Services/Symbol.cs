using Antlr4.Runtime;

namespace Proyecto2
{
    public class Symbol
    {
        public string Id { get; set; }
        public string TipoSimbolo { get; set; } 
        public string TipoDato { get; set; }    
        public string Ambito { get; set; }      
        public int Linea { get; set; }
        public int Columna { get; set; }
        public ValueWrapper Valor { get; set; } 
        public bool EsFuncionEmbebida { get; set; } = false;

        public Symbol(string id, string tipoSimbolo, string tipoDato, string ambito, IToken token, ValueWrapper valor = null)
        {
            Id = id;
            TipoSimbolo = tipoSimbolo;
            TipoDato = tipoDato;
            Ambito = ambito;
            Linea = token?.Line ?? 0;
            Columna = token?.Column ?? 0;
            Valor = valor;
        }

        public override string ToString()
        {
            return $"{Id} | {TipoSimbolo} | {TipoDato} | {Ambito} | {Linea} | {Columna}";
        }
    }
}