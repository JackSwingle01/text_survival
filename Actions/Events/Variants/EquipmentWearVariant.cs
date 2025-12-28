using text_survival.Items;

namespace text_survival.Actions.Variants;

/// <summary>
/// Bundles equipment wear description with matched mechanics.
/// Ensures the event description matches the equipment actually failing.
/// </summary>
public record EquipmentWearVariant(
    string Description,         // What player sees: "Your boot sole is separating"
    EquipSlot? Slot,            // For equipment (null for tools)
    ToolType? Tool,             // For tools (null for equipment)
    string RepairHint           // "Hide and sinew could fix this"
);

/// <summary>
/// Predefined equipment wear variant pools.
/// </summary>
public static class EquipmentWearVariants
{
    // === BOOT WEAR ===
    public static readonly EquipmentWearVariant[] BootWear =
    [
        new("Your boot sole is separating from the upper. Each step lets in cold.",
            EquipSlot.Feet, null, "Hide and sinew could patch this."),
        new("The stitching on your boot has unraveled. Your foot shifts with each step.",
            EquipSlot.Feet, null, "Sinew and a needle would fix the stitching."),
        new("A crack has split across your boot. Water seeps through.",
            EquipSlot.Feet, null, "Fat or resin could seal the crack."),
        new("The binding on your foot wrap is frayed. It's coming loose.",
            EquipSlot.Feet, null, "Plant fiber to rebind it."),
        new("Your boot insulation is compressed and thin. Cold creeps in.",
            EquipSlot.Feet, null, "Fresh moss or hide would restore warmth.")
    ];

    // === GLOVE WEAR ===
    public static readonly EquipmentWearVariant[] GloveWear =
    [
        new("Your gloves are threadbare at the fingertips. Cold seeps through.",
            EquipSlot.Hands, null, "Plant fiber could wrap the worn spots."),
        new("A seam on your glove has split. Your fingers are exposed.",
            EquipSlot.Hands, null, "Sinew to restitch the seam."),
        new("The palm of your glove is worn smooth. Grip is compromised.",
            EquipSlot.Hands, null, "Hide scrap to patch the palm."),
        new("Your hand wraps are unraveling at the wrist.",
            EquipSlot.Hands, null, "Any fiber to rebind them.")
    ];

    // === CHEST WEAR ===
    public static readonly EquipmentWearVariant[] ChestWear =
    [
        new("You feel cold air where you shouldn't. Your chest wrap has torn.",
            EquipSlot.Chest, null, "Hide to patch the tear."),
        new("The shoulder seam on your outer layer is splitting.",
            EquipSlot.Chest, null, "Sinew and patience to restitch."),
        new("Your chest insulation is matted and thin. Wind cuts through.",
            EquipSlot.Chest, null, "Additional hide or fur padding."),
        new("A hole has worn through your chest covering. Cold finds it instantly.",
            EquipSlot.Chest, null, "Hide scrap to cover the hole.")
    ];

    // === HEAD WEAR ===
    public static readonly EquipmentWearVariant[] HeadWear =
    [
        new("Your head covering is coming apart at the ear flaps.",
            EquipSlot.Head, null, "Sinew to reattach the pieces."),
        new("The insulation in your cap has compressed. Your ears ache.",
            EquipSlot.Head, null, "Fresh moss or fur padding."),
        new("Your head wrap keeps slipping. The binding is worn.",
            EquipSlot.Head, null, "New fiber to secure it properly.")
    ];

    // === BLADE WEAR (KNIFE/AXE) ===
    public static readonly EquipmentWearVariant[] BladeWear =
    [
        new("Your knife edge is dull. It tears more than it cuts.",
            null, ToolType.Knife, "Stone to resharpen the edge."),
        new("The blade on your knife has nicks. Each cut requires more force.",
            null, ToolType.Knife, "A smooth stone to work out the nicks."),
        new("Your axe head is loose on the handle. It wobbles with each swing.",
            null, ToolType.Axe, "Wedge it tighter, or reshape the handle."),
        new("The axe edge is blunted. Chopping takes twice the effort.",
            null, ToolType.Axe, "Stone to restore the edge.")
    ];

    // === FIRESTARTER WEAR ===
    public static readonly EquipmentWearVariant[] FirestarterWear =
    [
        new("Your hand drill spindle is nearly worn through. A few more uses at most.",
            null, ToolType.HandDrill, "Carve a new spindle before this one fails."),
        new("Your bow drill's fireboard is gouged out. Finding purchase is harder.",
            null, ToolType.BowDrill, "A fresh fireboard notch, or a new board."),
        new("Your striker stone is chipped and dull. Sparks come reluctantly.",
            null, ToolType.FireStriker, "A new striking edge, or a better pyrite.")
    ];

    // === SPEAR WEAR ===
    public static readonly EquipmentWearVariant[] SpearWear =
    [
        new("Your spear point is chipped. Penetration will suffer.",
            null, ToolType.Spear, "Stone or bone to reshape the point."),
        new("The binding on your spear head is fraying. The point wobbles.",
            null, ToolType.Spear, "Fresh sinew to secure it."),
        new("Your spear shaft has developed a crack. It might not survive another impact.",
            null, ToolType.Spear, "Binding around the crack, or a new shaft.")
    ];
}

