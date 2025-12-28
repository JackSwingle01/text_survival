using text_survival.Web;

namespace text_survival.Core
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            int port = 5000;
            var portArg = args.FirstOrDefault(a => a.StartsWith("--port="));
            if (portArg != null && int.TryParse(portArg.Split('=')[1], out int parsedPort))
                port = parsedPort;

            await WebServer.Run(port);
            return;
        }
    }
}
