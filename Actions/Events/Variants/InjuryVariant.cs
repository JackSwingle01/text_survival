using text_survival.Bodies;
using text_survival.Effects;

namespace text_survival.Actions.Variants;

/// <summary>
/// Bundles coherent injury text with matched mechanics.
/// Ensures the event description matches where damage actually lands.
/// </summary>
public record InjuryVariant(
    BodyTarget Target,          // Where damage goes
    string Description,         // What player sees: "Your ankle twists on a hidden rock"
    DamageType Type,
    int Amount,
    Effect[]? Effects = null    // Optional auto-applied effects (e.g., SprainedAnkle, Dazed)
)
{
    /// <summary>
    /// Get a display name for the targeted body part.
    /// Useful for follow-up text: "A cut on your {partName}."
    /// </summary>
    public string GetDisplayName(Body body)
    {
        return Target switch
        {
            BodyTarget.AnyLeg => "leg",
            BodyTarget.AnyArm => "arm",
            BodyTarget.Head => "head",
            BodyTarget.Chest => "chest",
            BodyTarget.Abdomen => "side",
            BodyTarget.LeftArm or BodyTarget.RightArm => "arm",
            BodyTarget.LeftLeg or BodyTarget.RightLeg => "leg",
            _ => "body"
        };
    }
}

/// <summary>
/// Selects appropriate injury variants based on context.
/// Filters and weights variants by terrain, activity, and player state.
/// </summary>
public static class VariantSelector
{
    /// <summary>
    /// Select a general accident variant, weighted by context.
    /// </summary>
    public static InjuryVariant SelectAccidentVariant(GameContext ctx)
    {
        var pool = new List<(InjuryVariant variant, double weight)>();

        // Always include generic trip/stumble variants
        pool.AddRange(AccidentVariants.TripStumble.Select(v => (v, 1.0)));
        pool.AddRange(AccidentVariants.SharpHazards.Select(v => (v, 0.6)));

        // Add ice variants with higher weight if near frozen water
        if (HasIce(ctx))
            pool.AddRange(AccidentVariants.IceSlip.Select(v => (v, 2.0)));

        // Add rocky variants with higher weight on hazardous terrain
        if (ctx.CurrentLocation.TerrainHazardLevel > 0.4)
            pool.AddRange(AccidentVariants.RockyTerrain.Select(v => (v, 1.5)));

        return SelectWeighted(pool);
    }

    /// <summary>
    /// Select a slip/fall variant (for water crossings, ice, etc.)
    /// </summary>
    public static InjuryVariant SelectSlipVariant(GameContext ctx)
    {
        var pool = new List<(InjuryVariant variant, double weight)>();

        pool.AddRange(AccidentVariants.IceSlip.Select(v => (v, 1.0)));
        pool.AddRange(AccidentVariants.FallImpact.Select(v => (v, 1.0)));

        return SelectWeighted(pool);
    }

    /// <summary>
    /// Select a sprain variant (ankles, wrists from bad landings)
    /// </summary>
    public static InjuryVariant SelectSprainVariant(GameContext ctx)
    {
        return AccidentVariants.Sprains[Random.Shared.Next(AccidentVariants.Sprains.Length)];
    }

    /// <summary>
    /// Select a terrain-specific hazard variant
    /// </summary>
    public static InjuryVariant SelectTerrainVariant(GameContext ctx)
    {
        var pool = new List<(InjuryVariant variant, double weight)>();

        // Default pool
        pool.AddRange(AccidentVariants.RockyTerrain.Select(v => (v, 1.0)));

        // Ice variants if frozen water nearby
        if (HasIce(ctx))
            pool.AddRange(AccidentVariants.IceSlip.Select(v => (v, 2.0)));

        return SelectWeighted(pool);
    }

