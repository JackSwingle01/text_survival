using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace text_survival.Web;

public partial class Program
{
    public static WebApplication BuildApp(string[]? args = null, int? port = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());

        if (port.HasValue)
        {
            builder.WebHost.UseUrls($"http://0.0.0.0:{port.Value}");
        }

        builder.Logging.ClearProviders();

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
            });

        var app = builder.Build();

        var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (Directory.Exists(wwwrootPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(wwwrootPath)
            });
        }

        app.MapControllers();
        app.MapFallbackToFile("index.html");

        return app;
    }

    public static async Task Main(string[] args)
    {
        int port = 5000;
        var portArg = args.FirstOrDefault(a => a.StartsWith("--port="));
        if (portArg != null && int.TryParse(portArg.Split('=')[1], out var parsed))
            port = parsed;

        var app = BuildApp(args, port);
        await app.RunAsync();
    }
}
