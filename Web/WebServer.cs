using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using text_survival.Actions;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Environments.Factories;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web.Dto;

namespace text_survival.Web;

public static class WebServer
{
    public static async Task Run(int port = 5000)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");

        // Suppress ASP.NET Core startup logging
        builder.Logging.ClearProviders();

        var app = builder.Build();

        // Configure WebSockets
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        });

        // Serve static files from wwwroot
        var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (Directory.Exists(wwwrootPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(wwwrootPath)
            });
        }

        // WebSocket endpoint
        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync();
            var sessionId = Guid.NewGuid().ToString();
            var session = new WebGameSession(socket);
            SessionRegistry.Register(sessionId, session);

            Console.WriteLine($"[WebServer] New session: {sessionId}");

            try
            {
                // Start receive loop in background (uses session's cancellation token)
                var receiveTask = session.ReceiveLoopAsync();

                // Run game on current thread (blocking)
                await Task.Run(() => RunGame(sessionId));

                // Wait for receive to finish
                await receiveTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebServer] Session {sessionId} error: {ex.Message}");
            }
            finally
            {
                SessionRegistry.Remove(sessionId);
                Console.WriteLine($"[WebServer] Session {sessionId} ended");
            }
        });

        // Fallback to index.html for SPA-style routing
        app.MapFallbackToFile("index.html");

        Console.WriteLine($"[WebServer] Starting on http://localhost:{port}");
        Console.WriteLine("[WebServer] Press Ctrl+C to stop");

        app.Lifetime.ApplicationStopping.Register(() => SessionRegistry.CancelAll());

        await app.RunAsync();
    }

    private static void RunGame(string sessionId)
    {
        // Initialize game state (same as Program.Main)
        Zone zone = ZoneFactory.MakeForestZone();

        var gameStartTime = new DateTime(2025, 1, 1, 9, 0, 0);
        zone.Weather.Update(gameStartTime);

        Location startingArea = zone.Graph.All.First(s => s.Name == "Forest Camp");

        HeatSourceFeature campfire = new HeatSourceFeature();
        campfire.AddFuel(2, FuelType.Kindling);
        startingArea.Features.Add(campfire);

        Player player = new Player();
        Camp camp = new Camp(startingArea);
        GameContext ctx = new GameContext(player, camp)
        {
            SessionId = sessionId
        };

        // Equip starting clothing
        ctx.Inventory.Equip(Equipment.WornFurChestWrap());
        ctx.Inventory.Equip(Equipment.FurLegWraps());
        ctx.Inventory.Equip(Equipment.FurBoots());

        // Add starting supplies
        ctx.Inventory.Tools.Add(Tool.HandDrill());
        ctx.Inventory.Sticks.Add(0.3);
        ctx.Inventory.Sticks.Add(0.25);
        ctx.Inventory.Sticks.Add(0.35);
        ctx.Inventory.Tinder.Add(0.05);
        ctx.Inventory.Tinder.Add(0.04);

        // Opening narrative
        GameDisplay.AddDanger(ctx, "You wake up in the forest, shivering. You don't remember how you got here.");
        GameDisplay.AddDanger(ctx, "Snow drifts down through the pines. The cold is already seeping into your bones.");
        GameDisplay.AddDanger(ctx, "There's a fire pit nearby with some kindling. You need to get it lit - fast.");
        GameDisplay.AddDanger(ctx, "You need to gather fuel, find food and water, and survive.");

        // Run the game
        GameRunner runner = new GameRunner(ctx);
        runner.Run();

        // Show death screen
        DisplayDeathScreen(ctx);
    }

    private static void DisplayDeathScreen(GameContext ctx)
    {
        Player player = ctx.player;

        GameDisplay.AddNarrative(ctx, "");
        GameDisplay.AddNarrative(ctx, "═══════════════════════════════════════════════════════════");
        GameDisplay.AddDanger(ctx, "                       YOU DIED                            ");
        GameDisplay.AddNarrative(ctx, "═══════════════════════════════════════════════════════════");
        GameDisplay.AddNarrative(ctx, "");

        string causeOfDeath = DetermineCauseOfDeath(player);
        GameDisplay.AddDanger(ctx, $"Cause of Death: {causeOfDeath}");
        GameDisplay.AddNarrative(ctx, "");

        var body = player.Body;
        GameDisplay.AddNarrative(ctx, "=== Final Survival Stats ===");
        GameDisplay.AddNarrative(ctx, $"Vitality: {player.Vitality * 100:F1}%");
        GameDisplay.AddNarrative(ctx, $"Calories: {body.CalorieStore:F0}/{Survival.SurvivalProcessor.MAX_CALORIES:F0}");
        GameDisplay.AddNarrative(ctx, $"Hydration: {body.Hydration:F0}/{Survival.SurvivalProcessor.MAX_HYDRATION:F0}");
        GameDisplay.AddNarrative(ctx, $"Body Temperature: {body.BodyTemperature:F1}F");
        GameDisplay.AddNarrative(ctx, "");

        var startTime = new DateTime(2025, 1, 1, 9, 0, 0);
        var timeSurvived = ctx.GameTime - startTime;
        GameDisplay.AddNarrative(ctx, $"You survived for {timeSurvived.Days} days, {timeSurvived.Hours} hours, and {timeSurvived.Minutes} minutes.");
        GameDisplay.AddNarrative(ctx, "");
        GameDisplay.AddNarrative(ctx, "═══════════════════════════════════════════════════════════");
        GameDisplay.AddNarrative(ctx, "                    GAME OVER                              ");
        GameDisplay.AddNarrative(ctx, "═══════════════════════════════════════════════════════════");

        // Send final frame
        GameDisplay.Render(ctx);

        // Wait for player acknowledgment
        Input.WaitForKey(ctx, "Press any key to close...");
    }

    private static string DetermineCauseOfDeath(Player player)
    {
        var body = player.Body;

        var brain = body.Parts.SelectMany(p => p.Organs).FirstOrDefault(o => o.Name == "Brain");
        var heart = body.Parts.SelectMany(p => p.Organs).FirstOrDefault(o => o.Name == "Heart");
        var lungs = body.Parts.SelectMany(p => p.Organs).FirstOrDefault(o => o.Name == "Lungs");
        var liver = body.Parts.SelectMany(p => p.Organs).FirstOrDefault(o => o.Name == "Liver");

        if (brain?.Condition <= 0) return "Brain death";
        if (heart?.Condition <= 0) return "Cardiac arrest";
        if (lungs?.Condition <= 0) return "Respiratory failure";
        if (liver?.Condition <= 0) return "Liver failure";

        if (body.BodyTemperature < 89.6)
            return "Severe hypothermia";

        if (body.Hydration <= 0)
            return "Severe dehydration";

        if (body.CalorieStore <= 0 && body.BodyFatPercentage < 0.05)
            return "Starvation";

        return "Multiple organ failure";
    }
}