    /// <summary>
    /// Select a stumble/darkness variant (for dark passages)
    /// </summary>
    public static InjuryVariant SelectStumbleVariant(GameContext ctx)
    {
        var pool = new List<(InjuryVariant variant, double weight)>();

        pool.AddRange(AccidentVariants.TripStumble.Select(v => (v, 1.0)));
        pool.AddRange(AccidentVariants.DarknessStumble.Select(v => (v, 1.5)));

        return SelectWeighted(pool);
    }

    /// <summary>
    /// Select a travel injury variant based on terrain hazard type and severity.
    /// Used when quick travel through hazardous terrain results in injury.
    /// </summary>
    public static InjuryVariant SelectTravelInjuryVariant(GameContext ctx, Environments.Location location)
    {
        var pool = new List<(InjuryVariant variant, double weight)>();
        double severity = location.TerrainHazardLevel;

        // Determine primary hazard type
        // Note: Climb hazards now handled via edge events (EdgeEvents.ClimbingHazard)
        bool hasIce = HasIce(ctx);

        if (hasIce)
        {
            // Ice hazards - slips and impact
            pool.AddRange(AccidentVariants.IceSlip.Select(v => (v, 2.0)));
            if (severity >= 0.5)
                pool.AddRange(AccidentVariants.FallImpact.Select(v => (v, 1.0)));
            else
                pool.AddRange(AccidentVariants.TripStumble.Select(v => (v, 0.8)));
        }
        else
        {
            // Generic terrain hazards - scale with severity
            if (severity >= 0.7)
            {
                // High severity - serious injuries
                pool.AddRange(AccidentVariants.FallImpact.Select(v => (v, 1.5)));
                pool.AddRange(AccidentVariants.Sprains.Select(v => (v, 1.5)));
                pool.AddRange(AccidentVariants.RockyTerrain.Select(v => (v, 1.0)));
            }
            else if (severity >= 0.5)
            {
                // Moderate severity - mix of injuries
                pool.AddRange(AccidentVariants.RockyTerrain.Select(v => (v, 1.5)));
                pool.AddRange(AccidentVariants.Sprains.Select(v => (v, 1.0)));
                pool.AddRange(AccidentVariants.SharpHazards.Select(v => (v, 0.8)));
            }
            else
            {
                // Low severity - minor injuries
                pool.AddRange(AccidentVariants.TripStumble.Select(v => (v, 1.5)));
                pool.AddRange(AccidentVariants.SharpHazards.Select(v => (v, 0.8)));
                pool.AddRange(AccidentVariants.RockyTerrain.Select(v => (v, 0.6)));
            }
        }

        return SelectWeighted(pool);
    }

    // ========================================
    // WORK MISHAP SELECTORS
    // Activity-specific injury selection
    // ========================================

    /// <summary>
    /// Select a debris cut injury (searching ash piles, collapsed structures).
    /// </summary>
    public static InjuryVariant SelectDebrisInjury(GameContext ctx)
    {
        return AccidentVariants.DebrisCuts[Random.Shared.Next(AccidentVariants.DebrisCuts.Length)];
    }

    /// <summary>
    /// Select a vermin bite injury (rodent encounters).
    /// </summary>
    public static InjuryVariant SelectVerminBite(GameContext ctx)
    {
        return AccidentVariants.VerminBites[Random.Shared.Next(AccidentVariants.VerminBites.Length)];
    }

    /// <summary>
    /// Select a collapse injury (shelter/structure failure).
    /// </summary>
    public static InjuryVariant SelectCollapseInjury(GameContext ctx)
    {
        return AccidentVariants.CollapseInjuries[Random.Shared.Next(AccidentVariants.CollapseInjuries.Length)];
    }

    /// <summary>
    /// Select an ember burn injury (fire-tending mishaps).
    /// </summary>
    public static InjuryVariant SelectEmberBurn(GameContext ctx)
    {
        return AccidentVariants.EmberBurns[Random.Shared.Next(AccidentVariants.EmberBurns.Length)];
    }

