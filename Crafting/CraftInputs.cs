using text_survival.Items;

namespace text_survival.Crafting;

/// <summary>Resolved input choices, shared by the preview and the eventual consumption.</summary>
public sealed class CraftInputs
{
    public Dictionary<Resource, int> Materials { get; } = [];
    public List<(Gear Tool, int Wear)> Tools { get; } = [];
    public List<string> Missing { get; } = [];
    public List<(MaterialRequirement Requirement, int Available)> Requirements { get; } = [];
    public bool Ready => Missing.Count == 0;
    private bool _consumed;

    public static CraftInputs Resolve(CraftOption option, Inventory inventory)
    {
        var result = new CraftInputs();
        // Reserve specific inputs first so overlapping categories cannot spend them twice.
        foreach (var req in option.Requirements.OrderBy(r => r.Material is MaterialSpecifier.Category))
        {
            IEnumerable<Resource> resources = req.Material switch
            {
                MaterialSpecifier.Specific(var r) => [r],
                MaterialSpecifier.Category(var c) => ResourceCategories.Items[c],
                _ => []
            };
            int remaining = req.Count;
            foreach (var resource in resources)
            {
                int available = inventory.Count(resource) - result.Materials.GetValueOrDefault(resource);
                int take = Math.Min(remaining, Math.Max(0, available));
                if (take > 0) result.Materials[resource] = result.Materials.GetValueOrDefault(resource) + take;
                remaining -= take;
            }
            result.Requirements.Add((req, req.Count - remaining));
            if (remaining > 0) result.Missing.Add($"{remaining} {MaterialName(req.Material)}");
        }
        foreach (var type in option.RequiredTools.Distinct())
        {
            var tool = OwnedGear(inventory).FirstOrDefault(g => g.ToolType == type && g.Works && g != option.TargetGear);
            int wear = option.ToolWear.GetValueOrDefault(type, 1);
            if (tool == null) result.Missing.Add($"Usable {type} (working tool)");
            else if (tool.Durability != -1 && tool.Durability < wear)
                result.Missing.Add($"{tool.Name} needs {wear} condition remaining");
            else result.Tools.Add((tool!, wear));
        }
        return result;
    }

    public bool CanConsume(Inventory inv) => !_consumed && Ready &&
        Materials.All(m => inv.Count(m.Key) >= m.Value) &&
        Tools.All(t => OwnedGear(inv).Contains(t.Tool) && t.Tool.Works &&
            (t.Tool.Durability == -1 || t.Tool.Durability >= t.Wear));

    public void Consume(Inventory inv)
    {
        if (!CanConsume(inv)) throw new InvalidOperationException("Crafting inputs are no longer available.");
        // All checks precede mutations. A resolved input set can only be consumed once.
        _consumed = true;
        foreach (var (resource, count) in Materials) inv.Remove(resource, count);
        foreach (var (tool, wear) in Tools)
            for (int i = 0; i < wear; i++) tool.Use();
    }

    public static IEnumerable<Gear> OwnedGear(Inventory inv)
    {
        if (inv.Weapon != null) yield return inv.Weapon;
        foreach (var gear in inv.Tools) yield return gear;
        foreach (var gear in inv.Accessories) yield return gear;
        foreach (var slot in Enum.GetValues<EquipSlot>())
            if (inv.GetEquipment(slot) is { } gear) yield return gear;
    }

    public static string MaterialName(MaterialSpecifier material) => material switch
    {
        MaterialSpecifier.Specific(var r) => r.ToDisplayName(),
        MaterialSpecifier.Category(var c) => c.ToString().ToLowerInvariant(),
        _ => "material"
    };
}
