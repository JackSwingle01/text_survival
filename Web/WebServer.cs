using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace text_survival.Web;

/// <summary>
/// HTTP server for the text survival game. Serves static files and REST API.
/// Stateless design - each request transforms state, no blocking I/O.
/// </summary>
public static class WebServer
{

    public static async Task Run(int port = 5000)
    {
        var app = Program.BuildApp(null, port);
        await app.RunAsync();
    }


    // public static async Task Run(int port = 5000)
    // {
    //     var builder = WebApplication.CreateBuilder();
    //     builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

    //     // Suppress ASP.NET Core startup logging
    //     builder.Logging.ClearProviders();

    //     // Add controllers with JSON configuration
    //     builder.Services.AddControllers()
    //         .AddJsonOptions(options =>
    //         {
    //             options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    //             options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    //             options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
    //         });

    //     var app = builder.Build();

    //     // Serve static files from wwwroot
    //     var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    //     if (Directory.Exists(wwwrootPath))
    //     {
    //         app.UseStaticFiles(new StaticFileOptions
    //         {
    //             FileProvider = new PhysicalFileProvider(wwwrootPath)
    //         });
    //     }

    //     // Map API controllers
    //     app.MapControllers();

    //     // Fallback to index.html for SPA-style routing
    //     app.MapFallbackToFile("index.html");

    //     Console.WriteLine($"[WebServer] Starting on http://localhost:{port}");
    //     Console.WriteLine("[WebServer] REST API mode - stateless");
    //     Console.WriteLine("[WebServer] Press Ctrl+C to stop");

    //     await app.RunAsync();
    // }
}
