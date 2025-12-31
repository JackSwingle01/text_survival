using text_survival.Actions;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web;
using text_survival.Web.Dto;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for butchering a carcass. Thin orchestration layer -
/// all butchering logic lives in CarcassFeature.
/// </summary>
public class ButcherStrategy : IWorkStrategy
{
    private readonly CarcassFeature _carcass;
    private ButcheringMode? _selectedMode;
    private int _selectedMinutes;
    private bool _cancelled;

    public ButcherStrategy(CarcassFeature carcass, ButcheringMode? mode = null)
    {
        _carcass = carcass;
        _selectedMode = mode;
    }

    public string? ValidateLocation(GameContext ctx, Location location)
    {
        if (_carcass.IsCompletelyButchered)
            return "There's nothing left to butcher.";

        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        // Build warnings
        var warnings = new List<string>();

        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        // Check manipulation impairment
        if (AbilityCalculator.IsManipulationImpaired(capacities.Manipulation))
        {
            warnings.Add("Your unsteady hands will waste some yield.");
        }

        // Check for cutting tool
        if (!ctx.Inventory.HasCuttingTool)
        {
            warnings.Add("Without a cutting tool, you'll only get meat and bone.");
        }

        // Frozen warning
        if (_carcass.IsFrozen)
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
                _carcass.GetRemainingMinutes(ButcheringMode.QuickStrip)
            ),
            new(
                "careful",
                "Careful",
                "Full yields - meat, hide, bone, sinew, fat",
                _carcass.GetRemainingMinutes(ButcheringMode.Careful)
            ),
            new(
                "full",
                "Full Processing",
                "+10% meat/fat, +20% sinew, less mess",
                _carcass.GetRemainingMinutes(ButcheringMode.FullProcessing)
            )
        };

        var butcherDto = new ButcherDto(
            AnimalName: _carcass.AnimalName,
            DecayStatus: _carcass.GetDecayDescription(),
            RemainingKg: _carcass.GetTotalRemainingKg(),
            IsFrozen: _carcass.IsFrozen,
            ModeOptions: modeOptions,
            Warnings: warnings
        );

        // Show overlay and get selection
        var selectedModeId = WebIO.SelectButcherOptions(ctx, butcherDto);

        if (selectedModeId == null)
        {
            _cancelled = true;
            return null;
        }

        // Parse mode selection
        _selectedMode = selectedModeId switch
        {
            "quick" => ButcheringMode.QuickStrip,
            "careful" => ButcheringMode.Careful,
            "full" => ButcheringMode.FullProcessing,
            _ => ButcheringMode.Careful
        };

        // Store selected time based on mode
        _selectedMinutes = _carcass.GetRemainingMinutes(_selectedMode.Value);

        return null; // Time already selected via overlay
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        // Use pre-selected time from overlay (baseTime will be 0 since GetTimeOptions returns null)
        int workTime = _selectedMinutes;

        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        // Butchering requires manipulation (steady hands for cuts)
        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkMoving: false,
            checkManipulation: true,
            effectRegistry: ctx.player.EffectRegistry
        );

        // Frozen carcass takes 50% longer to butcher
        if (_carcass.IsFrozen)
        {
            timeFactor *= 1.5;
            // Warning already shown in overlay, don't duplicate
        }

        return ((int)(workTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Butchering;

    public string GetActivityName() => "butchering";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        // Early return if user cancelled
        if (_cancelled)
            return new WorkResult([], null, 0, false);

        var mode = _selectedMode ?? ButcheringMode.Careful;
        var modeConfig = CarcassFeature.GetModeConfig(mode);

        // Determine tool and impairment status
        bool hasCuttingTool = ctx.Inventory.HasCuttingTool;
        var manipulation = ctx.player.GetCapacities().Manipulation;
        bool manipulationImpaired = AbilityCalculator.IsManipulationImpaired(manipulation);

        // Warn if no cutting tool
        if (!hasCuttingTool)
        {
            GameDisplay.AddWarning(ctx, "Without a cutting tool, you tear what you can by hand...");
        }

        // Warn if manipulation impaired (affects yield, not time here)
        if (manipulationImpaired)
        {
            GameDisplay.AddWarning(ctx, "Your unsteady hands waste some of the meat.");
        }

        // Harvest from carcass with selected mode
        var yield = _carcass.Harvest(actualTime, hasCuttingTool, manipulationImpaired, mode);

        // Apply Bloody effect based on mode (messy modes = more blood)
        double bloodySeverity = modeConfig.BloodySeverity * (actualTime / 60.0);
        bloodySeverity = Math.Min(0.5, bloodySeverity);
        if (bloodySeverity > 0.05)
        {
            ctx.player.EffectRegistry.AddEffect(EffectFactory.Bloody(bloodySeverity));
        }

        // Increase carcass scent based on mode
        _carcass.ScentIntensityBonus += modeConfig.ScentIncrease;

        // Add to inventory
        var collected = new List<string>();
        if (!yield.IsEmpty)
        {
            collected.Add(yield.GetDescription());
            InventoryCapacityHelper.CombineAndReport(ctx, yield);
        }

        // Build result message
        string resultMessage;
        if (_carcass.IsCompletelyButchered)
        {
            resultMessage = $"You've finished butchering the {_carcass.AnimalName}.";

            // Remove carcass feature from location
            location.RemoveFeature(_carcass);
        }
        else
        {
            double progressPct = 1.0 - (_carcass.GetTotalRemainingKg() /
                (_carcass.BodyWeightKg * 0.78));  // Total yield is ~78% of body weight
            progressPct = Math.Clamp(progressPct, 0, 1);

            resultMessage = $"Butchering progress: {progressPct:P0}. ~{_carcass.GetRemainingMinutes()} min remaining.";
        }

        // Decay warning
        if (_carcass.DecayLevel > 0.5 && !_carcass.IsCompletelyButchered)
        {
            resultMessage += " The meat is starting to spoil.";
        }

        // Show results in popup overlay
        WebIO.ShowWorkResult(ctx, "Butchering", resultMessage, collected);

        return new WorkResult(collected, null, actualTime, false);
    }
}
