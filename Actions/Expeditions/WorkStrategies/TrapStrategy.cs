using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

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
        var capacities = ctx.player.GetCapacities();

        if (_mode == TrapMode.Set)
        {
            baseTime = 10; // Setting time
            _injuryChance = 0.05; // Base 5% injury chance

            if (AbilityCalculator.IsManipulationImpaired(capacities.Manipulation))
            {
                baseTime = (int)(baseTime * 1.25);
                _injuryChance += 0.10 * (1.0 - capacities.Manipulation);
                return (baseTime, ["Your clumsy hands make setting the snare difficult."]);
            }
        }
        else // Check mode
        {
            var snareLine = location.GetFeature<SnareLineFeature>();
            baseTime = 5 + (snareLine!.SnareCount * 3); // Base time + per-snare check time
            _injuryChance = 0.03; // Lower base since just checking

            if (AbilityCalculator.IsManipulationImpaired(capacities.Manipulation))
            {
                baseTime = (int)(baseTime * 1.20);
                _injuryChance += 0.08 * (1.0 - capacities.Manipulation);
                return (baseTime, ["Your clumsy hands make checking the traps difficult."]);
            }
        }

        return (baseTime, []);
    }

    public ActivityType GetActivityType() => ActivityType.Foraging;

    public string GetActivityName() => _mode == TrapMode.Set ? "setting trap" : "checking traps";

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        if (_mode == TrapMode.Set)
            return ExecuteSet(ctx, location, actualTime);
        else
            return ExecuteCheck(ctx, location, actualTime);
    }

    private WorkResult ExecuteSet(GameContext ctx, Location location, int actualTime)
    {
        GameDisplay.AddNarrative(ctx, "You find a promising game trail and set the snare...");

        // Check for trap injury
        if (Utils.DetermineSuccess(_injuryChance))
        {
            GameDisplay.AddWarning(ctx, "The snare mechanism snaps unexpectedly!");
            ctx.player.Body.Damage(new Bodies.DamageInfo(3, Bodies.DamageType.Sharp, "snare mechanism"));
            GameDisplay.AddNarrative(ctx, "You cut your fingers on the trap mechanism.");
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
        GameDisplay.AddSuccess(ctx, $"Snare set{baitMsg}. Check back later.");

        return new WorkResult([$"Set {_selectedSnare.Name}"], null, actualTime, false);
    }

    private WorkResult ExecuteCheck(GameContext ctx, Location location, int actualTime)
    {
        GameDisplay.AddNarrative(ctx, "You check your snare line...");

        // Check for injury while handling traps
        if (Utils.DetermineSuccess(_injuryChance))
        {
            GameDisplay.AddWarning(ctx, "A snare catches your hand!");
            ctx.player.Body.Damage(new Bodies.DamageInfo(2, Bodies.DamageType.Sharp, "snare"));
        }

        // Collect results
        var snareLine = location.GetFeature<SnareLineFeature>()!;
        var results = snareLine.CheckAllSnares();
        var collected = new List<string>();
        var loot = new Inventory();

        foreach (var result in results)
        {
            if (result.WasDestroyed)
            {
                GameDisplay.AddWarning(ctx, "One snare was destroyed - torn apart by something large.");
            }
            else if (result.WasStolen)
            {
                GameDisplay.AddNarrative(ctx, $"Something got here first. Only scraps of {result.AnimalType} remain.");
                // Add partial remains (bones)
                loot.Add(Resource.Bone, 0.1);
                collected.Add($"Scraps ({result.AnimalType})");
            }
            else if (result.AnimalType != null)
            {
                GameDisplay.AddSuccess(ctx, $"Catch! A {result.AnimalType} ({result.WeightKg:F1}kg).");
                // Add raw meat based on weight
                loot.Add(Resource.RawMeat, result.WeightKg * 0.5); // ~50% edible
                loot.Add(Resource.Bone, result.WeightKg * 0.1);
                if (result.WeightKg > 3)
                    loot.Add(Resource.Hide, result.WeightKg * 0.15);
                collected.Add($"{result.AnimalType} ({result.WeightKg:F1}kg)");
            }
        }

        if (collected.Count == 0)
        {
            GameDisplay.AddNarrative(ctx, "Nothing caught yet. The snares are still set.");
        }
        else
        {
            InventoryCapacityHelper.CombineAndReport(ctx, loot);
        }

        // Report remaining snares
        int remaining = snareLine.SnareCount;
        if (remaining > 0)
            GameDisplay.AddNarrative(ctx, $"{remaining} snare(s) still active.");
        else
            GameDisplay.AddNarrative(ctx, "No snares remain at this location.");

        return new WorkResult(collected, null, actualTime, false);
    }
}
