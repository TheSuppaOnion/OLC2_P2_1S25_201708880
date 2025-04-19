using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Web;
using System.Diagnostics;
using Antlr4.Runtime.Tree;

namespace Proyecto2
{
    public static class AstVisualizer
    {
        // Configurar el endpoint para visualizar el AST
        public static void ConfigureAstEndpoint(WebApplication app, Func<IParseTree> getLastTree)
        {
            app.MapGet("/ast", async (HttpContext context) => {
                var tree = getLastTree();
                
                if (tree == null)
                {
                    // Si no hay árbol disponible, mostrar mensaje informativo
                    string noTreeHtml = GenerateNoTreeHtml();
                    return Results.Content(noTreeHtml, "text/html; charset=utf-8");
                }

                try {
                    string dotCode = GenerateDotCode(tree);
                    
                    string svgContent = await GenerateSvgFromDotAsync(dotCode);
                    
                    string html = GenerateAstHtml(svgContent);

                    return Results.Content(html, "text/html; charset=utf-8");
                }
                catch (Exception ex) {
                    return Results.Content(GenerateErrorHtml(ex), "text/html; charset=utf-8");
                }
            });
        }
        
        // Generar HTML para mensaje de "no hay árbol"
        private static string GenerateNoTreeHtml()
        {
            return @"<!DOCTYPE html>
            <html lang=""es"">
            <head>
                <meta charset=""UTF-8"">
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                <title>Árbol de Análisis Sintáctico</title>
                <style>
                    body {
                        font-family: Arial, sans-serif;
                        margin: 0;
                        padding: 0;
                        background-color: #f4f4f9;
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        height: 100vh;
                    }
                    .message {
                        background-color: #fff;
                        padding: 20px;
                        border-radius: 5px;
                        box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
                        text-align: center;
                    }
                    h1 {
                        color: #FF6347;
                    }
                </style>
            </head>
            <body>
                <div class=""message"">
                    <h1>No hay árbol disponible</h1>
                    <p>Debe ejecutar un análisis sintáctico exitoso antes de visualizar el árbol.</p>
                </div>
            </body>
            </html>";
        }
        
        // Generar HTML para mostrar el árbol
        private static string GenerateAstHtml(string svgContent)
        {
            return $@"<!DOCTYPE html>
            <html lang=""es"">
            <head>
                <meta charset=""UTF-8"">
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                <title>Árbol de Análisis Sintáctico</title>
                <style>
                    body {{
                        font-family: Arial, sans-serif;
                        margin: 0;
                        padding: 0;
                        background-color: #f4f4f9;
                    }}
                    header {{
                        background-color: #FF6347;
                        color: white;
                        padding: 10px;
                        text-align: center;
                    }}
                    .container {{
                        width: 95%;
                        margin: 20px auto;
                        background-color: white;
                        padding: 20px;
                        box-shadow: 0 0 10px rgba(0, 0, 0, 0.1);
                        overflow: auto;
                    }}
                    .svg-container {{
                        width: 100%;
                        overflow: auto;
                    }}
                    svg {{
                        display: block;
                        margin: 0 auto;
                        min-width: 800px;
                    }}
                    footer {{
                        background-color: #FF6347;
                        color: white;
                        text-align: center;
                        padding: 10px 0;
                        position: fixed;
                        width: 100%;
                        bottom: 0;
                    }}
                </style>
            </head>
            <body>
                <header>
                    <h1>Árbol de Análisis Sintáctico</h1>
                </header>
                <div class=""container"">
                    <div class=""svg-container"">
                        {svgContent}
                    </div>
                </div>
                <footer>
                    <p>Generado por el compilador - Visualizado con Graphviz</p>
                </footer>
            </body>
            </html>";
        }
        
        // Generar HTML para mostrar errores
        private static string GenerateErrorHtml(Exception ex)
        {
            return $@"<!DOCTYPE html>
            <html>
            <head>
                <title>Error al generar árbol</title>
                <style>
                    body {{
                        font-family: Arial, sans-serif;
                        margin: 20px;
                        line-height: 1.6;
                    }}
                    h1 {{
                        color: #FF6347;
                    }}
                    .error {{
                        background-color: #ffe6e6;
                        padding: 15px;
                        border-radius: 5px;
                        margin-top: 20px;
                    }}
                    .stack {{
                        background-color: #f5f5f5;
                        padding: 15px;
                        border-radius: 5px;
                        margin-top: 20px;
                        font-family: monospace;
                        white-space: pre-wrap;
                    }}
                </style>
            </head>
            <body>
                <h1>Error al generar el árbol de análisis</h1>
                <div class=""error"">
                    <strong>Mensaje:</strong> {HttpUtility.HtmlEncode(ex.Message)}
                </div>
                <div class=""stack"">
                    <strong>Stack trace:</strong>
                    {HttpUtility.HtmlEncode(ex.StackTrace)}
                </div>
            </body>
            </html>";
        }

        private static string GenerateDotCode(IParseTree tree)
        {
            StringBuilder dot = new StringBuilder();
            dot.AppendLine("digraph AST {");
            dot.AppendLine("  node [shape=box, style=filled, fillcolor=lightskyblue];");
            dot.AppendLine("  edge [arrowhead=none];");
            
            int nodeId = 0;
            Dictionary<IParseTree, int> nodeIds = new Dictionary<IParseTree, int>();
            
            void ProcessNode(IParseTree node)
            {
                if (node == null) return;
                
                int currentId = nodeId++;
                nodeIds[node] = currentId;
                
                string nodeType = node.GetType().Name.Replace("Context", "");
                string nodeText = node.GetText();
                
                if (nodeText.Length > 20)
                {
                    nodeText = nodeText.Substring(0, 17) + "...";
                }
                
                nodeText = EscapeForDot(nodeText);
                
                dot.AppendLine($"  node{currentId} [label=\"{nodeType}\\n{nodeText}\"];");
                
                for (int i = 0; i < node.ChildCount; i++)
                {
                    var child = node.GetChild(i);
                    ProcessNode(child);
                    
                    if (nodeIds.ContainsKey(child))
                    {
                        dot.AppendLine($"  node{currentId} -> node{nodeIds[child]};");
                    }
                }
            }
            
            ProcessNode(tree);
            dot.AppendLine("}");
            
            return dot.ToString();
        }

        private static string EscapeForDot(string text)
        {
            text = text.Replace("\\", "\\\\")   
                  .Replace("\"", "\\\"")     
                  .Replace("\r", "\\r")     
                  .Replace("\n", "\\n")      
                  .Replace("\t", "\\t");    
            
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[^\x20-\x7E]", "?");
            
            return text;
        }
        
        private static async Task<string> GenerateSvgFromDotAsync(string dotCode)
        {
            string tempDotFile = Path.GetTempFileName();
            string tempSvgFile = Path.GetTempFileName();
            
            try
            {
                await File.WriteAllTextAsync(tempDotFile, dotCode);
                
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "dot",
                    Arguments = $"-Tsvg -o \"{tempSvgFile}\" \"{tempDotFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        throw new Exception("No se pudo iniciar el proceso de Graphviz. Asegúrate de que Graphviz esté instalado.");
                    }
                    
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"Error al ejecutar Graphviz: {error}");
                    }
                    
                    // Leer el SVG generado
                    return await File.ReadAllTextAsync(tempSvgFile);
                }
            }
            finally
            {
                try { File.Delete(tempDotFile); } catch { }
                try { File.Delete(tempSvgFile); } catch { }
            }
        }
    }
}