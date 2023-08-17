using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using text_survival_rpg_web;
using EventHandler = text_survival_rpg_web.EventHandler;

public class Program
{
    public static void Main(string[] args)
    {
        CreateWebHostBuilder(args).Build().Run();
    }
    public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
        WebHost.CreateDefaultBuilder(args)
            .UseStartup<Startup>();
}
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
    }
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        app.UseRouting();
        app.UseStaticFiles();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
[ApiController]
[Route("[controller]")]
public class StartController : ControllerBase
{
    [HttpGet]
    public async Task Get()
    {
        Game game = new Game();
        EventHandler.Subscribe<WriteEvent>(game.OnWriteEvent);
        var gameTask = Task.Run(() => game.StartGame());
        var response = Response;
        response.Headers.Add("Content-Type", "text/event-stream");
        while (!gameTask.IsCompleted)
        {
            if (game.OutputQueue.Count > 0)
            {
                await response.WriteAsync($"data: {game.OutputQueue.Dequeue().Replace("\n", "<br>")}\n\n");
                await response.Body.FlushAsync();
            }
            await Task.Delay(100);
        }
    }

}
[ApiController]
[Route("[controller]")]
public class InputController : ControllerBase
{

    public class InputData
    {
        public string Input { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] InputData data)
    {
        Input.OnUserInputReceived(data.Input);
        return Ok();
    }


}

public class Game
{
    public Game()
    {
        OutputQueue = new Queue<string>();
    }

    public Queue<string> OutputQueue;
    public async Task StartGame()
    {

        var game = new Game();
        Output.WriteLine("You have been banished from your home city.");
        Thread.Sleep(1000);
        Output.WriteLine("Stripped of your possessions, you've been left to fend for yourself in the unforgiving wilderness.");
        Thread.Sleep(1000);
        Output.WriteLine("The ancient laws, however, grant one path to redemption:");
        Thread.Sleep(1000);
        Output.WriteLine("To kill a Dragon...\n");
        Thread.Sleep(2000);

        Player player = World.Player;
        Actions actions = new(player);
        while (player.Health > 0)
        {
            actions.Act();
        }
    }



    public void OnWriteEvent(WriteEvent e)
    {
        OutputQueue.Enqueue(e.Message);
    }

}


