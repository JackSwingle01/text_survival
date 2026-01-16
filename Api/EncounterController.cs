using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Actors.Animals;
using text_survival.Combat;
using text_survival.Environments.Grid;
using text_survival.Web.Dto;

namespace text_survival.Api;

[ApiController]
[Route("api/game/{sessionId}/encounter")]
public class EncounterController : GameControllerBase
{
    [HttpPost("stand")]
    public ActionResult<GameResponse> Stand(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.EncounterActive)
            return BadRequest(new { error = "No active encounter" });

        if (ctx.PendingActivity.Encounter == null)
            return BadRequest(new { error = "Encounter state missing" });

        var encounter = ctx.PendingActivity.Encounter;
        var predator = ctx.PendingActivity.EncounterPredator;

        double currentBoldness = predator?.EncounterBoldness ?? encounter.Boldness;
        double newBoldness = Math.Max(0, currentBoldness - 0.15);
        if (predator != null)
            predator.EncounterBoldness = newBoldness;

        if (newBoldness < 0.3)
        {
            ctx.PendingActivity.Phase = ActivityPhase.EncounterOutcome;
            ctx.PendingActivity.Encounter = encounter with
            {
                Boldness = 0,
                BoldnessDescriptor = "retreating",
                StatusMessage = $"The {encounter.AnimalType.ToLower()} backs away and retreats."
            };
        }
        else
        {
            ctx.PendingActivity.Encounter = encounter with
            {
                Boldness = newBoldness,
                BoldnessDescriptor = GetBoldnessDescriptor(newBoldness),
                StatusMessage = $"The {encounter.AnimalType.ToLower()} seems less certain."
            };
        }

        ctx.Update(1, ActivityType.Encounter);

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("back")]
    public ActionResult<GameResponse> Back(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.EncounterActive)
            return BadRequest(new { error = "No active encounter" });

        if (ctx.PendingActivity.Encounter == null)
            return BadRequest(new { error = "Encounter state missing" });

        var encounter = ctx.PendingActivity.Encounter;
        var predator = ctx.PendingActivity.EncounterPredator;

        double currentDistance = predator?.DistanceFromPlayer ?? encounter.Distance;
        double currentBoldness = predator?.EncounterBoldness ?? encounter.Boldness;
        double newDistance = currentDistance + 5;
        double newBoldness = Math.Min(1.0, currentBoldness + 0.05);

        if (predator != null)
        {
            predator.DistanceFromPlayer = newDistance;
            predator.EncounterBoldness = newBoldness;
        }

        if (newBoldness >= 0.8 && currentDistance < 15)
        {
            return TransitionToCombat(ctx, sessionId, predator, "The predator charges!");
        }

        if (newDistance > 40)
        {
            ctx.PendingActivity.Phase = ActivityPhase.EncounterOutcome;
            ctx.PendingActivity.Encounter = encounter with
            {
                Distance = newDistance,
                Boldness = newBoldness,
                BoldnessDescriptor = GetBoldnessDescriptor(newBoldness),
                StatusMessage = "You've put enough distance between you. It doesn't follow."
            };
        }
        else
        {
            ctx.PendingActivity.Encounter = encounter with
            {
                Distance = newDistance,
                Boldness = newBoldness,
                BoldnessDescriptor = GetBoldnessDescriptor(newBoldness),
                StatusMessage = "You slowly back away, keeping eyes on the predator."
            };
        }

        ctx.Update(1, ActivityType.Encounter);

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("drop-meat")]
    public ActionResult<GameResponse> DropMeat(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.EncounterActive)
            return BadRequest(new { error = "No active encounter" });

        var encounter = ctx.PendingActivity.Encounter;

        double meatWeight = ctx.Inventory.GetWeight(ResourceCategory.Food);
        if (meatWeight > 0)
        {
            ctx.Inventory.Remove(Resource.RawMeat, ctx.Inventory.Count(Resource.RawMeat));
            ctx.Inventory.Remove(Resource.CookedMeat, ctx.Inventory.Count(Resource.CookedMeat));
        }

        ctx.PendingActivity.Phase = ActivityPhase.EncounterOutcome;
        if (encounter != null)
        {
            ctx.PendingActivity.Encounter = encounter with
            {
                StatusMessage = $"You drop your food. The {encounter.AnimalType.ToLower()} goes for the meat, ignoring you."
            };
        }

        ctx.Update(1, ActivityType.Encounter);

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("attack")]
    public ActionResult<GameResponse> Attack(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.EncounterActive)
            return BadRequest(new { error = "No active encounter" });

        var predator = ctx.PendingActivity.EncounterPredator;
        return TransitionToCombat(ctx, sessionId, predator, "You attack!");
    }

    [HttpPost("run")]
    public ActionResult<GameResponse> Run(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.EncounterActive)
            return BadRequest(new { error = "No active encounter" });

        var predator = ctx.PendingActivity.EncounterPredator;
        var encounter = ctx.PendingActivity.Encounter;

        double escapeChance = ctx.player.Speed * (1.0 - (encounter?.Boldness ?? 0.5));
        bool escaped = Random.Shared.NextDouble() < escapeChance;

        if (escaped)
        {
            ctx.PendingActivity.Phase = ActivityPhase.EncounterOutcome;
            if (encounter != null)
            {
                ctx.PendingActivity.Encounter = encounter with
                {
                    StatusMessage = $"You run! The {encounter.AnimalType.ToLower()} doesn't give chase."
                };
            }
        }
        else
        {
            return TransitionToCombat(ctx, sessionId, predator, "You try to run, but the predator catches you!");
        }

        ctx.Update(2, ActivityType.Encounter);

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    private ActionResult<GameResponse> TransitionToCombat(GameContext ctx, string sessionId, Animal? predator, string narrative)
    {
        if (predator == null)
            return BadRequest(new { error = "No predator for combat transition" });

        var playerUnit = new Unit(ctx.player, new GridPosition(12, 4));
        var enemyUnit = new Unit(predator, new GridPosition(12, 10));
        var scenario = new CombatScenario(
            [playerUnit],
            [enemyUnit],
            playerUnit);

        ctx.PendingActivity.Phase = ActivityPhase.CombatPlayerTurn;
        ctx.PendingActivity.Encounter = null;
        ctx.PendingActivity.CombatScenario = scenario;
        ctx.PendingActivity.CombatPlayerUnit = playerUnit;
        ctx.PendingActivity.CombatNarrative = narrative;
        ctx.PendingActivity.Combat = new CombatSnapshot(
            AnimalType: predator.Name,
            Zone: 2,
            AnimalHealth: predator.Vitality,
            PlayerHealth: ctx.player.Vitality,
            AnimalState: "threatening",
            AvailableActions: ["thrust", "back_away", "dodge"],
            DistanceMeters: playerUnit.Position.DistanceTo(enemyUnit.Position),
            ZoneName: "close",
            PlayerEnergy: ctx.player.Body.Energy / 480.0,
            Narrative: narrative
        );

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    private static string GetBoldnessDescriptor(double boldness)
    {
        return boldness switch
        {
            >= 0.8 => "aggressive",
            >= 0.6 => "confident",
            >= 0.4 => "wary",
            >= 0.2 => "hesitant",
            _ => "retreating"
        };
    }
}
