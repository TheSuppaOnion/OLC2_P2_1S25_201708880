using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System;

namespace Proyecto2
{
    public static class ErrorReporter
    {
        // Endpoint para configurar la tabla de errores
        public static void ConfigureErrorTableEndpoint(WebApplication app, Func<List<string>> getErroresSintacticos, Func<List<string>> getErroresLexicos, Func<List<string>> getErroresSemanticos)
        {
            app.MapGet("/tablaerror", (HttpContext context) => {
                string erroresHTML;
                
                var erroresSintacticos = getErroresSintacticos();
                var erroresLexicos = getErroresLexicos();
                var erroresSemanticos = getErroresSemanticos();
                var todosLosErrores = new List<ErrorInfo>();
                
                // Procesar errores léxicos
                foreach (var error in erroresLexicos)
                {
                    var parts = error.Split(" - ");
                    var locationParts = parts[0].Replace("Línea ", "").Split(":");
                    
                    string linea = locationParts[0];
                    string columna = locationParts[1];
                    string mensajeCompleto = parts.Length > 1 ? parts[1] : "Error desconocido";
                    
                    // Simplificar el mensaje
                    string mensajeSimplificado = SimplificarMensajeError(mensajeCompleto);
                    
                    todosLosErrores.Add(new ErrorInfo {
                        Linea = linea,
                        Columna = columna,
                        Mensaje = mensajeSimplificado,
                        Tipo = "Léxico"
                    });
                }
                
                // Procesar errores sintácticos
                foreach (var error in erroresSintacticos)
                {
                    var parts = error.Split(" - ");
                    var locationParts = parts[0].Replace("Línea ", "").Split(":");
                    
                    string linea = locationParts[0];
                    string columna = locationParts[1];
                    string mensajeCompleto = parts.Length > 1 ? parts[1] : "Error desconocido";
                    
                    // Simplificar el mensaje
                    string mensajeSimplificado = SimplificarMensajeError(mensajeCompleto);
                    
                    string tipoError = "Sintáctico";
                    
                    // Verificar si es un error semántico
                    if (mensajeCompleto.Contains("semantic error") || 
                        mensajeCompleto.Contains("undefined variable") || 
                        mensajeCompleto.Contains("type mismatch"))
                    {
                        tipoError = "Semántico";
                    }
                    
                    todosLosErrores.Add(new ErrorInfo {
                        Linea = linea,
                        Columna = columna,
                        Mensaje = mensajeSimplificado,
                        Tipo = tipoError
                    });
                }
                // Procesar errores semánticos
                foreach (var error in erroresSemanticos)
                {
                    // Extraer información del mensaje de error
                    var errorText = error.Replace("Error semántico: ", "");
                    
                    // Buscar el patrón "en línea X, columna Y" en el mensaje
                    int lineaIndex = errorText.IndexOf("en línea ");
                    int columnaIndex = errorText.IndexOf(", columna ");
                    
                    if (lineaIndex >= 0 && columnaIndex >= 0)
                    {
                        string linea = errorText.Substring(lineaIndex + 9, columnaIndex - (lineaIndex + 9));
                        string columna = errorText.Substring(columnaIndex + 10);
                        string mensaje = errorText.Substring(0, lineaIndex).Trim();
                        
                        todosLosErrores.Add(new ErrorInfo {
                            Linea = linea,
                            Columna = columna,
                            Mensaje = mensaje,
                            Tipo = "Semántico"
                        });
                    }
                    else
                    {
                        // Si no se encuentra el patrón, agregar el error completo
                        todosLosErrores.Add(new ErrorInfo {
                            Linea = "0",
                            Columna = "0",
                            Mensaje = errorText,
                            Tipo = "Semántico"
                        });
                    }
                }
                
                if (todosLosErrores.Count > 0)
                {
                    var tablaErrores = new StringBuilder();
                    tablaErrores.AppendLine("<table>");
                    tablaErrores.AppendLine("    <thead>");
                    tablaErrores.AppendLine("        <tr>");
                    tablaErrores.AppendLine("            <th>No.</th>");
                    tablaErrores.AppendLine("            <th>Descripción</th>");
                    tablaErrores.AppendLine("            <th>Línea</th>");
                    tablaErrores.AppendLine("            <th>Columna</th>");
                    tablaErrores.AppendLine("            <th>Tipo</th>");
                    tablaErrores.AppendLine("        </tr>");
                    tablaErrores.AppendLine("    </thead>");
                    tablaErrores.AppendLine("    <tbody>");

                    for (int i = 0; i < todosLosErrores.Count; i++)
                    {
                        var error = todosLosErrores[i];
                        tablaErrores.AppendLine("        <tr>");
                        tablaErrores.AppendLine($"            <td>{i + 1}</td>");
                        tablaErrores.AppendLine($"            <td>{error.Mensaje}</td>");
                        tablaErrores.AppendLine($"            <td>{error.Linea}</td>");
                        tablaErrores.AppendLine($"            <td>{error.Columna}</td>");
                        tablaErrores.AppendLine($"            <td>{error.Tipo}</td>");
                        tablaErrores.AppendLine("        </tr>");
                    }

                    tablaErrores.AppendLine("    </tbody>");
                    tablaErrores.AppendLine("</table>");
                    
                    erroresHTML = tablaErrores.ToString();
                }
                else
                {
                    erroresHTML = "<p style=\"text-align: center; padding: 20px; color: #4CAF50; font-weight: bold;\">No se encontraron errores en el último análisis.</p>";
                }
                
                string html = GenerateErrorTableHtml(erroresHTML);
                
                return Results.Content(html, "text/html; charset=utf-8");
            });
        }
        
        private static string SimplificarMensajeError(string mensajeCompleto)
        {
            if (mensajeCompleto.Contains("expecting"))
            {
                return mensajeCompleto.Substring(0, mensajeCompleto.IndexOf("expecting")).Trim();
            }
            return mensajeCompleto;
        }
        
        // Método para generar el HTML de la tabla de errores
        private static string GenerateErrorTableHtml(string erroresHTML)
        {
            return $@"<!DOCTYPE html>
            <html lang=""es"">
            <head>
                <meta charset=""UTF-8"">
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                <title>Tabla de Errores</title>
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
                        padding: 10px 0;
                        text-align: center;
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
                    .container {{
                        width: 80%;
                        margin: 20px auto;
                        background-color: white;
                        padding: 20px;
                        box-shadow: 0 0 10px rgba(0, 0, 0, 0.1);
                        margin-bottom: 60px; /* Espacio para que el footer no tape contenido */
                    }}
                    table {{
                        border-collapse: collapse;
                        width: 100%;
                    }}
                    th, td {{
                        border: 1px solid #ddd;
                        padding: 8px;
                        text-align: left;
                    }}
                    th {{
                        background-color: #FF6347;
                        color: white;
                    }}
                    tr:nth-child(even) {{
                        background-color: #f2f2f2;
                    }}
                    tr:hover {{
                        background-color: #ddd;
                    }}
                </style>
            </head>
            <body>
                 <header>
                    <h1>Tabla de Errores</h1>
                </header>
                <div class=""container"">
                {erroresHTML}
                </div>
                <footer>
                    <p>Generado por el compilador</p>
                </footer>
            </body>
            </html>";
        }
    }

    // Clase para los datos de error
    public class ErrorInfo
    {
        public string? Linea { get; set; }
        public string? Columna { get; set; }
        public string? Mensaje { get; set; }
        public string? Tipo { get; set; }
    }
}