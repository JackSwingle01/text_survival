using text_survival.Bodies;
using text_survival.Effects;

namespace text_survival.Actions.Variants;

/// <summary>
/// Variant pools for accident/injury events.
/// Each variant bundles coherent text with matched mechanics.
/// </summary>
public static class AccidentVariants
{
    /// <summary>
    /// Generic trip and stumble injuries - suitable for most expeditions.
    /// </summary>
    public static readonly InjuryVariant[] TripStumble =
    [
        new(BodyTarget.AnyLeg, "Your foot catches on something hidden.", "stumble", DamageType.Blunt, 3),
        new(BodyTarget.AnyLeg, "Your ankle turns on uneven ground.", "twisted ankle", DamageType.Blunt, 3),
        new(BodyTarget.AnyLeg, "You stumble on a root buried under snow.", "trip", DamageType.Blunt, 3),
        new(BodyTarget.AnyArm, "You throw out your hands to catch yourself. Pain shoots through your wrist.", "fall impact", DamageType.Blunt, 3),
        new(BodyTarget.AnyArm, "You catch yourself hard. Your palm takes the worst of it.", "fall impact", DamageType.Blunt, 2),
        new(BodyTarget.Abdomen, "You land hard on your side.", "hard landing", DamageType.Blunt, 4),
        new(BodyTarget.Chest, "The impact drives the breath from your lungs.", "hard landing", DamageType.Blunt, 4),
    ];

    /// <summary>
    /// Sharp hazard injuries - cuts, scrapes from terrain.
    /// </summary>
    public static readonly InjuryVariant[] SharpHazards =
    [
        new(BodyTarget.AnyLeg, "A sharp rock tears through your legging.", "rock cut", DamageType.Sharp, 3),
        new(BodyTarget.AnyArm, "A hidden branch scrapes across your arm.", "branch scratch", DamageType.Sharp, 2),
        new(BodyTarget.AnyArm, "Your hand scrapes across frozen ground.", "scrape", DamageType.Sharp, 2),
        new(BodyTarget.Chest, "A jagged edge catches your chest.", "chest scrape", DamageType.Sharp, 3),
        new(BodyTarget.AnyLeg, "Something sharp slices your calf.", "cut", DamageType.Sharp, 4),
    ];

    /// <summary>
    /// Ice and slip injuries - for frozen terrain.
    /// </summary>
    public static readonly InjuryVariant[] IceSlip =
    [
        new(BodyTarget.AnyLeg, "The ice gives way under your weight.", "ice break", DamageType.Blunt, 4),
        new(BodyTarget.AnyLeg, "Your foot slides on icy rock.", "slip", DamageType.Blunt, 3),
        new(BodyTarget.Abdomen, "Your feet shoot out from under you. You land hard on your hip.", "hard landing", DamageType.Blunt, 5),
        new(BodyTarget.AnyArm, "You catch yourself but wrench your wrist.", "wrist strain", DamageType.Blunt, 4),
        new(BodyTarget.Head, "Your head strikes the ice.", "head impact", DamageType.Blunt, 4,
            [EffectFactory.Dazed(0.2)]),
    ];

    /// <summary>
    /// Rocky terrain injuries - for hazardous/mountainous areas.
    /// </summary>
    public static readonly InjuryVariant[] RockyTerrain =
    [
        new(BodyTarget.AnyLeg, "Loose scree shifts under your foot.", "rockslide", DamageType.Blunt, 4),
        new(BodyTarget.AnyArm, "You grab for a handhold. The rock is sharper than expected.", "rock cut", DamageType.Sharp, 3),
        new(BodyTarget.AnyLeg, "A rock rolls under your boot.", "misstep", DamageType.Blunt, 3),
        new(BodyTarget.Chest, "You scrape against rough stone.", "rock scrape", DamageType.Sharp, 3),
        new(BodyTarget.Head, "A rock dislodges from above.", "falling rock", DamageType.Blunt, 5,
            [EffectFactory.Dazed(0.3)]),
    ];

