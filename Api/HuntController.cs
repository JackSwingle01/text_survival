using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.UI;
using text_survival.Web.Dto;

namespace text_survival.Api;

[ApiController]
[Route("api/game/{sessionId}/hunt")]
public class HuntController : GameControllerBase
{
    [HttpPost("approach")]
    public ActionResult<GameResponse> Approach(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.HuntActive &&
            ctx.PendingActivity?.Phase != ActivityPhase.HuntSighting)
            return BadRequest(new { error = "No active hunt" });

        if (ctx.PendingActivity.Hunt == null || ctx.PendingActivity.HuntTarget == null)
            return BadRequest(new { error = "Hunt state missing" });

        var hunt = ctx.PendingActivity.Hunt;
        var target = ctx.PendingActivity.HuntTarget;

        ctx.PendingActivity.Phase = ActivityPhase.HuntActive;

        double approachDistance = 8 + Random.Shared.NextDouble() * 8;
        target.DistanceFromPlayer = Math.Max(3, target.DistanceFromPlayer - approachDistance);

        bool spooked = Random.Shared.NextDouble() < 0.15 * (1 + (target.State == AnimalState.Alert ? 0.5 : 0));

        if (spooked)
        {
            ctx.PendingActivity.Hunt = hunt with
            {
                Phase = "escaped",
                Distance = target.DistanceFromPlayer,
                StatusMessage = $"The {target.Name.ToLower()} spots you and bolts!"
            };
            ctx.PendingActivity.Phase = ActivityPhase.HuntResult;
        }
        else
        {
            ctx.PendingActivity.Hunt = hunt with
            {
                Distance = target.DistanceFromPlayer,
                Approaches = hunt.Approaches + 1,
                MinutesSpent = hunt.MinutesSpent + 5,
                AnimalState = target.State.ToString().ToLower(),
                StatusMessage = $"You creep closer... {target.DistanceFromPlayer:F0}m away.",
                AvailableChoices = BuildHuntChoices(ctx, target)
            };
        }

