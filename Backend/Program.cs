using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Proyecto2;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    WebRootPath = Path.Combine(Directory.GetCurrentDirectory(), "Frontend/public/")
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// Configurar endpoints para reporte de errores, ast y tabla de simbolos
ErrorReporter.ConfigureErrorTableEndpoint(app, 
    () => Proyecto_2.Controllers.Compile.GetErroresSintacticos(), 
    () => Proyecto_2.Controllers.Compile.GetErroresLexicos(),
    () => Proyecto_2.Controllers.Compile.GetErroresSemanticos());

AstVisualizer.ConfigureAstEndpoint(app, 
    () => Proyecto_2.Controllers.Compile.GetLastTree());

SymbolTableReporter.ConfigureSymbolTableEndpoint(app, 
    () => Proyecto_2.Controllers.Compile.GetSymbolos());
    
// Redirigir todas las solicitudes a index.html
app.MapFallbackToFile("index.html");

app.UseHttpsRedirection();

app.Run();