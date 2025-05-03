using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using analyzer;
using Proyecto_2.Services;
using Proyecto2;

namespace Proyecto_2.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class Compile : ControllerBase
    {
        private readonly ILogger<Compile> _logger;
        
        // Propiedades para almacenar errores y árbol de análisis
        private static List<string> erroresSintacticos = new List<string>();
        private static List<string> erroresLexicos = new List<string>();
        private static List<string> erroresSemanticos = new List<string>();
        private static List<Symbol> simbolos = new List<Symbol>();
        private static GolightParser.ProgramContext lastTree = null;
        private static string outputEjecucion = "";

        public Compile(ILogger<Compile> logger)
        {
            _logger = logger;
        }

        // Modelo para la solicitud
        public class CompileRequest
        {
            public string Code { get; set; }
        }

        // Endpoint para compilar código
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CompileRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Code) |!ModelState.IsValid)
            {
                return BadRequest("El código no puede estar vacío");
            }

            // Limpiar los errores anteriores
            erroresSintacticos.Clear();
            erroresLexicos.Clear();
            erroresSemanticos.Clear();
            simbolos.Clear();
            outputEjecucion = "";

            var resultado = new StringBuilder();
            
            try {
                ICharStream stream = CharStreams.fromString(request.Code);
                GolightLexer lexer = new GolightLexer(stream);
                CommonTokenStream tokens = new CommonTokenStream(lexer);
                GolightParser parser = new GolightParser(tokens);
                
                // Configurar listener para errores léxicos
                var lexicoErrorListener = new LexicoErrorListener();
                lexer.RemoveErrorListeners();
                lexer.AddErrorListener(lexicoErrorListener);

                // Análisis léxico
                //resultado.AppendLine("=== ANÁLISIS LÉXICO ===");
                lexer.Reset();
                IToken token;
                while ((token = lexer.NextToken()).Type != GolightLexer.Eof)
                {
                    string tokenName = lexer.Vocabulary.GetSymbolicName(token.Type);
                    //resultado.AppendLine($"Línea {token.Line}:{token.Column} - Token: {tokenName}, Texto: '{token.Text}'");
                }

                if (lexicoErrorListener.HasErrors)
                {
                    resultado.AppendLine("Se encontraron errores! Revise reportes para mas detalles");
                    //Solo para debuggear!!
                    /*resultado.AppendLine("\nErrores léxicos encontrados:");
                    foreach (var error in lexicoErrorListener.Errors)
                    {
                        resultado.AppendLine($"- {error}");
                    }*/
                    erroresLexicos.AddRange(lexicoErrorListener.Errors);
                    return Ok(resultado.ToString());
                }

                lexer.Reset();
                tokens = new CommonTokenStream(lexer);

                // Configurar el manejo de errores sintácticos
                var errorListener = new ErrorListener();
                parser.RemoveErrorListeners();
                parser.AddErrorListener(errorListener);

                //Solo para debuggear!!
                // Realizar el análisis sintáctico
                //resultado.AppendLine("\n=== ANÁLISIS SINTÁCTICO ===");
                GolightParser.ProgramContext tree = parser.program();

                if (errorListener.HasErrors) {
                    erroresSintacticos.AddRange(errorListener.Errors);
                    resultado.AppendLine("Se encontraron errores! Revise reportes para mas detalles");
                    //Solo para debuggear!!
                    /*resultado.AppendLine("Se encontraron errores sintácticos:");
                    foreach (var error in errorListener.Errors) {
                        resultado.AppendLine($"- {error}");
                    }
                    lastTree = null;*/
                    return Ok(resultado.ToString());
                } else {
                    //Descomentar para revisar el flujo del programa o para debuggear!
                    /*resultado.AppendLine("Análisis sintáctico completado exitosamente.");
                    resultado.AppendLine($"El árbol de análisis sintáctico contiene {GetNodeCount(tree)} nodos.");*/
                    lastTree = tree;

                    /*// EJECUCIÓN SEMÁNTICA CON EL VISITOR
                    resultado.AppendLine("\n=== EJECUCIÓN ===");*/
                    
                    try {
                        // Crear y ejecutar el visitor
                        var semantico = new SemanticVisitor();
                        semantico.Visit(tree);
                        
                        // Recolectar símbolos y guardarlos para la tabla de símbolos
                        simbolos = semantico.currentEnvironment.GetAllSymbols();
                        //outputEjecucion = semantico.output;

                        //Generacion de Assembler ARM64
                        var compiler = new CompilerVisitor(semantico.currentEnvironment);
                        compiler.Visit(tree);
                        outputEjecucion = compiler.c.ToString();
                        //Solo para debuggear!!
                        //resultado.AppendLine("//Compilación completada exitosamente.");
                        resultado.AppendLine(outputEjecucion);
                    } 
                    catch (SemanticError se) {
                        string errorMsg = $"Error semántico: {se.Message}";
                        erroresSemanticos.Add(errorMsg);
                        resultado.AppendLine("Se encontraron errores! Revise reportes para mas detalles");
                        //Solo para debuggear!!
                        /*resultado.AppendLine("Se encontraron errores semánticos:");
                        resultado.AppendLine($"- {errorMsg}");*/
                    }
                    catch (Exception ex) {
                        // Capturar otros errores durante la ejecución
                        string errorMsg = $"Error de ejecución: {ex.Message}";
                        erroresSemanticos.Add(errorMsg);
                        resultado.AppendLine("Se encontraron errores durante la ejecución:");
                        resultado.AppendLine($"- {errorMsg}");
                        
                        _logger.LogError(ex, "Error durante la ejecución del visitor");
                    }
                }

                return Ok(resultado.ToString());

            } catch (Exception ex) {
                _logger.LogError(ex, "Error al procesar la entrada");
                resultado.AppendLine($"ERROR INTERNO: {ex.Message}");
                resultado.AppendLine(ex.StackTrace);
                return StatusCode(500, resultado.ToString());
            }
        }

        // Endpoint para obtener el árbol de análisis sintáctico
        [HttpGet("ast")]
        public IActionResult GetAst()
        {
            return Ok("Este endpoint será atendido por AstVisualizer");
        }

        // Endpoint para obtener la tabla de errores
        [HttpGet("errors")]
        public IActionResult GetErrors()
        {
            return Ok("Este endpoint será atendido por ErrorReporter");
        }

        //Descomentar para debuggear!!
        /*// Método auxiliar para contar nodos en el árbol
        private static int GetNodeCount(IParseTree tree) {
            if (tree == null) return 0;
            int count = 1; // Contar el nodo actual
            for (int i = 0; i < tree.ChildCount; i++) {
                count += GetNodeCount(tree.GetChild(i));
            }
            return count;
        }*/
    
        public static List<string> GetErroresSintacticos()
        {
            return erroresSintacticos;
        }

        public static List<string> GetErroresLexicos()
        {
            return erroresLexicos;
        }

        public static List<string> GetErroresSemanticos()
        {
            return erroresSemanticos;
        }

        public static List<Symbol> GetSymbolos()
        {
            return simbolos;
        }

        public static GolightParser.ProgramContext GetLastTree()
        {
            return lastTree;
        }
    }
}