    /// <summary>
    /// Fall impact injuries - for serious falls.
    /// </summary>
    public static readonly InjuryVariant[] FallImpact =
    [
        new(BodyTarget.AnyLeg, "You land badly. Pain shoots up your leg.", "bad landing", DamageType.Blunt, 5),
        new(BodyTarget.Abdomen, "You hit the ground hard. The impact winds you.", "hard fall", DamageType.Blunt, 6),
        new(BodyTarget.AnyArm, "Your arm takes the brunt of the fall.", "fall impact", DamageType.Blunt, 5),
        new(BodyTarget.Chest, "You slam into the ground chest-first.", "hard fall", DamageType.Blunt, 6),
        new(BodyTarget.Head, "Your head strikes something solid. Stars explode.", "head impact", DamageType.Blunt, 5,
            [EffectFactory.Dazed(0.4)]),
    ];

    /// <summary>
    /// Sprain injuries - twisted joints from bad landings.
    /// </summary>
    public static readonly InjuryVariant[] Sprains =
    [
        new(BodyTarget.AnyLeg, "Your ankle twists badly. Something's wrong.", "twisted ankle", DamageType.Blunt, 3,
            [EffectFactory.SprainedAnkle(0.4)]),
        new(BodyTarget.AnyLeg, "Pain flares in your knee as it bends the wrong way.", "twisted knee", DamageType.Blunt, 4,
            [EffectFactory.SprainedAnkle(0.5)]),
        new(BodyTarget.AnyArm, "Your wrist bends back too far. The pain is immediate.", "wrist sprain", DamageType.Blunt, 3,
            [EffectFactory.Clumsy(0.3, 60)]),
    ];

    /// <summary>
    /// Darkness stumble injuries - for dark passages and night travel.
    /// </summary>
    public static readonly InjuryVariant[] DarknessStumble =
    [
        new(BodyTarget.AnyLeg, "Your shin connects with something hard in the darkness.", "blind impact", DamageType.Blunt, 4),
        new(BodyTarget.Head, "You walk straight into something. Stars burst behind your eyes.", "blind impact", DamageType.Blunt, 4,
            [EffectFactory.Dazed(0.25)]),
        new(BodyTarget.AnyArm, "Your hand finds something sharp you couldn't see.", "blind cut", DamageType.Sharp, 3),
        new(BodyTarget.Chest, "You collide with an unseen obstacle.", "blind impact", DamageType.Blunt, 4),
        new(BodyTarget.AnyLeg, "You step into nothing. Your leg buckles.", "misstep in dark", DamageType.Blunt, 4),
    ];

    /// <summary>
    /// Climbing injuries - for locations with climb risk.
    /// Higher severity than general terrain hazards.
    /// </summary>
    public static readonly InjuryVariant[] ClimbingFall =
    [
        new(BodyTarget.AnyLeg, "Your foothold crumbles. You drop hard onto a ledge below.", "climbing fall", DamageType.Blunt, 6,
            [EffectFactory.SprainedAnkle(0.5)]),
        new(BodyTarget.AnyArm, "Your grip fails. You catch yourself but wrench your shoulder.", "climbing strain", DamageType.Blunt, 5,
            [EffectFactory.Clumsy(0.4, 90)]),
        new(BodyTarget.AnyArm, "A handhold breaks off in your grip. You scramble for purchase.", "handhold failure", DamageType.Sharp, 4),
        new(BodyTarget.Chest, "You slide down rough rock face before stopping yourself.", "rock scrape", DamageType.Sharp, 5),
        new(BodyTarget.Head, "Your head strikes the cliff face as you slip.", "head strike", DamageType.Blunt, 6,
            [EffectFactory.Dazed(0.5)]),
        new(BodyTarget.AnyLeg, "You land badly on a narrow ledge. Something in your knee gives.", "bad landing", DamageType.Blunt, 6,
            [EffectFactory.SprainedAnkle(0.6)]),
        new(BodyTarget.Abdomen, "You swing into the rock face. The impact drives air from your lungs.", "impact", DamageType.Blunt, 5),
    ];

