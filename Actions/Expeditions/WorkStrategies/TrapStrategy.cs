using text_survival.Actions;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for setting and checking traps.
/// Mode determines whether to set new traps or check existing ones.
/// Both impaired by manipulation capacity (handling trap mechanisms).
/// </summary>
public class TrapStrategy : IWorkStrategy
{
    public enum TrapMode { Set, Check }

    private readonly TrapMode _mode;
    private Gear? _selectedSnare;
    private BaitType _selectedBait;
    private double _injuryChance;

    public TrapStrategy(TrapMode mode)
    {
        _mode = mode;
    }

    public string? ValidateLocation(GameContext ctx, Location location)
    {
        if (_mode == TrapMode.Set)
        {
            // Validate location has animal territory
            var territory = location.GetFeature<AnimalTerritoryFeature>();
            if (territory == null)
                return "No game trails here. Snares need animal territory.";

            // Get available snares from inventory
            var snares = ctx.Inventory.Tools.Where(t => t.ToolType == ToolType.Snare && t.Works).ToList();
            if (snares.Count == 0)
                return "You don't have any snares to set.";

            // Select which snare to use
            if (snares.Count == 1)
            {
                _selectedSnare = snares[0];
            }
            else
            {
                GameDisplay.Render(ctx, statusText: "Planning.");
                var snareChoice = new Choice<Gear>("Which snare do you want to set?");
                foreach (var snare in snares)
                {
                    string durability = snare.Durability > 0 ? $"{snare.Durability} uses" : "unlimited";
                    snareChoice.AddOption($"{snare.Name} ({durability})", snare);
                }
                _selectedSnare = snareChoice.GetPlayerChoice(ctx);
            }

            // Ask about bait
            GameDisplay.Render(ctx, statusText: "Planning.");
            var baitChoice = new Choice<BaitType>("Do you want to bait the snare?");
            baitChoice.AddOption("No bait", BaitType.None);

            if (ctx.Inventory.Count(Resource.RawMeat) > 0 || ctx.Inventory.Count(Resource.CookedMeat) > 0)
                baitChoice.AddOption("Use meat (strong attraction, decays faster)", BaitType.Meat);
            if (ctx.Inventory.Count(Resource.Berries) > 0)
                baitChoice.AddOption("Use berries (moderate attraction)", BaitType.Berries);

            _selectedBait = baitChoice.GetPlayerChoice(ctx);

            // Consume bait
            if (_selectedBait == BaitType.Meat)
            {
                if (ctx.Inventory.Count(Resource.RawMeat) > 0)
                    ctx.Inventory.Pop(Resource.RawMeat);
                else
                    ctx.Inventory.Pop(Resource.CookedMeat);
            }
            else if (_selectedBait == BaitType.Berries)
            {
                ctx.Inventory.Pop(Resource.Berries);
            }
        }
        else // Check mode
        {
            // Use feature's CanBeChecked property
            var snareLine = location.GetFeature<SnareLineFeature>();
            if (snareLine?.CanBeChecked != true)
                return "No snares set here.";
        }

        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        // Trap work has fixed time - no player choice
        return null;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        // Use Dexterity which combines manipulation, wetness, darkness, vitality
        double dexterity = AbilityCalculator.GetDexterity(ctx.player, ctx);

        if (_mode == TrapMode.Set)
        {
            baseTime = 10; // Setting time
            _injuryChance = 0.05; // Base 5% injury chance

            if (dexterity < 0.7)
            {
                // Scale time penalty: dexterity 0.7 = no penalty, 0.0 = +50% time
                double timePenalty = 1.0 + ((0.7 - dexterity) / 0.7 * 0.5);
                baseTime = (int)(baseTime * timePenalty);
                // Injury chance increases as dexterity drops
                _injuryChance += 0.10 * (1.0 - dexterity);

                // Get context for warnings
                var abilityContext = AbilityContext.FromFullContext(
                    ctx.player, ctx.Inventory, ctx.player.CurrentLocation, ctx.GameTime.Hour);

                // Contextual warning
                if (abilityContext.DarknessLevel > 0.5 && !abilityContext.HasLightSource)
                    return (baseTime, ["The darkness makes setting the snare difficult."]);
                else if (abilityContext.WetnessPct > 0.3)
                    return (baseTime, ["Your wet hands make setting the snare difficult."]);
                else
                    return (baseTime, ["Your clumsy hands make setting the snare difficult."]);
            }
        }
        else // Check mode
        {
            var snareLine = location.GetFeature<SnareLineFeature>();
            baseTime = 5 + (snareLine!.SnareCount * 3); // Base time + per-snare check time
            _injuryChance = 0.03; // Lower base since just checking

            if (dexterity < 0.7)
            {
                double timePenalty = 1.0 + ((0.7 - dexterity) / 0.7 * 0.4);
                baseTime = (int)(baseTime * timePenalty);
                _injuryChance += 0.08 * (1.0 - dexterity);

                // Get context for warnings
                var abilityContext = AbilityContext.FromFullContext(
                    ctx.player, ctx.Inventory, ctx.player.CurrentLocation, ctx.GameTime.Hour);

                // Contextual warning
                if (abilityContext.DarknessLevel > 0.5 && !abilityContext.HasLightSource)
                    return (baseTime, ["The darkness makes checking the traps difficult."]);
                else if (abilityContext.WetnessPct > 0.3)
                    return (baseTime, ["Your wet hands make checking the traps difficult."]);
                else
                    return (baseTime, ["Your clumsy hands make checking the traps difficult."]);
            }
        }

        return (baseTime, []);
    }