        ctx.Update(5, ActivityType.Hunting);

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("throw")]
    public ActionResult<GameResponse> Throw(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.HuntActive)
            return BadRequest(new { error = "No active hunt" });

        if (ctx.PendingActivity.Hunt == null || ctx.PendingActivity.HuntTarget == null)
            return BadRequest(new { error = "Hunt state missing" });

        var hunt = ctx.PendingActivity.Hunt;
        var target = ctx.PendingActivity.HuntTarget;

        double distanceFactor = Math.Max(0.1, 1.0 - target.DistanceFromPlayer / 30.0);
        double hitChance = distanceFactor * ctx.player.Dexterity;
        bool hit = Random.Shared.NextDouble() < hitChance;

        if (hit)
        {
            var damageInfo = ctx.player.GetAttackDamage(BodyTarget.Random);
            target.Body.Damage(damageInfo);

            ctx.PendingActivity.Hunt = hunt with
            {
                Phase = "killed",
                StatusMessage = $"Your throw is true! The {target.Name.ToLower()} falls."
            };
            CreateCarcassFromHunt(ctx, target);
        }
        else
        {
            ctx.PendingActivity.Hunt = hunt with
            {
                Phase = "escaped",
                StatusMessage = $"Your throw goes wide. The {target.Name.ToLower()} flees."
            };
        }

        ctx.PendingActivity.Phase = ActivityPhase.HuntResult;
        ctx.Update(2, ActivityType.Hunting);

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("strike")]
    public ActionResult<GameResponse> Strike(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.HuntActive)
            return BadRequest(new { error = "No active hunt" });

        if (ctx.PendingActivity.Hunt == null || ctx.PendingActivity.HuntTarget == null)
            return BadRequest(new { error = "Hunt state missing" });

        var hunt = ctx.PendingActivity.Hunt;
        var target = ctx.PendingActivity.HuntTarget;

        if (target.DistanceFromPlayer > 5)
            return BadRequest(new { error = "Too far for melee strike" });

        double hitChance = 0.7 * ctx.player.Dexterity;
        bool hit = Random.Shared.NextDouble() < hitChance;

        if (hit)
        {
            var damageInfo = ctx.player.GetAttackDamage(BodyTarget.Random);
            target.Body.Damage(damageInfo);

            ctx.PendingActivity.Hunt = hunt with
            {
                Phase = "killed",
                StatusMessage = $"You strike! The {target.Name.ToLower()} falls."
            };
            CreateCarcassFromHunt(ctx, target);
        }
        else
        {
            ctx.PendingActivity.Hunt = hunt with
            {
                Phase = "escaped",
                StatusMessage = $"You miss! The {target.Name.ToLower()} escapes."
            };
        }

        ctx.PendingActivity.Phase = ActivityPhase.HuntResult;
        ctx.Update(1, ActivityType.Hunting);

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("wait")]
    public ActionResult<GameResponse> Wait(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.HuntActive)
            return BadRequest(new { error = "No active hunt" });

        if (ctx.PendingActivity.Hunt == null || ctx.PendingActivity.HuntTarget == null)
            return BadRequest(new { error = "Hunt state missing" });

        var hunt = ctx.PendingActivity.Hunt;
        var target = ctx.PendingActivity.HuntTarget;

        if (target.State == AnimalState.Alert)
        {
            target.State = AnimalState.Idle;
        }

        double drift = (Random.Shared.NextDouble() - 0.5) * 10;
        target.DistanceFromPlayer = Math.Max(5, target.DistanceFromPlayer + drift);

        ctx.PendingActivity.Hunt = hunt with
        {
            Distance = target.DistanceFromPlayer,
            MinutesSpent = hunt.MinutesSpent + 5,
            AnimalState = target.State.ToString().ToLower(),
            StatusMessage = "You wait and watch...",
            AvailableChoices = BuildHuntChoices(ctx, target)
        };

        ctx.Update(5, ActivityType.Hunting);

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("assess")]
    public ActionResult<GameResponse> Assess(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.HuntActive &&
            ctx.PendingActivity?.Phase != ActivityPhase.HuntSighting)
            return BadRequest(new { error = "No active hunt" });

        if (ctx.PendingActivity.Hunt == null || ctx.PendingActivity.HuntTarget == null)
            return BadRequest(new { error = "Hunt state missing" });

        var hunt = ctx.PendingActivity.Hunt;
        var target = ctx.PendingActivity.HuntTarget;

        var assessment = $"A {target.Name.ToLower()} - {target.GetTraitDescription()}. " +
                        $"It appears {target.GetActivityDescription()}.";

        ctx.PendingActivity.Hunt = hunt with
        {
            AnimalDescription = assessment,
            StatusMessage = assessment,
            AvailableChoices = BuildHuntChoices(ctx, target)
        };

        ctx.Update(1, ActivityType.Hunting);

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("abandon")]
    public ActionResult<GameResponse> Abandon(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.HuntActive &&
            ctx.PendingActivity?.Phase != ActivityPhase.HuntSighting)
            return BadRequest(new { error = "No active hunt" });

        if (ctx.PendingActivity.Hunt != null)
        {
            ctx.PendingActivity.Hunt = ctx.PendingActivity.Hunt with
            {
                Phase = "abandoned",
                StatusMessage = "You give up the hunt."
            };
        }
        ctx.PendingActivity.Phase = ActivityPhase.HuntResult;

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    private static void CreateCarcassFromHunt(GameContext ctx, Animal target)
    {
        var carcass = new CarcassFeature(target);
        ctx.CurrentLocation.AddFeature(carcass);

        // Look up the source herd by index and remove the killed animal
        var sourceHerd = ctx.Herds.GetHerdByIndex(ctx.PendingActivity?.HuntSourceHerdIndex);
        if (sourceHerd != null)
        {
            sourceHerd.RemoveMember(target);
        }
    }

    private static List<HuntChoiceSnapshot> BuildHuntChoices(GameContext ctx, Animal target)
    {
        var choices = new List<HuntChoiceSnapshot>();

        choices.Add(new HuntChoiceSnapshot(
            Id: "approach",
            Label: "Approach",
            Description: "Get closer to the prey",
            IsAvailable: true,
            DisabledReason: null
        ));

        bool hasWeapon = ctx.Inventory.Weapon != null;
        bool inRange = target.DistanceFromPlayer <= 20;
        choices.Add(new HuntChoiceSnapshot(
            Id: "throw",
            Label: "Throw",
            Description: "Throw your weapon",
            IsAvailable: hasWeapon && inRange,
            DisabledReason: !hasWeapon ? "No weapon" : !inRange ? "Too far" : null
        ));

        choices.Add(new HuntChoiceSnapshot(
            Id: "abandon",
            Label: "Abandon",
            Description: "Give up the hunt",
            IsAvailable: true,
            DisabledReason: null
        ));

        return choices;
    }
}