    // ========================================
    // ENVIRONMENTAL INJURY SELECTORS
    // ========================================

    /// <summary>
    /// Select a frostbite injury variant.
    /// </summary>
    public static InjuryVariant SelectFrostbiteVariant(GameContext ctx)
    {
        return AccidentVariants.Frostbite[Random.Shared.Next(AccidentVariants.Frostbite.Length)];
    }

    /// <summary>
    /// Select a severe frostbite injury variant.
    /// </summary>
    public static InjuryVariant SelectSevereFrostbiteVariant(GameContext ctx)
    {
        return AccidentVariants.SevereFrostbite[Random.Shared.Next(AccidentVariants.SevereFrostbite.Length)];
    }

    /// <summary>
    /// Select a frostbite injury, severity based on context.
    /// Uses severe variant if body temp is very low or there's existing frostbite.
    /// </summary>
    public static InjuryVariant SelectFrostbiteByContext(GameContext ctx)
    {
        // Severe frostbite if extremely cold
        if (ctx.player.Body.BodyTemperature < 33)
            return SelectSevereFrostbiteVariant(ctx);

        return SelectFrostbiteVariant(ctx);
    }

    // ========================================
    // MUSCLE/STRAIN SELECTORS
    // ========================================

    /// <summary>
    /// Select a muscle cramp injury.
    /// </summary>
    public static InjuryVariant SelectMuscleCramp(GameContext ctx)
    {
        return AccidentVariants.MuscleCramp[Random.Shared.Next(AccidentVariants.MuscleCramp.Length)];
    }

    /// <summary>
    /// Select a muscle strain injury.
    /// </summary>
    public static InjuryVariant SelectMuscleStrain(GameContext ctx)
    {
        return AccidentVariants.MuscleStrain[Random.Shared.Next(AccidentVariants.MuscleStrain.Length)];
    }

    /// <summary>
    /// Select a muscle tear injury (severe).
    /// </summary>
    public static InjuryVariant SelectMuscleTear(GameContext ctx)
    {
        return AccidentVariants.MuscleTear[Random.Shared.Next(AccidentVariants.MuscleTear.Length)];
    }

    /// <summary>
    /// Select a muscle injury, severity based on context.
    /// Cramp for minor, strain for moderate, tear for severe.
    /// </summary>
    public static InjuryVariant SelectMuscleInjury(GameContext ctx, double severity = 0.5)
    {
        if (severity >= 0.8)
            return SelectMuscleTear(ctx);
        if (severity >= 0.5)
            return SelectMuscleStrain(ctx);
        return SelectMuscleCramp(ctx);
    }

    // ========================================
    // ANIMAL ENCOUNTER SELECTORS
    // ========================================

    /// <summary>
    /// Select a stampede injury variant.
    /// </summary>
    public static InjuryVariant SelectStampedeVariant(GameContext ctx)
    {
        return AccidentVariants.Stampede[Random.Shared.Next(AccidentVariants.Stampede.Length)];
    }

    /// <summary>
    /// Select a gore injury variant (horns, tusks).
    /// </summary>
    public static InjuryVariant SelectGoreVariant(GameContext ctx)
    {
        return AccidentVariants.Gore[Random.Shared.Next(AccidentVariants.Gore.Length)];
    }

    private static bool HasIce(GameContext ctx)
    {
        var water = ctx.CurrentLocation.GetFeature<Environments.Features.WaterFeature>();
        return water?.IsFrozen ?? false;
    }

    private static InjuryVariant SelectWeighted(List<(InjuryVariant variant, double weight)> pool)
    {
        if (pool.Count == 0)
            return AccidentVariants.TripStumble[0]; // Fallback

        var totalWeight = pool.Sum(p => p.weight);
        var roll = Random.Shared.NextDouble() * totalWeight;

        double cumulative = 0;
        foreach (var (variant, weight) in pool)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return variant;
        }

        return pool[^1].variant;
    }
}
