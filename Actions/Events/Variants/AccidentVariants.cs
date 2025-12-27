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
        new(BodyTarget.AnyLeg, "Your foot catches on something hidden.", DamageType.Blunt, 3),
        new(BodyTarget.AnyLeg, "Your ankle turns on uneven ground.", DamageType.Blunt, 3),
        new(BodyTarget.AnyLeg, "You stumble on a root buried under snow.", DamageType.Blunt, 3),
        new(BodyTarget.AnyArm, "You throw out your hands to catch yourself. Pain shoots through your wrist.", DamageType.Blunt, 3),
        new(BodyTarget.AnyArm, "You catch yourself hard. Your palm takes the worst of it.", DamageType.Blunt, 2),
        new(BodyTarget.Abdomen, "You land hard on your side.", DamageType.Blunt, 4),
        new(BodyTarget.Chest, "The impact drives the breath from your lungs.", DamageType.Blunt, 4),
    ];

    /// <summary>
    /// Sharp hazard injuries - cuts, scrapes from terrain.
    /// </summary>
    public static readonly InjuryVariant[] SharpHazards =
    [
        new(BodyTarget.AnyLeg, "A sharp rock tears through your legging.", DamageType.Sharp, 3),
        new(BodyTarget.AnyArm, "A hidden branch scrapes across your arm.", DamageType.Sharp, 2),
        new(BodyTarget.AnyArm, "Your hand scrapes across frozen ground.", DamageType.Sharp, 2),
        new(BodyTarget.Chest, "A jagged edge catches your chest.", DamageType.Sharp, 3),
        new(BodyTarget.AnyLeg, "Something sharp slices your calf.", DamageType.Sharp, 4),
    ];

    /// <summary>
    /// Ice and slip injuries - for frozen terrain.
    /// </summary>
    public static readonly InjuryVariant[] IceSlip =
    [
        new(BodyTarget.AnyLeg, "The ice gives way under your weight.", DamageType.Blunt, 4),
        new(BodyTarget.AnyLeg, "Your foot slides on icy rock.", DamageType.Blunt, 3),
        new(BodyTarget.Abdomen, "Your feet shoot out from under you. You land hard on your hip.", DamageType.Blunt, 5),
        new(BodyTarget.AnyArm, "You catch yourself but wrench your wrist.", DamageType.Blunt, 4),
        new(BodyTarget.Head, "Your head strikes the ice.", DamageType.Blunt, 4,
            [EffectFactory.Dazed(0.2)]),
    ];

    /// <summary>
    /// Rocky terrain injuries - for hazardous/mountainous areas.
    /// </summary>
    public static readonly InjuryVariant[] RockyTerrain =
    [
        new(BodyTarget.AnyLeg, "Loose scree shifts under your foot.", DamageType.Blunt, 4),
        new(BodyTarget.AnyArm, "You grab for a handhold. The rock is sharper than expected.", DamageType.Sharp, 3),
        new(BodyTarget.AnyLeg, "A rock rolls under your boot.", DamageType.Blunt, 3),
        new(BodyTarget.Chest, "You scrape against rough stone.", DamageType.Sharp, 3),
        new(BodyTarget.Head, "A rock dislodges from above.", DamageType.Blunt, 5,
            [EffectFactory.Dazed(0.3)]),
    ];

    /// <summary>
    /// Fall impact injuries - for serious falls.
    /// </summary>
    public static readonly InjuryVariant[] FallImpact =
    [
        new(BodyTarget.AnyLeg, "You land badly. Pain shoots up your leg.", DamageType.Blunt, 5),
        new(BodyTarget.Abdomen, "You hit the ground hard. The impact winds you.", DamageType.Blunt, 6),
        new(BodyTarget.AnyArm, "Your arm takes the brunt of the fall.", DamageType.Blunt, 5),
        new(BodyTarget.Chest, "You slam into the ground chest-first.", DamageType.Blunt, 6),
        new(BodyTarget.Head, "Your head strikes something solid. Stars explode.", DamageType.Blunt, 5,
            [EffectFactory.Dazed(0.4)]),
    ];

    /// <summary>
    /// Sprain injuries - twisted joints from bad landings.
    /// </summary>
    public static readonly InjuryVariant[] Sprains =
    [
        new(BodyTarget.AnyLeg, "Your ankle twists badly. Something's wrong.", DamageType.Blunt, 3,
            [EffectFactory.SprainedAnkle(0.4)]),
        new(BodyTarget.AnyLeg, "Pain flares in your knee as it bends the wrong way.", DamageType.Blunt, 4,
            [EffectFactory.SprainedAnkle(0.5)]),
        new(BodyTarget.AnyArm, "Your wrist bends back too far. The pain is immediate.", DamageType.Blunt, 3,
            [EffectFactory.Clumsy(0.3, 60)]),
    ];

    /// <summary>
    /// Darkness stumble injuries - for dark passages and night travel.
    /// </summary>
    public static readonly InjuryVariant[] DarknessStumble =
    [
        new(BodyTarget.AnyLeg, "Your shin connects with something hard in the darkness.", DamageType.Blunt, 4),
        new(BodyTarget.Head, "You walk straight into something. Stars burst behind your eyes.", DamageType.Blunt, 4,
            [EffectFactory.Dazed(0.25)]),
        new(BodyTarget.AnyArm, "Your hand finds something sharp you couldn't see.", DamageType.Sharp, 3),
        new(BodyTarget.Chest, "You collide with an unseen obstacle.", DamageType.Blunt, 4),
        new(BodyTarget.AnyLeg, "You step into nothing. Your leg buckles.", DamageType.Blunt, 4),
    ];

    /// <summary>
    /// Climbing injuries - for locations with climb risk.
    /// Higher severity than general terrain hazards.
    /// </summary>
    public static readonly InjuryVariant[] ClimbingFall =
    [
        new(BodyTarget.AnyLeg, "Your foothold crumbles. You drop hard onto a ledge below.", DamageType.Blunt, 6,
            [EffectFactory.SprainedAnkle(0.5)]),
        new(BodyTarget.AnyArm, "Your grip fails. You catch yourself but wrench your shoulder.", DamageType.Blunt, 5,
            [EffectFactory.Clumsy(0.4, 90)]),
        new(BodyTarget.AnyArm, "A handhold breaks off in your grip. You scramble for purchase.", DamageType.Sharp, 4),
        new(BodyTarget.Chest, "You slide down rough rock face before stopping yourself.", DamageType.Sharp, 5),
        new(BodyTarget.Head, "Your head strikes the cliff face as you slip.", DamageType.Blunt, 6,
            [EffectFactory.Dazed(0.5)]),
        new(BodyTarget.AnyLeg, "You land badly on a narrow ledge. Something in your knee gives.", DamageType.Blunt, 6,
            [EffectFactory.SprainedAnkle(0.6)]),
        new(BodyTarget.Abdomen, "You swing into the rock face. The impact drives air from your lungs.", DamageType.Blunt, 5),
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
        new(BodyTarget.AnyArm, "A shard of bone slices your palm.", DamageType.Sharp, 2),
        new(BodyTarget.AnyLeg, "Jagged wood splinter catches your calf.", DamageType.Sharp, 3),
        new(BodyTarget.AnyArm, "Charred debris edge cuts your hand.", DamageType.Sharp, 2),
        new(BodyTarget.AnyArm, "Something hidden in the ash opens your finger.", DamageType.Sharp, 2),
        new(BodyTarget.AnyArm, "A broken edge catches your wrist.", DamageType.Sharp, 3),
    ];

    /// <summary>
    /// Vermin bite injuries - from rodent/pest encounters.
    /// </summary>
    public static readonly InjuryVariant[] VerminBites =
    [
        new(BodyTarget.AnyArm, "Rat teeth sink into your hand.", DamageType.Pierce, 3),
        new(BodyTarget.AnyArm, "Small teeth puncture your finger.", DamageType.Pierce, 2),
        new(BodyTarget.AnyLeg, "A stoat bites your leg as you corner it.", DamageType.Pierce, 3),
        new(BodyTarget.AnyArm, "It bites before you can pull away.", DamageType.Pierce, 2),
        new(BodyTarget.AnyArm, "Quick teeth find your hand. Small but sharp.", DamageType.Pierce, 2),
    ];

    /// <summary>
    /// Collapse injuries - from shelter/structure failure.
    /// More severe than typical work injuries.
    /// </summary>
    public static readonly InjuryVariant[] CollapseInjuries =
    [
        new(BodyTarget.AnyArm, "Logs strike your shoulder as the structure gives way.", DamageType.Blunt, 5,
            [EffectFactory.Clumsy(0.3, 60)]),
        new(BodyTarget.Head, "Debris catches your head. Stars burst.", DamageType.Blunt, 5,
            [EffectFactory.Dazed(0.4)]),
        new(BodyTarget.AnyLeg, "Snow buries your leg as the roof fails.", DamageType.Blunt, 4,
            [EffectFactory.SprainedAnkle(0.3)]),
        new(BodyTarget.Chest, "The structure comes down on you. Something cracks.", DamageType.Blunt, 6),
        new(BodyTarget.AnyArm, "Your arm takes the worst of it.", DamageType.Blunt, 5),
    ];

    /// <summary>
    /// Ember burn injuries - from fire-tending mishaps.
    /// </summary>
    public static readonly InjuryVariant[] EmberBurns =
    [
        new(BodyTarget.AnyArm, "An ember pops onto your hand.", DamageType.Burn, 2),
        new(BodyTarget.AnyArm, "Hot ash scatters across your arm.", DamageType.Burn, 3),
        new(BodyTarget.AnyArm, "You reach too close. Heat sears your skin.", DamageType.Burn, 3),
        new(BodyTarget.AnyArm, "A log shifts. Sparks catch your sleeve.", DamageType.Burn, 2),
        new(BodyTarget.AnyArm, "The fire spits. Your hand pays for it.", DamageType.Burn, 2),
    ];

    // ========================================
    // ENVIRONMENTAL INJURY POOLS
    // Cold exposure and environmental hazards
    // ========================================

    /// <summary>
    /// Frostbite injuries - from severe cold exposure.
    /// Targets extremities most commonly affected.
    /// </summary>
    public static readonly InjuryVariant[] Frostbite =
    [
        new(BodyTarget.AnyArm, "Your fingers have gone white. The feeling's gone.", DamageType.Internal, 6),
        new(BodyTarget.AnyArm, "Waxy patches on your hands. Frostbite.", DamageType.Internal, 5),
        new(BodyTarget.AnyLeg, "Your toes stopped hurting. That's worse.", DamageType.Internal, 6),
        new(BodyTarget.AnyLeg, "Numbness spreads through your feet. The cold has done damage.", DamageType.Internal, 5),
        new(BodyTarget.Head, "Your ears are waxy-white. The tips have frozen.", DamageType.Internal, 4),
        new(BodyTarget.Head, "Your nose has gone numb. The skin looks wrong.", DamageType.Internal, 4),
    ];

    /// <summary>
    /// Severe frostbite - deep tissue damage from prolonged exposure.
    /// </summary>
    public static readonly InjuryVariant[] SevereFrostbite =
    [
        new(BodyTarget.AnyArm, "Your hand is a claw. Black patches at the fingertips.", DamageType.Internal, 10),
        new(BodyTarget.AnyLeg, "Your foot is dead weight. Deep frostbite has set in.", DamageType.Internal, 10),
        new(BodyTarget.AnyArm, "The skin on your fingers is hard. Like wood. This won't heal right.", DamageType.Internal, 8),
        new(BodyTarget.AnyLeg, "Blisters on your toes. The flesh beneath is dying.", DamageType.Internal, 8),
    ];

    // ========================================
    // MUSCLE/STRAIN INJURY POOLS
    // Overexertion and internal damage
    // ========================================

    /// <summary>
    /// Muscle strain injuries - from overexertion.
    /// </summary>
    public static readonly InjuryVariant[] MuscleStrain =
    [
        new(BodyTarget.AnyLeg, "Something pulls in your calf. Sharp and immediate.", DamageType.Internal, 4,
            [EffectFactory.Sore(0.3, 60)]),
        new(BodyTarget.AnyLeg, "Your thigh seizes. The muscle has been pushed too far.", DamageType.Internal, 5,
            [EffectFactory.Sore(0.4, 90)]),
        new(BodyTarget.Abdomen, "Pain lances through your back. Something's wrong in there.", DamageType.Internal, 5,
            [EffectFactory.Sore(0.4, 90)]),
        new(BodyTarget.AnyArm, "Your shoulder burns. The muscle is pulled tight.", DamageType.Internal, 4,
            [EffectFactory.Clumsy(0.2, 60)]),
        new(BodyTarget.Chest, "Sharp pain when you breathe. You've strained something in your chest.", DamageType.Internal, 4),
    ];

    /// <summary>
    /// Muscle cramp injuries - sudden seizing.
    /// </summary>
    public static readonly InjuryVariant[] MuscleCramp =
    [
        new(BodyTarget.AnyLeg, "Your calf locks up. The cramp is brutal.", DamageType.Internal, 3,
            [EffectFactory.Sore(0.2, 30)]),
        new(BodyTarget.AnyLeg, "Your thigh seizes without warning. You nearly fall.", DamageType.Internal, 4,
            [EffectFactory.Sore(0.3, 45)]),
        new(BodyTarget.AnyArm, "Your hand cramps into a fist. Getting it open hurts.", DamageType.Internal, 2,
            [EffectFactory.Clumsy(0.2, 30)]),
        new(BodyTarget.Abdomen, "A cramp knots your side. Movement makes it worse.", DamageType.Internal, 3),
    ];

    /// <summary>
    /// Muscle tear injuries - serious damage from overexertion or falls.
    /// </summary>
    public static readonly InjuryVariant[] MuscleTear =
    [
        new(BodyTarget.AnyLeg, "Something tears in your leg. A pop you felt more than heard.", DamageType.Internal, 8,
            [EffectFactory.SprainedAnkle(0.5)]),
        new(BodyTarget.AnyArm, "Your arm gives way. The muscle has torn.", DamageType.Internal, 7,
            [EffectFactory.Clumsy(0.5, 120)]),
        new(BodyTarget.Abdomen, "Fire in your back. Something's torn in there.", DamageType.Internal, 8,
            [EffectFactory.Sore(0.6, 180)]),
    ];

    // ========================================
    // ANIMAL ENCOUNTER POOLS
    // Stampede and large animal injuries
    // ========================================

    /// <summary>
    /// Stampede injuries - from herd encounters.
    /// </summary>
    public static readonly InjuryVariant[] Stampede =
    [
        new(BodyTarget.Chest, "A glancing blow from the stampede. You're knocked sprawling.", DamageType.Blunt, 6,
            [EffectFactory.Dazed(0.3)]),
        new(BodyTarget.AnyLeg, "Hooves clip your leg as the herd thunders past.", DamageType.Blunt, 7),
        new(BodyTarget.Abdomen, "You're hit. The herd sweeps past. You're lucky to be alive.", DamageType.Blunt, 8,
            [EffectFactory.Dazed(0.4)]),
        new(BodyTarget.AnyArm, "An antler catches your arm as you dive clear.", DamageType.Pierce, 5),
        new(BodyTarget.AnyLeg, "You're knocked down. A hoof grazes your thigh.", DamageType.Blunt, 6),
        new(BodyTarget.Head, "Something strikes your head. The world spins.", DamageType.Blunt, 7,
            [EffectFactory.Dazed(0.5)]),
    ];

    /// <summary>
    /// Gore injuries - from horned/tusked animals.
    /// </summary>
    public static readonly InjuryVariant[] Gore =
    [
        new(BodyTarget.Abdomen, "The horn catches your side. You feel it go deep.", DamageType.Pierce, 10),
        new(BodyTarget.AnyLeg, "Tusks rake your thigh. Blood flows freely.", DamageType.Pierce, 8),
        new(BodyTarget.Chest, "The point finds your chest. A glancing blow, but deep.", DamageType.Pierce, 9),
        new(BodyTarget.AnyArm, "You throw up your arm. The horn tears through it.", DamageType.Pierce, 7),
    ];
}
