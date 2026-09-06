using text_survival.Items;

namespace text_survival.Crafting;

/// <summary>Named transformations of owned equipment, without a separate component inventory.</summary>
public static class GearCrafting
{
    public const double SharpeningCap = 0.8;

    public static IEnumerable<CraftOption> Options(Inventory inventory, NeedCraftingSystem crafting)
    {
        foreach (var target in CraftInputs.OwnedGear(inventory).Distinct())
        {
            // Clothing repair applies to the selected garment, including unequipped clothing.
            if (target.Category == GearCategory.Equipment && target.Slot is { } slot)
            {
                var mend = crafting.AllOptions.FirstOrDefault(o => o.MendSlot == slot);
                if (mend != null)
                    yield return Action(target, mend, "mend", "Maintain", $"Mend {target.Name}", mend.Description,
                        mend.Requirements, mend.RequiredTools, mend.CraftingTimeMinutes,
                        () =>
                        {
                            var repaired = target.Copy();
                            repaired.Repair(target.MaxDurability / 2);
                            return repaired;
                        },
                        () => target.MaxDurability <= 0 || target.ConditionPct >= 1 ? "This garment does not need mending" : null);
                continue;
            }

            var design = FindDesign(target, crafting);
            if (design == null) continue;
            if (design.FamilyId == "cutting")
            {
                if (design.Method == "Quick edge")
                {
                    var knife = crafting.AllOptions.Single(o => o.Id == "stone-knife");
                    yield return Action(target, knife, "handle", "Improve", $"Add handle to {target.Name}",
                        "Keep the existing edge and add a grip. The edge keeps its current proportion of condition.",
                        [new(Resource.Stick, 1), new(Resource.PlantFiber, 1)], [], 12,
                        () => Create(knife, Math.Max(1, (int)Math.Floor(knife.Durability * target.ConditionPct)), target),
                        () => target.IsBroken ? "A shattered edge cannot be given a handle" : null);
                    continue;
                }

                yield return Action(target, design, "sharpen", "Maintain", $"Sharpen {target.Name}",
                    "Restore a serviceable edge, up to 80% condition. Replace the edge to restore it fully.",
                    [], [ToolType.KnappingStone], 10,
                    () =>
                    {
                        var sharpened = target.Copy();
                        sharpened.Durability = Math.Max(target.Durability, (int)Math.Floor(target.MaxDurability * SharpeningCap));
                        return sharpened;
                    },
                    () => target.IsBroken ? "The edge is broken; replace it" :
                        target.Durability == -1 || target.Durability >= (int)Math.Floor(target.MaxDurability * SharpeningCap)
                            ? "The edge is already serviceable" : null);

                foreach (var replacement in crafting.AllOptions.Where(o => o.FamilyId == "cutting" && o.Method == "Handled knife"))
                {
                    // Retain the handle; replacing the edge still needs its material and a new binding.
                    var requirements = replacement.Requirements.Where(r => r.Material is not MaterialSpecifier.Specific(Resource.Stick)).ToList();
                    yield return Action(target, replacement, "refit", "Improve", $"Refit {target.Name}: {replacement.Name}",
                        "Keep the handle. Fit a new edge and binding, restoring full condition.",
                        requirements, replacement.RequiredTools, Math.Max(10, replacement.CraftingTimeMinutes - 5),
                        () => Create(replacement, replacement.Durability, target),
                        () => design.Id == replacement.Id && target.ConditionPct >= 1 ? "This edge is already new" : null);
                }
            }
            else if (design.FamilyId == "spear")
            {
                foreach (var replacement in crafting.AllOptions.Where(o => o.Id is "stone-tipped-spear" or "ivory-tipped-spear"))
                {
                    var requirements = replacement.Requirements.Where(r => r.Material is not MaterialSpecifier.Category(ResourceCategory.Log)).ToList();
                    yield return Action(target, replacement, "point", "Improve", $"Fit point to {target.Name}: {replacement.Name}",
                        "Retain the serviceable shaft and fit a new point and binding.",
                        requirements, replacement.RequiredTools, Math.Max(15, replacement.CraftingTimeMinutes - 10),
                        () => Create(replacement, Math.Max(1, (int)Math.Floor(replacement.Durability * target.ConditionPct)), target),
                        () => target.IsBroken ? "The shaft is broken; make a new spear" :
                            design.Id == replacement.Id ? "This spear already has that point" : null);
                }
            }
        }
    }

    public static CraftOption? FindDesign(Gear target, NeedCraftingSystem crafting)
    {
        if (target.DesignId != null) return crafting.AllOptions.FirstOrDefault(o => o.Id == target.DesignId && o.GearFactory != null);
        // Conservative compatibility for legacy saves. Customized/reward gear does not inherit arbitrary upgrades.
        return crafting.AllOptions.FirstOrDefault(o => o.Name == target.Name && o.GearFactory != null &&
            o.GearFactory(o.Durability) is { } original && original.ToolType == target.ToolType &&
            original.MaxDurability == target.MaxDurability && original.Damage == target.Damage);
    }

    private static Gear Create(CraftOption recipe, int durability, Gear target)
    {
        // Factories set maximum from the supplied durability; create at authored maximum, then apply condition.
        var result = recipe.GearFactory!(recipe.Durability);
        result.Durability = durability;
        result.DesignId = recipe.Id;
        result.InstanceId = target.InstanceId;
        result.ResinTreatmentDurability = target.ResinTreatmentDurability;
        return result;
    }

    private static CraftOption Action(Gear target, CraftOption design, string operation, string method,
        string name, string description, List<MaterialRequirement> requirements, List<ToolType> tools,
        int minutes, Func<Gear> result, Func<string?> blocker) => new()
    {
        Id = $"{operation}:{target.InstanceId}:{design.Id}", FamilyId = design.FamilyId, Method = method,
        Name = name, Description = description, Category = design.Category,
        CraftingTimeMinutes = minutes, Durability = design.Durability,
        Requirements = requirements, RequiredTools = tools,
        TargetGear = target, TargetResult = result, TargetBlocker = blocker
    };
}