    // ========================================
    // WORK MISHAP POOLS
    // Activity-specific injuries during camp work
    // ========================================

    /// <summary>
    /// Debris cut injuries - from searching ash piles, collapsed structures.
    /// </summary>
    public static readonly InjuryVariant[] DebrisCuts =
    [
        new(BodyTarget.AnyArm, "A shard of bone slices your palm.", "bone cut", DamageType.Sharp, 2),
        new(BodyTarget.AnyLeg, "Jagged wood splinter catches your calf.", "splinter", DamageType.Sharp, 3),
        new(BodyTarget.AnyArm, "Charred debris edge cuts your hand.", "debris cut", DamageType.Sharp, 2),
        new(BodyTarget.AnyArm, "Something hidden in the ash opens your finger.", "ash cut", DamageType.Sharp, 2),
        new(BodyTarget.AnyArm, "A broken edge catches your wrist.", "debris cut", DamageType.Sharp, 3),
    ];

    /// <summary>
    /// Vermin bite injuries - from rodent/pest encounters.
    /// </summary>
    public static readonly InjuryVariant[] VerminBites =
    [
        new(BodyTarget.AnyArm, "Rat teeth sink into your hand.", "rat bite", DamageType.Pierce, 3),
        new(BodyTarget.AnyArm, "Small teeth puncture your finger.", "rodent bite", DamageType.Pierce, 2),
        new(BodyTarget.AnyLeg, "A stoat bites your leg as you corner it.", "stoat bite", DamageType.Pierce, 3),
        new(BodyTarget.AnyArm, "It bites before you can pull away.", "vermin bite", DamageType.Pierce, 2),
        new(BodyTarget.AnyArm, "Quick teeth find your hand. Small but sharp.", "rodent bite", DamageType.Pierce, 2),
    ];

    /// <summary>
    /// Collapse injuries - from shelter/structure failure.
    /// More severe than typical work injuries.
    /// </summary>
    public static readonly InjuryVariant[] CollapseInjuries =
    [
        new(BodyTarget.AnyArm, "Logs strike your shoulder as the structure gives way.", "collapse impact", DamageType.Blunt, 5,
            [EffectFactory.Clumsy(0.3, 60)]),
        new(BodyTarget.Head, "Debris catches your head. Stars burst.", "falling debris", DamageType.Blunt, 5,
            [EffectFactory.Dazed(0.4)]),
        new(BodyTarget.AnyLeg, "Snow buries your leg as the roof fails.", "snow burial", DamageType.Blunt, 4,
            [EffectFactory.SprainedAnkle(0.3)]),
        new(BodyTarget.Chest, "The structure comes down on you. Something cracks.", "collapse", DamageType.Blunt, 6),
        new(BodyTarget.AnyArm, "Your arm takes the worst of it.", "collapse impact", DamageType.Blunt, 5),
    ];

    /// <summary>
    /// Ember burn injuries - from fire-tending mishaps.
    /// </summary>
    public static readonly InjuryVariant[] EmberBurns =
    [
        new(BodyTarget.AnyArm, "An ember pops onto your hand.", "ember burn", DamageType.Burn, 2),
        new(BodyTarget.AnyArm, "Hot ash scatters across your arm.", "ash burn", DamageType.Burn, 3),
        new(BodyTarget.AnyArm, "You reach too close. Heat sears your skin.", "fire burn", DamageType.Burn, 3),
        new(BodyTarget.AnyArm, "A log shifts. Sparks catch your sleeve.", "spark burn", DamageType.Burn, 2),
        new(BodyTarget.AnyArm, "The fire spits. Your hand pays for it.", "ember burn", DamageType.Burn, 2),
    ];
}
