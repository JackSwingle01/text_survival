using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Items;

namespace text_survival.Actions;

/// <summary>
/// Reusable outcome templates that reduce verbosity in event definitions.
/// Extension methods on EventResult for fluent chaining.
/// </summary>
public static class OutcomeTemplates
{
    // === COLD/WEATHER ===

    /// <summary>Brief cold exposure. -5 degrees over 30 minutes.</summary>
    public static EventResult MinorCold(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-5, 30));

    /// <summary>Moderate cold exposure. -12 degrees over 45 minutes.</summary>
    public static EventResult ModerateCold(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-12, 45));

    /// <summary>Severe cold exposure. -20 degrees over 60 minutes.</summary>
    public static EventResult SevereCold(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-20, 60));

    /// <summary>Light chill. -3 degrees over 20 minutes.</summary>
    public static EventResult LightChill(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-3, 20));

    /// <summary>Harsh cold. -15 degrees over 45 minutes.</summary>
    public static EventResult HarshCold(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-15, 45));

    /// <summary>Dangerous cold exposure. -18 degrees over 60 minutes.</summary>
    public static EventResult DangerousCold(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-18, 60));

    /// <summary>Cold with customizable parameters.</summary>
    public static EventResult WithCold(this EventResult r, double degreesPerHour, int durationMinutes)
        => r.WithEffects(EffectFactory.Cold(degreesPerHour, durationMinutes));

    // === FEAR/PSYCHOLOGICAL ===

    /// <summary>Mild fear. 0.2 severity.</summary>
    public static EventResult Unsettling(this EventResult r)
        => r.WithEffects(EffectFactory.Fear(0.2));

    /// <summary>Moderate fear. 0.3 severity.</summary>
    public static EventResult Frightening(this EventResult r)
        => r.WithEffects(EffectFactory.Fear(0.3));

    /// <summary>Strong fear. 0.4 severity.</summary>
    public static EventResult Terrifying(this EventResult r)
        => r.WithEffects(EffectFactory.Fear(0.4));

    /// <summary>Extreme fear. 0.5 severity.</summary>
    public static EventResult Panicking(this EventResult r)
        => r.WithEffects(EffectFactory.Fear(0.5));

    /// <summary>Mild unease. Shaken 0.15 severity.</summary>
    public static EventResult Shaken(this EventResult r)
        => r.WithEffects(EffectFactory.Shaken(0.15));

    // === DAMAGE PATTERNS ===

    /// <summary>Minor fall damage. 3-4 blunt.</summary>
    public static EventResult MinorFall(this EventResult r)
        => r.Damage(3, DamageType.Blunt);

    /// <summary>Moderate fall damage. 5-6 blunt.</summary>
    public static EventResult ModerateFall(this EventResult r)
        => r.Damage(6, DamageType.Blunt);

    /// <summary>Serious fall damage. 8 blunt.</summary>
    public static EventResult SeriousFall(this EventResult r)
        => r.Damage(8, DamageType.Blunt);

    /// <summary>Minor frostbite. 3-5 internal damage.</summary>
    public static EventResult MinorFrostbite(this EventResult r)
        => r.Damage(4, DamageType.Internal);

    /// <summary>Moderate frostbite. 6-8 internal damage.</summary>
    public static EventResult ModerateFrostbite(this EventResult r)
        => r.Damage(8, DamageType.Internal);

    /// <summary>Severe frostbite. 10-12 internal damage.</summary>
    public static EventResult SevereFrostbite(this EventResult r)
        => r.Damage(12, DamageType.Internal);

    /// <summary>Minor animal bite. 6-8 sharp damage + fear.</summary>
    public static EventResult MinorBite(this EventResult r)
        => r.Damage(6, DamageType.Sharp)
           .WithEffects(EffectFactory.Fear(0.3));

    /// <summary>Serious animal attack. 10-12 sharp damage + fear.</summary>
    public static EventResult AnimalAttack(this EventResult r)
        => r.Damage(10, DamageType.Sharp)
           .WithEffects(EffectFactory.Fear(0.4));

    /// <summary>Severe mauling. 15+ sharp damage + strong fear.</summary>
    public static EventResult Mauled(this EventResult r)
        => r.Damage(15, DamageType.Sharp)
           .WithEffects(EffectFactory.Fear(0.5));

    /// <summary>Debris damage (from collapsing shelter, falling branches).</summary>
    public static EventResult DebrisDamage(this EventResult r, int amount = 5)
        => r.Damage(amount, DamageType.Blunt);

    /// <summary>Cold exposure damage (internal from hypothermia).</summary>
    public static EventResult ExposureDamage(this EventResult r, int amount = 3)
        => r.Damage(amount, DamageType.Internal);

    /// <summary>Predator attack damage (generic).</summary>
    public static EventResult PredatorAttack(this EventResult r, int amount = 10)
        => r.Damage(amount, DamageType.Sharp);

    // === RESOURCE COSTS ===

    /// <summary>Start a proper fire. 1 tinder + 2 fuel.</summary>
    public static EventResult StartsFire(this EventResult r)
        => r.Costs(ResourceType.Tinder, 1).Costs(ResourceType.Fuel, 2);

    /// <summary>Quick emergency fire. 1 fuel only.</summary>
    public static EventResult QuickFire(this EventResult r)
        => r.Costs(ResourceType.Fuel, 1);

    /// <summary>Burns fuel for warmth. Configurable amount.</summary>
    public static EventResult BurnsFuel(this EventResult r, int amount = 2)
        => r.Costs(ResourceType.Fuel, amount);

    /// <summary>Uses tinder (for failed fire attempts).</summary>
    public static EventResult WastesTinder(this EventResult r)
        => r.Costs(ResourceType.Tinder, 1);

    // === REWARDS ===

    /// <summary>Basic supplies find.</summary>
    public static EventResult FindsSupplies(this EventResult r)
        => r.Rewards(RewardPool.BasicSupplies);

    /// <summary>Basic meat find.</summary>
    public static EventResult FindsMeat(this EventResult r)
        => r.Rewards(RewardPool.BasicMeat);

    /// <summary>Large meat find.</summary>
    public static EventResult FindsLargeMeat(this EventResult r)
        => r.Rewards(RewardPool.LargeMeat);

    /// <summary>Hidden cache discovery.</summary>
    public static EventResult FindsCache(this EventResult r)
        => r.Rewards(RewardPool.HiddenCache);

    /// <summary>Game trail discovery.</summary>
    public static EventResult FindsGameTrail(this EventResult r)
        => r.Rewards(RewardPool.GameTrailDiscovery);

    // === TENSION OPERATIONS ===

    /// <summary>Begin being stalked by a predator.</summary>
    public static EventResult BecomeStalked(this EventResult r, double severity, string? predator = null)
        => r.CreateTension("Stalked", severity, animalType: predator);

    /// <summary>Increase stalking tension.</summary>
    public static EventResult EscalatesStalking(this EventResult r, double amount = 0.15)
        => r.Escalate("Stalked", amount);

    /// <summary>Resolve stalking tension (escaped or dealt with).</summary>
    public static EventResult ResolvesStalking(this EventResult r)
        => r.ResolveTension("Stalked");

    /// <summary>Increase pack tension.</summary>
    public static EventResult EscalatesPack(this EventResult r, double amount = 0.15)
        => r.Escalate("PackNearby", amount);

    /// <summary>Resolve pack tension.</summary>
    public static EventResult ResolvesPack(this EventResult r)
        => r.ResolveTension("PackNearby");

    // === COMPOUND PATTERNS ===

    /// <summary>Escape back to camp, resolving active threats.</summary>
    public static EventResult EscapeToCamp(this EventResult r)
        => r.ResolveTension("Stalked").ResolveTension("PackNearby").Aborts();

    /// <summary>Fire scares away predator, reducing tension.</summary>
    public static EventResult FireScaresPredator(this EventResult r, string tension = "Stalked", double reduction = 0.3)
        => r.BurnsFuel(2).Escalate(tension, -reduction);

    /// <summary>Cold exposure plus fear (caught in storm with threat).</summary>
    public static EventResult ColdAndFear(this EventResult r, double coldDegrees = -12, int coldMinutes = 45, double fear = 0.3)
        => r.WithEffects(EffectFactory.Cold(coldDegrees, coldMinutes), EffectFactory.Fear(fear));

    /// <summary>Minor injury + abort expedition.</summary>
    public static EventResult InjuredRetreat(this EventResult r, int damage = 4)
        => r.Damage(damage, DamageType.Blunt).Aborts();

    /// <summary>Frostbite damage with frostbite effect.</summary>
    public static EventResult WithFrostbite(this EventResult r, int damage, double effectSeverity)
        => r.Damage(damage, DamageType.Internal)
           .WithEffects(EffectFactory.Frostbite(effectSeverity));

    // === WEATHER-SPECIFIC ===

    /// <summary>Caught partially exposed in storm.</summary>
    public static EventResult StormExposure(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-10, 35));

    /// <summary>Good shelter found during storm.</summary>
    public static EventResult StormSheltered(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-2, 30));

    /// <summary>Partial shelter during storm.</summary>
    public static EventResult PartialShelter(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-8, 35));

    // === FEATURE CREATION ===

    /// <summary>
    /// Add a shelter with named parameters for clarity.
    /// temp = temperature insulation, overhead = overhead coverage, wind = wind coverage.
    /// </summary>
    public static EventResult AddsShelter(this EventResult r,
        double temp, double overhead, double wind)
        => r.AddsFeature(typeof(Environments.Features.ShelterFeature), (temp, overhead, wind));
}