    public ActivityType GetActivityType() => ActivityType.Foraging;

    public string GetActivityName() => _mode == TrapMode.Set ? "setting trap" : "checking traps";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        if (_mode == TrapMode.Set)
            return ExecuteSet(ctx, location, actualTime);
        else
            return ExecuteCheck(ctx, location, actualTime);
    }

    private WorkResult ExecuteSet(GameContext ctx, Location location, int actualTime)
    {
        string resultMessage;
        bool injured = false;

        // Check for trap injury
        if (Utils.DetermineSuccess(_injuryChance))
        {
            ctx.player.Body.Damage(new Bodies.DamageInfo(3, Bodies.DamageType.Sharp));
            injured = true;
        }

        // Get or create SnareLineFeature at this location
        var territory = location.GetFeature<AnimalTerritoryFeature>()!;
        var snareLine = location.GetFeature<SnareLineFeature>();
        if (snareLine == null)
        {
            snareLine = new SnareLineFeature(territory);
            location.AddFeature(snareLine);
        }

        // Place the snare
        bool reinforced = _selectedSnare!.Name.Contains("Reinforced");
        if (_selectedBait != BaitType.None)
            snareLine.PlaceSnareWithBait(_selectedSnare.Durability, _selectedBait, reinforced);
        else
            snareLine.PlaceSnare(_selectedSnare.Durability, reinforced);

        // Remove snare from inventory
        ctx.Inventory.Tools.Remove(_selectedSnare);

        string baitMsg = _selectedBait != BaitType.None ? $" baited with {_selectedBait.ToString().ToLower()}" : "";
        resultMessage = $"Snare set{baitMsg}. Check back later.";
        if (injured)
            resultMessage = "The mechanism snapped and cut your fingers! " + resultMessage;

        var collected = new List<string> { $"Set {_selectedSnare.Name}" };

        // Show results in popup overlay
        WebIO.ShowWorkResult(ctx, "Setting Trap", resultMessage, collected);

        return new WorkResult(collected, null, actualTime, false);
    }

    private WorkResult ExecuteCheck(GameContext ctx, Location location, int actualTime)
    {
        bool injured = false;

        // Check for injury while handling traps
        if (Utils.DetermineSuccess(_injuryChance))
        {
            ctx.player.Body.Damage(new Bodies.DamageInfo(2, Bodies.DamageType.Sharp));
            injured = true;
        }

        // Collect results
        var snareLine = location.GetFeature<SnareLineFeature>()!;
        var results = snareLine.CheckAllSnares();
        var collected = new List<string>();
        var loot = new Inventory();
        int destroyed = 0;

        foreach (var result in results)
        {
            if (result.WasDestroyed)
            {
                destroyed++;
            }
            else if (result.WasStolen)
            {
                // Add partial remains (bones)
                loot.Add(Resource.Bone, 0.1);
                collected.Add($"Scraps ({result.AnimalType})");
            }
            else if (result.AnimalType != null)
            {
                // Add raw meat based on weight
                loot.Add(Resource.RawMeat, result.WeightKg * 0.5); // ~50% edible
                loot.Add(Resource.Bone, result.WeightKg * 0.1);
                if (result.WeightKg > 3)
                    loot.Add(Resource.Hide, result.WeightKg * 0.15);
                collected.Add($"{result.AnimalType} ({result.WeightKg:F1}kg)");
            }
        }

        if (collected.Count > 0)
            InventoryCapacityHelper.CombineAndReport(ctx, loot);

        // Build result message
        string resultMessage;
        int remaining = snareLine.SnareCount;

        if (collected.Count == 0)
            resultMessage = "Nothing caught yet.";
        else
            resultMessage = $"Catch! {collected.Count} animal(s) found.";

        if (destroyed > 0)
            resultMessage += $" {destroyed} snare(s) destroyed.";
        if (remaining > 0)
            resultMessage += $" {remaining} snare(s) still active.";
        else
            resultMessage += " No snares remain.";
        if (injured)
            resultMessage = "A snare caught your hand! " + resultMessage;

        // Show results in popup overlay
        WebIO.ShowWorkResult(ctx, "Checking Traps", resultMessage, collected);

        return new WorkResult(collected, null, actualTime, false);
    }
}
