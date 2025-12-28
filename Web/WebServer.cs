using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using text_survival.Actions;
using text_survival.Actors.Player;
using text_survival.IO;
using text_survival.Persistence;
using text_survival.UI;
using text_survival.Web.Dto;

namespace text_survival.Web;

public static class WebServer
{
    public static async Task Run(int port = 5000)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

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

            // Read or create session ID from cookie
            string sessionId;
            if (context.Request.Cookies.TryGetValue("session_id", out var existingId)
                && !string.IsNullOrEmpty(existingId))
            {
                sessionId = existingId;
                Console.WriteLine($"[WebServer] Resuming session: {sessionId}");
            }
            else
            {
                sessionId = Guid.NewGuid().ToString();
                context.Response.Cookies.Append("session_id", sessionId, new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Strict,
                    MaxAge = TimeSpan.FromDays(30)
                });
                Console.WriteLine($"[WebServer] New session: {sessionId}");
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync();

            // Dispose old session if reconnecting
            var oldSession = SessionRegistry.Get(sessionId);
            if (oldSession != null)
            {
                Console.WriteLine($"[WebServer] Disposing old session for reconnect: {sessionId}");
                oldSession.Dispose();
            }

            var session = new WebGameSession(socket);
            SessionRegistry.Register(sessionId, session);

            try
            {
                // Start receive loop in background (uses session's cancellation token)
                var receiveTask = session.ReceiveLoopAsync();

                // Run game on current thread (blocking)
                await Task.Run(() => RunGame(sessionId));

                // Wait for receive to finish
                await receiveTask;
            }
            catch (OperationCanceledException)
            {
                // Session cancelled due to timeout/disconnect - preserve save file
                Console.WriteLine($"[WebServer] Session {sessionId} cancelled (timeout/disconnect)");
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
        GameContext ctx;
        bool isNewGame;

        // Try to load existing save
        if (SaveManager.HasSaveFile(sessionId))
        {
            var (loadedCtx, error) = SaveManager.Load(sessionId);

            if (error != null)
            {
                // Save load failed - prompt user
                var tempCtx = GameContext.CreateNewGame();
                tempCtx.SessionId = sessionId;

                GameDisplay.AddDanger(tempCtx, "═══════════════════════════════════════════════════════════");
                GameDisplay.AddDanger(tempCtx, "                  SAVE LOAD FAILED                          ");
                GameDisplay.AddDanger(tempCtx, "═══════════════════════════════════════════════════════════");
                GameDisplay.AddNarrative(tempCtx, "");
                GameDisplay.AddWarning(tempCtx, "Your save file is corrupted or incompatible with this version.");
                GameDisplay.AddNarrative(tempCtx, $"Error: {error}");
                GameDisplay.AddNarrative(tempCtx, "");
                GameDisplay.Render(tempCtx);

                bool startNew = Input.Confirm(tempCtx, "Would you like to start a new game?", defaultValue: true);

                if (!startNew)
                {
                    GameDisplay.AddNarrative(tempCtx, "");
                    GameDisplay.AddNarrative(tempCtx, "Game cancelled. Refresh the page to try again.");
                    GameDisplay.Render(tempCtx);
                    return;
                }

                // Delete corrupted save and start fresh
                SaveManager.DeleteSave(sessionId);
                ctx = GameContext.CreateNewGame();
                ctx.SessionId = sessionId;
                isNewGame = true;
            }
            else if (loadedCtx != null)
            {
                // Load succeeded
                ctx = loadedCtx;
                ctx.SessionId = sessionId;
                isNewGame = false;
            }
            else
            {
                // No save found (shouldn't happen since we checked HasSaveFile, but handle anyway)
                ctx = GameContext.CreateNewGame();
                ctx.SessionId = sessionId;
                isNewGame = true;
            }
        }
        else
        {
            // No save file exists
            ctx = GameContext.CreateNewGame();
            ctx.SessionId = sessionId;
            isNewGame = true;
        }

        // Show appropriate intro message
        if (isNewGame)
        {
            GameDisplay.AddDanger(ctx, "You wake up in the forest, shivering. You don't remember how you got here.");
            GameDisplay.AddDanger(ctx, "Snow drifts down through the pines. The cold is already seeping into your bones.");
            GameDisplay.AddDanger(ctx, "There's a fire pit nearby with some kindling. You need to get it lit - fast.");
            GameDisplay.AddDanger(ctx, "You need to gather fuel, find food and water, and survive.");
        }
        else
        {
            // GameDisplay.AddNarrative(ctx, "Game loaded.");
        }

        // Run the game
        try
        {
            GameRunner runner = new GameRunner(ctx);
            runner.Run();

            // Delete save on death
            SaveManager.DeleteSave(sessionId);

            // Show death screen
            DisplayDeathScreen(ctx);
        }
        catch (OperationCanceledException)
        {
            // Session cancelled - save is preserved, just exit cleanly
            Console.WriteLine($"[WebServer] Game cancelled for session {sessionId}");
            throw; // Re-throw to outer handler
        }
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
