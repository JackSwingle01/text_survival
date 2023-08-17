using Microsoft.AspNetCore;
using text_survival_rpg_web;
using text_survival_rpg_web.Web;

public class Program
{
    public static void Main(string[] args)
    {
        if (Config.io == Config.IOType.Web)
        {
            CreateWebHostBuilder(args).Build().Run();
        }
        else
        {
            Game game = new Game();
            game.StartGame().Wait();
        }
    }
    public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
        WebHost.CreateDefaultBuilder(args).UseStartup<Startup>();
}







