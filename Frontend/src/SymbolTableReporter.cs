using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Proyecto2
{
    public static class SymbolTableReporter
    {
        // Endpoint para configurar la tabla de símbolos
        public static void ConfigureSymbolTableEndpoint(WebApplication app, Func<List<Symbol>> getSymbols)
        {
            app.MapGet("/tablasimbolos", (HttpContext context) => {
                var symbols = getSymbols();
                string symbolsHTML;
                
                if (symbols != null && symbols.Count > 0)
                {
                    // Ordenar símbolos: primero por ámbito (Global primero), luego por tipo de símbolo
                    var sortedSymbols = symbols
                        .OrderBy(s => s.Ambito == "Global" ? 0 : 1)
                        .ThenBy(s => s.Ambito)
                        .ThenBy(s => GetSymbolTypeOrder(s.TipoSimbolo))
                        .ThenBy(s => s.Id)
                        .ToList();
                    
                    var tablaSimbolos = new StringBuilder();
                    tablaSimbolos.AppendLine("<table>");
                    tablaSimbolos.AppendLine("    <thead>");
                    tablaSimbolos.AppendLine("        <tr>");
                    tablaSimbolos.AppendLine("            <th>No.</th>");
                    tablaSimbolos.AppendLine("            <th>ID</th>");
                    tablaSimbolos.AppendLine("            <th>Tipo Símbolo</th>");
                    tablaSimbolos.AppendLine("            <th>Tipo Dato</th>");
                    tablaSimbolos.AppendLine("            <th>Ámbito</th>");
                    tablaSimbolos.AppendLine("            <th>Línea</th>");
                    tablaSimbolos.AppendLine("            <th>Columna</th>");
                    tablaSimbolos.AppendLine("        </tr>");
                    tablaSimbolos.AppendLine("    </thead>");
                    tablaSimbolos.AppendLine("    <tbody>");
                    
                    for (int i = 0; i < sortedSymbols.Count; i++)
                    {
                        var symbol = sortedSymbols[i];
                        // Determinar si es una función embebida (las embebidas tienen línea y columna 0)
                        bool esEmbebida = symbol.EsFuncionEmbebida || 
                        (symbol.Linea == 0 && symbol.Columna == 0 && 
                        (symbol.TipoDato == "function" || symbol.Id.Contains(".")));

                        string rowClass = esEmbebida ? "funcion-embebida" : "";
                        tablaSimbolos.AppendLine($"        <tr class=\"{rowClass}\">");
                        tablaSimbolos.AppendLine($"            <td>{i + 1}</td>");
                        tablaSimbolos.AppendLine($"            <td>{symbol.Id}</td>");
                        tablaSimbolos.AppendLine($"            <td>{symbol.TipoSimbolo}</td>");
                        tablaSimbolos.AppendLine($"            <td>{symbol.TipoDato}</td>");
                        tablaSimbolos.AppendLine($"            <td>{symbol.Ambito}</td>");
                        tablaSimbolos.AppendLine($"            <td>{symbol.Linea}</td>");
                        tablaSimbolos.AppendLine($"            <td>{symbol.Columna}</td>");
                        tablaSimbolos.AppendLine("        </tr>");
                    }
                    
                    tablaSimbolos.AppendLine("    </tbody>");
                    tablaSimbolos.AppendLine("</table>");
                    
                    symbolsHTML = tablaSimbolos.ToString();
                }
                else
                {
                    symbolsHTML = "<p style=\"text-align: center; padding: 20px; color: #4CAF50; font-weight: bold;\">No hay símbolos para mostrar.</p>";
                }
                
                string html = GenerateSymbolTableHtml(symbolsHTML);
                return Results.Content(html, "text/html; charset=utf-8");
            });
        }
        
        private static int GetSymbolTypeOrder(string symbolType)
        {
            switch (symbolType)
            {
                case "Función": return 0;
                case "Método": return 1;
                case "Variable": return 2;
                default: return 3;
            }
        }
        
        // Método para generar el HTML de la tabla de símbolos
        private static string GenerateSymbolTableHtml(string symbolsHTML)
        {
            return $@"<!DOCTYPE html>
            <html lang=""es"">
            <head>
                <meta charset=""UTF-8"">
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                <title>Tabla de Símbolos</title>
                <style>
                    body {{
                        font-family: Arial, sans-serif;
                        margin: 0;
                        padding: 0;
                        background-color: #f4f4f9;
                    }}
                    header {{
                        background-color: #4CAF50;
                        color: white;
                        padding: 10px 0;
                        text-align: center;
                    }}
                    footer {{
                        background-color: #4CAF50;
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
                        margin-bottom: 60px;
                        overflow-x: auto;
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
                        background-color: #4CAF50;
                        color: white;
                        position: sticky;
                        top: 0;
                    }}
                    tr:nth-child(even) {{
                        background-color: #f2f2f2;
                    }}
                    tr:hover {{
                        background-color: #ddd;
                    }}
                    .filter-container {{
                        margin-bottom: 15px;
                        padding: 10px;
                        background-color: #f8f9fa;
                        border-radius: 5px;
                    }}
                    select, input {{
                        padding: 5px;
                        margin-right: 10px;
                    }}
                    button {{
                        padding: 5px 10px;
                        background-color: #4CAF50;
                        color: white;
                        border: none;
                        border-radius: 3px;
                        cursor: pointer;
                    }}
                    button:hover {{
                        background-color: #45a049;
                    }}
                    .funcion-embebida {{
                        display: none;
                        color: #999;
                        font-style: italic;
                    }}
                </style>
            </head>
            <body>
                <header>
                    <h1>Tabla de Símbolos</h1>
                </header>
                <div class=""container"">
                    <div class=""filter-container"">
                        <label for=""filterAmbito"">Filtrar por ámbito:</label>
                        <select id=""filterAmbito"">
                            <option value="""">Todos</option>
                            <option value=""Global"">Global</option>
                        </select>

                        <label for=""filterTipoSimbolo"">Filtrar por tipo de símbolo:</label>
                        <select id=""filterTipoSimbolo"">
                            <option value="""">Todos</option>
                            <option value=""Función"">Función</option>
                            <option value=""Método"">Método</option>
                            <option value=""Variable"">Variable</option>
                        </select>

                        <label for=""searchInput"">Buscar:</label>
                        <input type=""text"" id=""searchInput"" placeholder=""Buscar en la tabla..."">

                        <label>
                            <input type=""checkbox"" id=""mostrarEmbebidas""> 
                            Mostrar funciones embebidas
                        </label>

                        <button onclick=""resetFilters()"">Resetear filtros</button>
                    </div>
                    {symbolsHTML}
                </div>
                <footer>
                    <p>Tabla de Símbolos - Generada por el compilador</p>
                </footer>

                <script>
                    // Cuando se carga la página
                    document.addEventListener('DOMContentLoaded', function() {{
                        const table = document.querySelector('table');
                        if (table) {{
                            const rows = Array.from(table.querySelectorAll('tbody tr'));
                            const ambitoSelect = document.getElementById('filterAmbito');

                            // Obtener ámbitos únicos
                            const ambitos = new Set();
                            rows.forEach(row => {{
                                if (row.cells.length >= 5) {{
                                    const ambito = row.cells[4].textContent.trim();
                                    ambitos.add(ambito);
                                }}
                            }});

                            // Añadir opciones de ámbito
                            ambitos.forEach(ambito => {{
                                if (ambito !== 'Global') {{ // Global ya está en el select
                                    const option = document.createElement('option');
                                    option.value = ambito;
                                    option.textContent = ambito;
                                    ambitoSelect.appendChild(option);
                                }}
                            }});
                        }}

                        // Añadir listener para el checkbox de funciones embebidas
                        document.getElementById('mostrarEmbebidas').addEventListener('change', function() {{
                            const mostrarEmbebidas = this.checked;
                            document.querySelectorAll('.funcion-embebida').forEach(row => {{
                                row.style.display = mostrarEmbebidas ? 'table-row' : 'none';
                            }});
                        }});
                    }});

                    // Filtrado de tabla
                    function filterTable() {{
                        const ambitoFilter = document.getElementById('filterAmbito').value.toLowerCase();
                        const tipoFilter = document.getElementById('filterTipoSimbolo').value.toLowerCase();
                        const searchText = document.getElementById('searchInput').value.toLowerCase();

                        const table = document.querySelector('table');
                        if (!table) return;

                        const rows = Array.from(table.querySelectorAll('tbody tr'));

                        rows.forEach(row => {{
                            if (row.cells.length >= 5) {{
                                const id = row.cells[1].textContent.toLowerCase();
                                const tipo = row.cells[2].textContent.toLowerCase();
                                const tipoDato = row.cells[3].textContent.toLowerCase();
                                const ambito = row.cells[4].textContent.toLowerCase();

                                const matchesAmbito = ambitoFilter === '' || ambito === ambitoFilter;
                                const matchesTipo = tipoFilter === '' || tipo === tipoFilter;
                                const matchesSearch = searchText === '' || 
                                                    id.includes(searchText) || 
                                                    tipo.includes(searchText) || 
                                                    tipoDato.includes(searchText) || 
                                                    ambito.includes(searchText);

                                row.style.display = (matchesAmbito && matchesTipo && matchesSearch) ? '' : 'none';
                            }}
                        }});
                    }}

                    // Resetear filtros
                    function resetFilters() {{
                        document.getElementById('filterAmbito').value = '';
                        document.getElementById('filterTipoSimbolo').value = '';
                        document.getElementById('searchInput').value = '';

                        const table = document.querySelector('table');
                        if (!table) return;

                        const rows = Array.from(table.querySelectorAll('tbody tr'));
                        rows.forEach(row => {{
                            row.style.display = '';
                        }});

                        // Mantener la configuración de funciones embebidas
                        const mostrarEmbebidas = document.getElementById('mostrarEmbebidas').checked;
                        if (!mostrarEmbebidas) {{
                            document.querySelectorAll('.funcion-embebida').forEach(row => {{
                                row.style.display = 'none';
                            }});
                        }}
                    }}

                    // Añadir listeners para filtros
                    document.getElementById('filterAmbito').addEventListener('change', filterTable);
                    document.getElementById('filterTipoSimbolo').addEventListener('change', filterTable);
                    document.getElementById('searchInput').addEventListener('input', filterTable);
                </script>
            </body>
            </html>";
        }
    }
}