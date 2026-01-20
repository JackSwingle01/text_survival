using text_survival.Actions;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Desktop;
using DesktopIO = text_survival.Desktop.DesktopIO;

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
    private List<string> _impairmentWarnings = [];

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

        // Check dexterity impairment (combines manipulation, wetness, darkness, vitality)
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
        if (_carcass.IsFrozen)
        {
            warnings.Add("The carcass is frozen solid. This will take longer.");
        }

        // Show overlay and get selection - pass carcass directly
        var selectedModeId = DesktopIO.SelectButcherOptions(ctx, _carcass, warnings);

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

        // Store impairment warnings for later use in Execute
        _impairmentWarnings = warnings;

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

        // Collect warnings for overlay display
        var warnings = new List<string>();

        // Determine tool and dexterity status
        bool hasCuttingTool = ctx.Inventory.HasCuttingTool;
        double dexterity = AbilityCalculator.GetDexterity(ctx.player, ctx);
        bool dexterityImpaired = dexterity < 0.7;

        // Warn if no cutting tool
        if (!hasCuttingTool)
        {
            warnings.Add("Without a cutting tool, you tear what you can by hand...");
        }

        // Warn if dexterity impaired (affects yield, not time here)
        if (dexterityImpaired)
        {
            // Get context for warnings
            var abilityContext = AbilityContext.FromFullContext(
                ctx.player, ctx.Inventory, ctx.player.CurrentLocation, ctx.GameTime.Hour);

            // Contextual warning
            if (abilityContext.DarknessLevel > 0.5 && !abilityContext.HasLightSource)
                warnings.Add("The darkness makes your cuts imprecise, wasting some of the meat.");
            else if (abilityContext.WetnessPct > 0.3)
                warnings.Add("Your wet, slippery hands waste some of the meat.");
            else
                warnings.Add("Your unsteady hands waste some of the meat.");
        }

        // Harvest from carcass with selected mode
        var yield = _carcass.Harvest(actualTime, hasCuttingTool, dexterityImpaired, mode);

        // Apply Bloody effect based on mode (messy modes = more blood)
        double bloodySeverity = modeConfig.BloodySeverity * (actualTime / 60.0);
        bloodySeverity = Math.Min(0.5, bloodySeverity);
        if (bloodySeverity > 0.05)
        {
            ctx.player.EffectRegistry.AddEffect(EffectFactory.Bloody(bloodySeverity));
        }

        // Increase carcass scent based on mode
        _carcass.ScentIntensityBonus += modeConfig.ScentIncrease;

        // Add to inventory - track what actually fits
        var collected = new List<string>();
        if (!yield.IsEmpty)
        {
            // Try to add to inventory, get back what doesn't fit
            var leftovers = InventoryCapacityHelper.CombineAndReport(ctx, yield);

            // Calculate what was actually taken (yield minus leftovers)
            var taken = CalculateTaken(yield, leftovers);

            if (!taken.IsEmpty)
            {
                collected.Add(taken.GetDescription());
            }

            // Restore leftovers back to carcass for future butchering
            if (!leftovers.IsEmpty)
            {
                _carcass.RestoreYields(leftovers);
            }
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
            warnings.Add("The meat is starting to spoil.");
        }

        // Combine impairment warnings with activity-specific warnings
        var allWarnings = new List<string>();
        allWarnings.AddRange(_impairmentWarnings);
        allWarnings.AddRange(warnings);

        // Show results in popup overlay
        DesktopIO.ShowWorkResult(ctx, "Butchering", resultMessage, collected, warnings: allWarnings);

        return new WorkResult(collected, null, actualTime, false);
    }

    /// <summary>
    /// Calculate what was actually taken from the yield (yield minus leftovers).
    /// Used to show accurate "collected" message when inventory is full.
    /// </summary>
    private static Inventory CalculateTaken(Inventory yield, Inventory leftovers)
    {
        var taken = new Inventory();

        foreach (Resource type in Enum.GetValues<Resource>())
        {
            // Get all items of this type from yield
            var yieldItems = yield[type].ToList();
            var leftoverItems = leftovers[type].ToList();

            // What was taken = yield items minus leftover items
            int takenCount = yieldItems.Count - leftoverItems.Count;
            for (int i = 0; i < takenCount; i++)
            {
                taken.Add(type, yieldItems[i]);
            }
        }

        return taken;
    }
}