/// <summary>
/// Selects appropriate equipment wear variants based on context.
/// </summary>
public static class EquipmentWearSelector
{
    /// <summary>
    /// Select a boot wear variant.
    /// </summary>
    public static EquipmentWearVariant SelectBootWear(GameContext ctx)
        => EquipmentWearVariants.BootWear[Random.Shared.Next(EquipmentWearVariants.BootWear.Length)];

    /// <summary>
    /// Select a glove wear variant.
    /// </summary>
    public static EquipmentWearVariant SelectGloveWear(GameContext ctx)
        => EquipmentWearVariants.GloveWear[Random.Shared.Next(EquipmentWearVariants.GloveWear.Length)];

    /// <summary>
    /// Select a chest wear variant.
    /// </summary>
    public static EquipmentWearVariant SelectChestWear(GameContext ctx)
        => EquipmentWearVariants.ChestWear[Random.Shared.Next(EquipmentWearVariants.ChestWear.Length)];

    /// <summary>
    /// Select a head wear variant.
    /// </summary>
    public static EquipmentWearVariant SelectHeadWear(GameContext ctx)
        => EquipmentWearVariants.HeadWear[Random.Shared.Next(EquipmentWearVariants.HeadWear.Length)];

    /// <summary>
    /// Select a blade (knife/axe) wear variant.
    /// </summary>
    public static EquipmentWearVariant SelectBladeWear(GameContext ctx)
    {
        // Prefer knife if both present and worn
        var knife = ctx.Inventory.Tools.FirstOrDefault(t => t.ToolType == ToolType.Knife);
        var axe = ctx.Inventory.Tools.FirstOrDefault(t => t.ToolType == ToolType.Axe);

        var knifeVariants = EquipmentWearVariants.BladeWear.Where(v => v.Tool == ToolType.Knife).ToArray();
        var axeVariants = EquipmentWearVariants.BladeWear.Where(v => v.Tool == ToolType.Axe).ToArray();

        if (knife != null && knife.ConditionPct < 0.3)
            return knifeVariants[Random.Shared.Next(knifeVariants.Length)];
        if (axe != null && axe.ConditionPct < 0.3)
            return axeVariants[Random.Shared.Next(axeVariants.Length)];

        // Default to knife variants
        return knifeVariants[Random.Shared.Next(knifeVariants.Length)];
    }

    /// <summary>
    /// Select a firestarter wear variant based on what the player has.
    /// </summary>
    public static EquipmentWearVariant SelectFirestarterWear(GameContext ctx)
    {
        var pool = new List<EquipmentWearVariant>();

        foreach (var variant in EquipmentWearVariants.FirestarterWear)
        {
            var tool = ctx.Inventory.Tools.FirstOrDefault(t => t.ToolType == variant.Tool);
            if (tool != null && tool.ConditionPct < 0.25)
                pool.Add(variant);
        }

        if (pool.Count == 0)
            return EquipmentWearVariants.FirestarterWear[0]; // Fallback

        return pool[Random.Shared.Next(pool.Count)];
    }

    /// <summary>
    /// Select a spear wear variant.
    /// </summary>
    public static EquipmentWearVariant SelectSpearWear(GameContext ctx)
        => EquipmentWearVariants.SpearWear[Random.Shared.Next(EquipmentWearVariants.SpearWear.Length)];

    /// <summary>
    /// Select the most pressing equipment wear variant based on what's actually worn.
    /// Used when not targeting a specific slot.
    /// </summary>
    public static EquipmentWearVariant? SelectWorstEquipmentWear(GameContext ctx)
    {
        // Check each slot for worn equipment
        var worst = Situations.GetWorstEquipment(ctx);
        if (worst == null) return null;

        return worst.Value.Slot switch
        {
            EquipSlot.Feet => SelectBootWear(ctx),
            EquipSlot.Hands => SelectGloveWear(ctx),
            EquipSlot.Chest => SelectChestWear(ctx),
            EquipSlot.Head => SelectHeadWear(ctx),
            EquipSlot.Legs => SelectChestWear(ctx), // Use chest variants for legs
            _ => null
        };
    }

    /// <summary>
    /// Select the most pressing tool wear variant based on what's actually worn.
    /// </summary>
    public static EquipmentWearVariant? SelectWorstToolWear(GameContext ctx)
    {
        var worst = Situations.GetWorstTool(ctx);
        if (worst == null) return null;

        return worst.Value.Tool.ToolType switch
        {
            ToolType.Knife or ToolType.Axe => SelectBladeWear(ctx),
            ToolType.HandDrill or ToolType.BowDrill or ToolType.FireStriker => SelectFirestarterWear(ctx),
            ToolType.Spear => SelectSpearWear(ctx),
            _ => null
        };
    }
}
