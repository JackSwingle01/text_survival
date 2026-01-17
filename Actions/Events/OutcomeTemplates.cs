using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Items;
using ShelterImprovementType = text_survival.Items.ShelterImprovementType;

namespace text_survival.Actions;

/// <summary>
/// Reusable outcome templates that reduce verbosity in event definitions.
/// Extension methods on EventResult for fluent chaining.
/// </summary>
public static class OutcomeTemplates
{
    public static EventResult MinorCold(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-5, 30));

    public static EventResult ModerateCold(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-12, 45));

    public static EventResult SevereCold(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-20, 60));

    public static EventResult LightChill(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-3, 20));

    public static EventResult HarshCold(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-15, 45));

    public static EventResult DangerousCold(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-18, 60));

    public static EventResult WithCold(this EventResult r, double degreesPerHour, int durationMinutes)
        => r.WithEffects(EffectFactory.Cold(degreesPerHour, durationMinutes));

    public static EventResult Unsettling(this EventResult r)
        => r.WithEffects(EffectFactory.Fear(0.2));

    public static EventResult Frightening(this EventResult r)
        => r.WithEffects(EffectFactory.Fear(0.3));

    public static EventResult Terrifying(this EventResult r)
        => r.WithEffects(EffectFactory.Fear(0.4));

    public static EventResult Panicking(this EventResult r)
        => r.WithEffects(EffectFactory.Fear(0.5));

    public static EventResult Shaken(this EventResult r)
        => r.WithEffects(EffectFactory.Shaken(0.15));

    // Damage uses 0-1 scale where 1.0 = destroys tissue layer

    public static EventResult MinorFall(this EventResult r)
        => r.Damage(0.08, DamageType.Blunt);

    public static EventResult ModerateFall(this EventResult r)
        => r.Damage(0.15, DamageType.Blunt);

    public static EventResult SeriousFall(this EventResult r)
        => r.Damage(0.25, DamageType.Blunt);

    public static EventResult MinorFrostbite(this EventResult r)
        => r.Damage(0.12, DamageType.Internal);

    public static EventResult ModerateFrostbite(this EventResult r)
        => r.Damage(0.25, DamageType.Internal);

    public static EventResult SevereFrostbite(this EventResult r)
        => r.Damage(0.40, DamageType.Internal);

    public static EventResult MinorBite(this EventResult r)
        => r.Damage(0.15, DamageType.Sharp)
           .WithEffects(EffectFactory.Fear(0.3));

    public static EventResult AnimalAttack(this EventResult r)
        => r.Damage(0.35, DamageType.Sharp)
           .WithEffects(EffectFactory.Fear(0.4));

    public static EventResult Mauled(this EventResult r)
        => r.Damage(0.55, DamageType.Sharp)
           .WithEffects(EffectFactory.Fear(0.5));

    public static EventResult DebrisDamage(this EventResult r, double amount = 0.12)
        => r.Damage(amount, DamageType.Blunt);

    public static EventResult ExposureDamage(this EventResult r, double amount = 0.10)
        => r.Damage(amount, DamageType.Internal);

    public static EventResult PredatorAttack(this EventResult r, double amount = 0.35)
        => r.Damage(amount, DamageType.Sharp);

    public static EventResult MinorBloody(this EventResult r)
        => r.WithEffects(EffectFactory.Bloody(0.1));

    public static EventResult ModerateBloody(this EventResult r)
        => r.WithEffects(EffectFactory.Bloody(0.25));

    public static EventResult HeavilyBloody(this EventResult r)
        => r.WithEffects(EffectFactory.Bloody(0.4));

    public static EventResult StartsFire(this EventResult r)
        => r.Costs(ResourceType.Tinder, 1).Costs(ResourceType.Fuel, 2);

    public static EventResult QuickFire(this EventResult r)
        => r.Costs(ResourceType.Fuel, 1);

    public static EventResult BurnsFuel(this EventResult r, int amount = 2)
        => r.Costs(ResourceType.Fuel, amount);

    public static EventResult WastesTinder(this EventResult r)
        => r.Costs(ResourceType.Tinder, 1);

    public static EventResult FindsSupplies(this EventResult r)
        => r.Rewards(RewardPool.BasicSupplies);

    public static EventResult FindsMeat(this EventResult r)
        => r.Rewards(RewardPool.BasicMeat);

    public static EventResult FindsLargeMeat(this EventResult r)
        => r.Rewards(RewardPool.LargeMeat);

    public static EventResult FindsMassiveMeat(this EventResult r)
        => r.Rewards(RewardPool.MassiveMeat);

    public static EventResult FindsCache(this EventResult r)
        => r.Rewards(RewardPool.HiddenCache);

    public static EventResult FindsGameTrail(this EventResult r)
        => r.Rewards(RewardPool.GameTrailDiscovery);

    public static EventResult BecomeStalked(this EventResult r, double severity, AnimalType? predator = null)
        => r.CreateTension("Stalked", severity, animalType: predator);

    public static EventResult EscalatesStalking(this EventResult r, double amount = 0.15)
        => r.Escalate("Stalked", amount);

    public static EventResult ResolvesStalking(this EventResult r)
        => r.ResolveTension("Stalked");

    public static EventResult EscalatesPack(this EventResult r, double amount = 0.15)
        => r.Escalate("PackNearby", amount);

    public static EventResult ResolvesPack(this EventResult r)
        => r.ResolveTension("PackNearby");

    public static EventResult MarksDiscovery(this EventResult r, string description, double severity = 0.5)
        => r.CreateTension("MarkedDiscovery", severity, description: description);

    public static EventResult MarksAnimalSign(this EventResult r, AnimalType animal, double severity = 0.4)
        => r.CreateTension("MarkedDiscovery", severity, animalType: animal, description: $"{animal.DisplayName().ToLower()} territory");

    public static EventResult ConfrontStalker(this EventResult r, AnimalType animal, int distance, double boldness)
        => r.ResolveTension("Stalked").Encounter(animal, distance, boldness);

    public static EventResult ConfrontPack(this EventResult r, AnimalType animal, int distance, double boldness)
        => r.ResolveTension("PackNearby").Encounter(animal, distance, boldness);

    public static EventResult EscalatesToHunted(this EventResult r, AnimalType? animal = null)
        => r.ResolveTension("Stalked").CreateTension("Hunted", 0.5, animalType: animal);

    public static EventResult EscapeToCamp(this EventResult r)
        => r.ResolveTension("Stalked").ResolveTension("PackNearby").Aborts();

    public static EventResult FireScaresPredator(this EventResult r, string tension = "Stalked", double reduction = 0.3)
        => r.BurnsFuel(2).Escalate(tension, -reduction);

    public static EventResult ColdAndFear(this EventResult r, double coldDegrees = -12, int coldMinutes = 45, double fear = 0.3)
        => r.WithEffects(EffectFactory.Cold(coldDegrees, coldMinutes), EffectFactory.Fear(fear));

    public static EventResult InjuredRetreat(this EventResult r, double damage = 0.10)
        => r.Damage(damage, DamageType.Blunt).Aborts();

    public static EventResult WithFrostbite(this EventResult r, double damage, double effectSeverity)
        => r.Damage(damage, DamageType.Internal)
           .WithEffects(EffectFactory.Frostbite(effectSeverity));

    public static EventResult StormExposure(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-10, 35));

    public static EventResult StormSheltered(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-2, 30));

    public static EventResult PartialShelter(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-8, 35));

    public static EventResult SoakedAndCold(this EventResult r, double wetness = 0.6, double coldDegrees = -12, int minutes = 40)
        => r.WithEffects(EffectFactory.Wet(wetness), EffectFactory.Cold(coldDegrees, minutes));

    public static EventResult StormBattered(this EventResult r)
        => r.WithEffects(EffectFactory.Cold(-15, 50), EffectFactory.Exhausted(0.3, 60));

    public static EventResult FellThroughIce(this EventResult r)
        => r.WithEffects(EffectFactory.Wet(0.9), EffectFactory.Cold(-20, 60))
           .Frightening();

    public static EventResult AddsShelter(this EventResult r,
        double temp, double overhead, double wind)
        => r.AddsFeature(typeof(Environments.Features.ShelterFeature), (temp, overhead, wind));

    public static EventResult CreatesCarcass(this EventResult r, AnimalType? animalType = null, double harvestedPct = 0, double ageHours = 0)
    {
        r.CarcassCreation = new CarcassCreation(animalType, harvestedPct, ageHours);
        return r;
    }

    public static EventResult DamagesEquipment(this EventResult r, EquipSlot slot, int durabilityLoss)
    {
        r.DamageGear = new GearDamage(GearCategory.Equipment, durabilityLoss, Slot: slot);
        return r;
    }

    public static EventResult RepairsEquipment(this EventResult r, EquipSlot slot, int durabilityGain)
    {
        r.RepairGear = new GearRepair(GearCategory.Equipment, durabilityGain, Slot: slot);
        return r;
    }

    public static EventResult DestroysEquipment(this EventResult r, EquipSlot slot)
    {
        // Use a very high damage value to ensure destruction
        r.DamageGear = new GearDamage(GearCategory.Equipment, 1000, Slot: slot);
        return r;
    }

    public static EventResult DamagesTool(this EventResult r, ToolType tool, int durabilityLoss)
    {
        r.DamageGear = new GearDamage(GearCategory.Tool, durabilityLoss, ToolType: tool);
        return r;
    }

    public static EventResult RepairsTool(this EventResult r, ToolType tool, int durabilityGain)
    {
        r.RepairGear = new GearRepair(GearCategory.Tool, durabilityGain, ToolType: tool);
        return r;
    }

    public static EventResult DestroysTool(this EventResult r, ToolType tool)
    {
        r.DamagesTool(tool, 99);
        return r;
    }

    public static EventResult MinorEquipmentWear(this EventResult r, EquipSlot slot)
        => r.DamagesEquipment(slot, 3);

    public static EventResult ModerateEquipmentWear(this EventResult r, EquipSlot slot)
        => r.DamagesEquipment(slot, 6);

    public static EventResult SevereEquipmentWear(this EventResult r, EquipSlot slot)
        => r.DamagesEquipment(slot, 10);

    public static EventResult FieldRepair(this EventResult r, EquipSlot slot)
        => r.RepairsEquipment(slot, 5);

    public static EventResult ProperRepair(this EventResult r, EquipSlot slot)
        => r.RepairsEquipment(slot, 10);

    public static EventResult DiscoversPredator(this EventResult r, AnimalType animal, double stalkSeverity = 0.3)
        => r.SpawnsHerd(animal, 1, 2).BecomeStalked(stalkSeverity, animal);

    public static EventResult DiscoversPack(this EventResult r, AnimalType animal, int count = 4, double severity = 0.4)
        => r.SpawnsHerd(animal, count, 3).CreateTension("PackNearby", severity, animalType: animal);

    public static EventResult DiscoversPreyHerd(this EventResult r, AnimalType animal, int count = 8, double severity = 0.5)
        => r.SpawnsHerd(animal, count, 4).CreateTension("HerdNearby", severity, animalType: animal);

    public static EventResult FollowsTracks(this EventResult r, AnimalType animal, bool isPredator, int count = 1)
    {
        r.SpawnsHerd(animal, count, isPredator ? 2 : 4);
        return isPredator
            ? r.BecomeStalked(0.2, animal)
            : r.CreateTension("HerdNearby", 0.4, animalType: animal);
    }

    public static EventResult ScavengersWaiting(this EventResult r, double severity = 0.4)
        => r.CreateTension("ScavengersWaiting", severity);

    public static EventResult EscalatesScavengers(this EventResult r, double amount = 0.15)
        => r.Escalate("ScavengersWaiting", amount);

    public static EventResult ResolvesScavengers(this EventResult r)
        => r.ResolveTension("ScavengersWaiting");

    public static EventResult ConfrontScavengers(this EventResult r, int distance, double boldness)
        => r.ResolveTension("ScavengersWaiting").Encounter(AnimalType.Hyena, distance, boldness);

    public static EventResult BecomeSaberToothStalked(this EventResult r, double severity)
        => r.CreateTension("SaberToothStalked", severity);

    public static EventResult EscalatesSaberTooth(this EventResult r, double amount = 0.2)
        => r.Escalate("SaberToothStalked", amount);

    public static EventResult ResolvesSaberTooth(this EventResult r)
        => r.ResolveTension("SaberToothStalked");

    public static EventResult ConfrontSaberTooth(this EventResult r, int distance, double boldness)
        => r.ResolveTension("SaberToothStalked").Encounter(AnimalType.SaberTooth, distance, boldness);

    public static EventResult MammothTracked(this EventResult r, double severity = 0.5)
        => r.CreateTension("MammothTracked", severity);

    public static EventResult EscalatesMammothTracking(this EventResult r, double amount = 0.2)
        => r.Escalate("MammothTracked", amount);

    public static EventResult ResolvesMammothTracking(this EventResult r)
        => r.ResolveTension("MammothTracked");

    public static EventResult AlertsHerd(this EventResult r, double alertLevel = 0.5)
    {
        r.HerdAlertLevel = alertLevel;
        return r;
    }

    public static EventResult TriggersHerdFlee(this EventResult r)
    {
        r.HerdFlees = true;
        return r;
    }

    public static EventResult KillsMammoth(this EventResult r)
    {
        r.MammothKilled = true;
        return r.CreatesCarcass(AnimalType.Mammoth)
               .CreateTension("FoodScentStrong", 0.8)
               .TriggersHerdFlee();
    }

    public static EventResult MammothCharge(this EventResult r)
        => r.Damage(0.80, Bodies.DamageType.Blunt)
           .Frightening()
           .TriggersHerdFlee();

    public static EventResult DamagesRoof(this EventResult r, double amount = 0.07)
        => r.DamagesShelter(ShelterImprovementType.Overhead, amount);

    public static EventResult DamagesWindBlock(this EventResult r, double amount = 0.07)
        => r.DamagesShelter(ShelterImprovementType.Wind, amount);

    public static EventResult DamagesInsulation(this EventResult r, double amount = 0.05)
        => r.DamagesShelter(ShelterImprovementType.Insulation, amount);

    public static EventResult StormDamagesShelter(this EventResult r)
        => r.DamagesShelter(ShelterImprovementType.Overhead, 0.10);

    public static EventResult MinorShelterWear(this EventResult r, ShelterImprovementType stat)
        => r.DamagesShelter(stat, 0.03);

    public static EventResult ModerateShelterDamage(this EventResult r, ShelterImprovementType stat)
        => r.DamagesShelter(stat, 0.08);

    public static EventResult SevereShelterDamage(this EventResult r, ShelterImprovementType stat)
        => r.DamagesShelter(stat, 0.15);
}
