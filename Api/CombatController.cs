using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Web.Dto;

namespace text_survival.Api;

public record CombatActionRequest(string ActionId);

[ApiController]
[Route("api/game/{sessionId}/combat")]
public class CombatController : GameControllerBase
{
    [HttpPost("action")]
    public ActionResult<GameResponse> Action(string sessionId, [FromBody] CombatActionRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.CombatPlayerTurn)
            return BadRequest(new { error = "Not player's turn" });

        if (ctx.PendingActivity.Combat == null)
            return BadRequest(new { error = "Combat state missing" });

        var combat = ctx.PendingActivity.Combat;

        // Validate action ID is in available actions
        if (!combat.AvailableActions.Contains(req.ActionId.ToLower()) &&
            !combat.AvailableActions.Contains(req.ActionId))
        {
            return BadRequest(new { error = $"Invalid combat action: {req.ActionId}" });
        }

        // Apply player action and get result
        var (newCombat, message) = ApplyCombatAction(ctx, combat, req.ActionId);

        // Check for combat end
        if (newCombat.IsOver)
        {
            ctx.PendingActivity.Combat = newCombat;
            ctx.PendingActivity.Phase = ActivityPhase.CombatResult;
        }
        else
        {
            // Apply animal turn immediately
            var (afterAnimal, animalMessage) = ApplyAnimalTurn(ctx, newCombat);

            if (afterAnimal.IsOver)
            {
                ctx.PendingActivity.Combat = afterAnimal;
                ctx.PendingActivity.Phase = ActivityPhase.CombatResult;
            }
            else
            {
                // Continue combat
                ctx.PendingActivity.Combat = afterAnimal;
            }
        }

        ctx.Update(1, ActivityType.Fighting);

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("continue")]
    public ActionResult<GameResponse> Continue(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.CombatResult)
            return BadRequest(new { error = "Combat not finished" });

        // Clear combat state
        ctx.PendingActivity = null;

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    private static (CombatSnapshot, string) ApplyCombatAction(GameContext ctx, CombatSnapshot combat, string actionId)
    {
        var newCombat = combat;
        string message = "";

        switch (actionId.ToLower())
        {
            case "thrust":
                // Attack - damage based on zone
                double damage = combat.Zone switch
                {
                    0 => 0.4,  // Melee
                    1 => 0.3,  // Close
                    2 => 0.2,  // Mid
                    _ => 0.1   // Far
                };
                double hitChance = combat.Zone switch
                {
                    0 => 0.9,
                    1 => 0.7,
                    2 => 0.5,
                    _ => 0.3
                };

                if (Random.Shared.NextDouble() < hitChance)
                {
                    newCombat = combat with { AnimalHealth = Math.Max(0, combat.AnimalHealth - damage) };
                    message = "You strike true!";
                }
                else
                {
                    message = "Your thrust misses.";
                }
                break;

            case "back_away":
                // Retreat - increase distance
                int newZone = Math.Min(3, combat.Zone + 1);
                newCombat = combat with { Zone = newZone };
                message = "You back away carefully.";
                break;

            case "hold_ground":
                // Defensive stance
                message = "You hold your ground.";
                break;

            default:
                message = $"Unknown action: {actionId}";
                break;
        }

        return (newCombat, message);
    }

    private static (CombatSnapshot, string) ApplyAnimalTurn(GameContext ctx, CombatSnapshot combat)
    {
        var newCombat = combat;
        string message = "";

        // Simple animal AI - attack if close, approach if far
        if (combat.Zone <= 1)
        {
            // Animal attacks
            double damage = 0.2;
            double hitChance = 0.6;

            if (Random.Shared.NextDouble() < hitChance)
            {
                newCombat = combat with { PlayerHealth = Math.Max(0, combat.PlayerHealth - damage) };
                message = "The animal strikes!";
            }
            else
            {
                message = "The animal's attack misses.";
            }
        }
        else
        {
            // Animal approaches
            int newZone = Math.Max(0, combat.Zone - 1);
            newCombat = combat with { Zone = newZone };
            message = "The animal closes in.";
        }

        return (newCombat, message);
    }
}
