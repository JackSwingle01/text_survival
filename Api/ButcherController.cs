using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web.Dto;

namespace text_survival.Api;

public record ButcherModeRequest(string ModeId);

[ApiController]
[Route("api/game/{sessionId}/butcher")]
public class ButcherController : GameControllerBase
{
    [HttpPost("open")]
    public ActionResult<GameResponse> Open(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
        if (carcass == null)
            return BadRequest(new { error = "No carcass to butcher here" });

        if (carcass.IsCompletelyButchered)
            return BadRequest(new { error = "There's nothing left to butcher" });

        var butcherData = BuildButcherData(ctx, carcass);
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { new ButcherOverlay(butcherData) });

        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    [HttpPost("mode")]
    public ActionResult<GameResponse> SelectMode(string sessionId, [FromBody] ButcherModeRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
        if (carcass == null)
            return BadRequest(new { error = "No carcass to butcher here" });

        if (carcass.IsCompletelyButchered)
            return BadRequest(new { error = "There's nothing left to butcher" });

        // Parse mode selection
        var mode = req.ModeId switch
        {
            "quick" => ButcheringMode.QuickStrip,
            "careful" => ButcheringMode.Careful,
            "full" => ButcheringMode.FullProcessing,
            _ => ButcheringMode.Careful
        };

        // Execute butchering with selected mode (logic inlined from ButcherStrategy + WorkRunner)
        var strategy = new ButcherStrategy(carcass, mode);

        // Calculate time required
        int baseTime = carcass.GetRemainingMinutes(mode);
        var (adjustedTime, warnings) = strategy.ApplyImpairments(ctx, ctx.CurrentLocation, baseTime);

        // Show warnings
        foreach (var warning in warnings)
        {
            GameDisplay.AddWarning(ctx, warning);
        }

        // Update with time passage
        ctx.Update(adjustedTime, ActivityType.Butchering);

        // Execute butchering (inlined from ButcherStrategy.Execute)
        var modeConfig = CarcassFeature.GetModeConfig(mode);
        bool hasCuttingTool = ctx.Inventory.HasCuttingTool;
        double dexterity = AbilityCalculator.GetDexterity(ctx.player, ctx);
        bool dexterityImpaired = dexterity < 0.7;

        if (!hasCuttingTool)
        {
            GameDisplay.AddWarning(ctx, "Without a cutting tool, you tear what you can by hand...");
        }

        if (dexterityImpaired)
        {
            var abilityContext = AbilityContext.FromFullContext(
                ctx.player, ctx.Inventory, ctx.player.CurrentLocation, ctx.GameTime.Hour);

            if (abilityContext.DarknessLevel > 0.5 && !abilityContext.HasLightSource)
                GameDisplay.AddWarning(ctx, "The darkness makes your cuts imprecise, wasting some of the meat.");
            else if (abilityContext.WetnessPct > 0.3)
                GameDisplay.AddWarning(ctx, "Your wet, slippery hands waste some of the meat.");
            else
                GameDisplay.AddWarning(ctx, "Your unsteady hands waste some of the meat.");
        }

        // Harvest from carcass
        var yield = carcass.Harvest(adjustedTime, hasCuttingTool, dexterityImpaired, mode);

        // Apply Bloody effect
        double bloodySeverity = modeConfig.BloodySeverity * (adjustedTime / 60.0);
        bloodySeverity = Math.Min(0.5, bloodySeverity);
        if (bloodySeverity > 0.05)
        {
            ctx.player.EffectRegistry.AddEffect(EffectFactory.Bloody(bloodySeverity));
        }

        // Increase carcass scent
        carcass.ScentIntensityBonus += modeConfig.ScentIncrease;

        // Add to inventory
        var collected = new List<string>();
        if (!yield.IsEmpty)
        {
            var leftovers = InventoryCapacityHelper.CombineAndReport(ctx, yield);
            var taken = CalculateTaken(yield, leftovers);

            if (!taken.IsEmpty)
            {
                collected.Add(taken.GetDescription());
            }

            if (!leftovers.IsEmpty)
            {
                carcass.RestoreYields(leftovers);
            }
        }

        // Build result message
        string resultMessage;
        if (carcass.IsCompletelyButchered)
        {
            resultMessage = $"You've finished butchering the {carcass.AnimalName}.";
            ctx.CurrentLocation.RemoveFeature(carcass);
        }
        else
        {
            double progressPct = 1.0 - (carcass.GetTotalRemainingKg() / (carcass.BodyWeightKg * 0.78));
            progressPct = Math.Clamp(progressPct, 0, 1);
            resultMessage = $"Butchering progress: {progressPct:P0}. ~{carcass.GetRemainingMinutes()} min remaining.";
        }

        if (carcass.DecayLevel > 0.5 && !carcass.IsCompletelyButchered)
        {
            resultMessage += " The meat is starting to spoil.";
        }

        GameDisplay.AddSuccess(ctx, resultMessage);
        if (collected.Count > 0)
        {
            GameDisplay.AddSuccess(ctx, "Collected: " + string.Join(", ", collected));
        }

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    private static Inventory CalculateTaken(Inventory yield, Inventory leftovers)
    {
        var taken = new Inventory();

        foreach (Resource type in Enum.GetValues<Resource>())
        {
            var yieldItems = yield[type].ToList();
            var leftoverItems = leftovers[type].ToList();
            int takenCount = yieldItems.Count - leftoverItems.Count;

            for (int i = 0; i < takenCount; i++)
            {
                taken.Add(type, yieldItems[i]);
            }
        }

        return taken;
    }

    [HttpPost("cancel")]
    public ActionResult<GameResponse> Cancel(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("close")]
    public ActionResult<GameResponse> Close(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    private static ButcherDto BuildButcherData(GameContext ctx, CarcassFeature carcass)
    {
        var warnings = new List<string>();

        // Check dexterity impairment
        double dexterity = AbilityCalculator.GetDexterity(ctx.player, ctx);
        if (dexterity < 0.7)
        {
            warnings.Add("Your unsteady hands will waste some yield.");
        }

        // Check for cutting tool
        if (!ctx.Inventory.HasCuttingTool)
        {
            warnings.Add("Without a cutting tool, you'll only get meat and bone.");
        }

        // Frozen warning
        if (carcass.IsFrozen)
        {
            warnings.Add("The carcass is frozen solid. This will take longer.");
        }

        // Build mode options
        var modeOptions = new List<ButcherModeDto>
        {
            new(
                "quick",
                "Quick Strip",
                "Fast, meat-focused, messy - more scent",
                carcass.GetRemainingMinutes(ButcheringMode.QuickStrip)
            ),
            new(
                "careful",
                "Careful",
                "Full yields - meat, hide, bone, sinew, fat",
                carcass.GetRemainingMinutes(ButcheringMode.Careful)
            ),
            new(
                "full",
                "Full Processing",
                "+10% meat/fat, +20% sinew, less mess",
                carcass.GetRemainingMinutes(ButcheringMode.FullProcessing)
            )
        };

        return new ButcherDto(
            AnimalName: carcass.AnimalName,
            DecayStatus: carcass.GetDecayDescription(),
            RemainingKg: carcass.GetTotalRemainingKg(),
            IsFrozen: carcass.IsFrozen,
            ModeOptions: modeOptions,
            Warnings: warnings
        );
    }
}
