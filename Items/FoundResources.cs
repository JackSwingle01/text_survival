namespace text_survival.Items;

/// <summary>
/// Transfer object for resources found during foraging, harvesting, hunting, etc.
/// Returned by resource generation features, consumed by Inventory.Add().
/// </summary>
public class FoundResources
{
    // Fire supplies - each entry is item weight in kg
    public List<double> Logs { get; set; } = [];
    public List<double> Sticks { get; set; } = [];
    public List<double> Tinder { get; set; } = [];

    // Food - each entry is portion weight in kg
    public List<double> CookedMeat { get; set; } = [];
    public List<double> RawMeat { get; set; } = [];
    public List<double> Berries { get; set; } = [];

    // Water in liters
    public double WaterLiters { get; set; }

    // Discrete items
    public List<Tool> Tools { get; set; } = [];
    public List<Item> Special { get; set; } = [];

    // Narrative descriptions for expedition log ("a heavy log", "some berries")
    public List<string> Descriptions { get; set; } = [];

    // Convenience check
    public bool IsEmpty =>
        Logs.Count == 0 &&
        Sticks.Count == 0 &&
        Tinder.Count == 0 &&
        CookedMeat.Count == 0 &&
        RawMeat.Count == 0 &&
        Berries.Count == 0 &&
        WaterLiters == 0 &&
        Tools.Count == 0 &&
        Special.Count == 0;

    public double TotalWeightKg =>
        Logs.Sum() + Sticks.Sum() + Tinder.Sum() +
        CookedMeat.Sum() + RawMeat.Sum() + Berries.Sum() +
        WaterLiters +
        Tools.Sum(t => t.Weight) +
        Special.Sum(i => i.Weight);

    // Builder methods for fluent resource creation

    public FoundResources AddLog(double weightKg, string? description = null)
    {
        Logs.Add(weightKg);
        Descriptions.Add(description ?? DescribeLog(weightKg));
        return this;
    }

    public FoundResources AddStick(double weightKg, string? description = null)
    {
        Sticks.Add(weightKg);
        Descriptions.Add(description ?? DescribeStick(weightKg));
        return this;
    }

    public FoundResources AddTinder(double weightKg, string? description = null)
    {
        Tinder.Add(weightKg);
        Descriptions.Add(description ?? "some dry tinder");
        return this;
    }

    public FoundResources AddBerries(double weightKg, string? description = null)
    {
        Berries.Add(weightKg);
        Descriptions.Add(description ?? DescribeBerries(weightKg));
        return this;
    }

    public FoundResources AddRawMeat(double weightKg, string? description = null)
    {
        RawMeat.Add(weightKg);
        Descriptions.Add(description ?? DescribeMeat(weightKg));
        return this;
    }

    public FoundResources AddWater(double liters, string? description = null)
    {
        WaterLiters += liters;
        Descriptions.Add(description ?? $"{liters:F1}L of water");
        return this;
    }

    public FoundResources AddTool(Tool tool, string? description = null)
    {
        Tools.Add(tool);
        Descriptions.Add(description ?? $"a {tool.Name}");
        return this;
    }

    public FoundResources AddSpecial(Item item, string? description = null)
    {
        Special.Add(item);
        Descriptions.Add(description ?? $"a {item.Name}");
        return this;
    }

    // Description helpers
    private static string DescribeLog(double kg) => kg switch
    {
        < 1.5 => "a small log",
        < 2.5 => "a decent log",
        < 3.5 => "a heavy log",
        _ => "a massive log"
    };

    private static string DescribeStick(double kg) => kg switch
    {
        < 0.2 => "some twigs",
        < 0.4 => "a sturdy stick",
        _ => "a thick branch"
    };

    private static string DescribeBerries(double kg) => kg switch
    {
        < 0.1 => "a handful of berries",
        < 0.3 => "some berries",
        _ => "a good amount of berries"
    };

    private static string DescribeMeat(double kg) => kg switch
    {
        < 0.3 => "a small cut of meat",
        < 0.6 => "a portion of meat",
        _ => "a large piece of meat"
    };
}
