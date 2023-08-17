using Microsoft.AspNetCore.Mvc;

namespace text_survival_rpg_web.Web
{
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
            var response = HttpContext.Response;
            response.Headers.Add("Content-Type", "text/event-stream");
            while (!gameTask.IsCompleted)
            {
                if (game.OutputQueue.Count > 0)
                {
                    await response.WriteAsync($"data: {game.OutputQueue.Dequeue().Replace("\n", "<br>")}\n\n");
                    await response.Body.FlushAsync();
                }
                await Task.Delay(1);
            }
        }

    }
}
