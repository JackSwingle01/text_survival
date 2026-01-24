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
        // Mode selection (only if this is a fresh carcass - mode persists across sessions)
        if (_carcass.SelectedMode == null)
        {
            var warnings = BuildModeSelectionWarnings(ctx);
            var selectedModeId = DesktopIO.SelectButcherMode(ctx, _carcass, warnings);

            if (selectedModeId == null)
            {
                _cancelled = true;
                return null;
            }

            // Persist mode on carcass for all future sessions
            _carcass.SelectedMode = selectedModeId switch
            {
                "quick" => ButcheringMode.QuickStrip,
                "careful" => ButcheringMode.Careful,
                "full" => ButcheringMode.FullProcessing,
                _ => ButcheringMode.Careful
            };
        }

        _selectedMode = _carcass.SelectedMode.Value;

        // Time chunk selection (like ChoppingStrategy)
        double remainingMinutes = _carcass.GetRemainingMinutes(_selectedMode.Value);

        string progressText = _carcass.ProgressPct > 0.01
            ? $" ({_carcass.ProgressPct:P0} complete)"
            : "";

        var choice = new Choice<int>($"How long do you want to butcher?{progressText}");

        // Offer time chunks up to remaining time
        if (remainingMinutes >= 15)
            choice.AddOption("15 minutes", 15);
        if (remainingMinutes >= 30)
            choice.AddOption("30 minutes", 30);
        if (remainingMinutes >= 60)
            choice.AddOption("1 hour", 60);

        // If less than 15 minutes remain, or for convenience, offer to finish
        if (remainingMinutes > 0 && remainingMinutes < 15)
            choice.AddOption($"Finish ({(int)remainingMinutes} min)", (int)remainingMinutes);
        else if (remainingMinutes >= 15)
            choice.AddOption($"Finish ({(int)remainingMinutes} min)", (int)remainingMinutes);

        choice.AddOption("Cancel", 0);

        return choice;
    }

    private List<string> BuildModeSelectionWarnings(GameContext ctx)
    {
        var warnings = new List<string>();

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

        return warnings;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
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
            // Warning already shown in mode selection, don't duplicate
        }

        return ((int)(baseTime * timeFactor), warnings);
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
