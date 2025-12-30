using text_survival.Bodies;
using text_survival.Items;

namespace text_survival.Actors.Animals;

public static class AnimalFactory
{
    /// <summary>
    /// Create an animal from an AnimalType enum.
    /// </summary>
    public static Animal? FromType(AnimalType animalType)
    {
        return animalType switch
        {
            AnimalType.Caribou => MakeCaribou(),
            AnimalType.Rabbit => MakeRabbit(),
            AnimalType.Ptarmigan => MakePtarmigan(),
            AnimalType.Fox => MakeFox(),
            AnimalType.Wolf => MakeWolf(),
            AnimalType.Bear => MakeBear(),
            AnimalType.CaveBear => MakeCaveBear(),
            AnimalType.Hyena => MakeCaveHyena(),
            AnimalType.Mammoth => MakeWoollyMammoth(),
            AnimalType.SaberTooth => MakeSaberToothTiger(),
            AnimalType.Megaloceros => MakeMegaloceros(),
            AnimalType.Bison => MakeSteppeBison(),
            AnimalType.Rat => MakeRat(),
            AnimalType.Squirrel => MakeRat(),  // Placeholder - similar small game
            AnimalType.Grouse => MakePtarmigan(),  // Placeholder - similar bird
            AnimalType.Fish => null,  // Fish don't have combat stats
            _ => null
        };
    }

    /// <summary>
    /// Create an animal from a name string.
    /// Used for territories and event-based carcass creation.
    /// </summary>
    public static Animal? FromName(string animalName)
    {
        var animalType = AnimalTypes.Parse(animalName);
        return animalType.HasValue ? FromType(animalType.Value) : null;
    }

    public static Animal MakeRat()
    {
        var bodyStats = new BodyCreationInfo
        {
            type = BodyTypes.Quadruped,
            overallWeight = 2,
            fatPercent = 0.15,
            musclePercent = 0.40
        };

        // 0-1 damage scale: 0.05 = scratches, negligible
        var animal = new Animal("Rat", bodyStats, AnimalBehaviorType.Prey, AnimalSize.Small,
            attackDamage: 0.05, attackName: "teeth", attackType: DamageType.Pierce)
        {
            TrackingDifficulty = 3
        };
        animal.GenerateTraits();
        return animal;
    }

    public static Animal MakeWolf()
    {
        var bodyStats = new BodyCreationInfo
        {
            type = BodyTypes.Quadruped,
            overallWeight = 40,
            fatPercent = 0.20,
            musclePercent = 0.60
        };

        // 0-1 damage scale: 0.45 = 2 hits cripples limb, very dangerous
        var animal = new Animal("Wolf", bodyStats, AnimalBehaviorType.Predator, AnimalSize.Large,
            attackDamage: 0.45, attackName: "fangs", attackType: DamageType.Pierce,
            speedMps: 8.0, pursuitCommitment: 60.0,
            disengageAfterMaul: 0.2)  // Pack hunters tend to finish prey
        {
            TrackingDifficulty = 6
        };
        animal.GenerateTraits();
        return animal;
    }

    public static Animal MakeBear()
    {
        var bodyStats = new BodyCreationInfo
        {
            type = BodyTypes.Quadruped,
            overallWeight = 250,
            fatPercent = 0.30,
            musclePercent = 0.55
        };

        // 0-1 damage scale: 0.60 = 1-2 hits cripples limb, large predator
        var animal = new Animal("Bear", bodyStats, AnimalBehaviorType.Predator, AnimalSize.Large,
            attackDamage: 0.60, attackName: "claws", attackType: DamageType.Sharp,
            speedMps: 5.0, pursuitCommitment: 30.0,
            disengageAfterMaul: 0.5)  // Often leaves after incapacitating (territorial defense)
        {
            TrackingDifficulty = 5
        };
        animal.GenerateTraits();
        return animal;
    }

    public static Animal MakeCaveBear()
    {
        var bodyStats = new BodyCreationInfo
        {
            type = BodyTypes.Quadruped,
            overallWeight = 350,
            fatPercent = 0.35,
            musclePercent = 0.55
        };

        // 0-1 damage scale: 0.80 = 1 hit major wound, terrifying
        var animal = new Animal("Cave Bear", bodyStats, AnimalBehaviorType.Predator, AnimalSize.Large,
            attackDamage: 0.80, attackName: "massive claws", attackType: DamageType.Sharp,
            speedMps: 4.5, pursuitCommitment: 25.0,
            disengageAfterMaul: 0.5)  // Often leaves after incapacitating (territorial defense)
        {
            TrackingDifficulty = 4
        };
        animal.GenerateTraits();
        return animal;
    }

    public static Animal MakeWoollyMammoth()
    {
        var bodyStats = new BodyCreationInfo
        {
            type = BodyTypes.Quadruped,
            overallWeight = 6000,
            fatPercent = 0.35,
            musclePercent = 0.50
        };

        // 0-1 damage scale: 1.2 = 1 hit destroys limb, run or die
        var animal = new Animal("Woolly Mammoth", bodyStats, AnimalBehaviorType.DangerousPrey, AnimalSize.Large,
            attackDamage: 1.2, attackName: "tusks", attackType: DamageType.Pierce,
            disengageAfterMaul: 0.7)  // Defensive - just wants you to go away
        {
            TrackingDifficulty = 2,
            SpecialYields =
            [
                (Resource.Ivory, 4),        // 2 tusks
                (Resource.MammothHide, 15)  // Special thick hide
            ]
        };
        animal.GenerateTraits();
        return animal;
    }

    public static Animal MakeSaberToothTiger()
    {
        var bodyStats = new BodyCreationInfo
        {
            type = BodyTypes.Quadruped,
            overallWeight = 300,
            fatPercent = 0.10,
            musclePercent = 0.70
        };

        // 0-1 damage scale: 0.90 = 1 hit cripples/dying, apex predator
        var animal = new Animal("Saber-Tooth Tiger", bodyStats, AnimalBehaviorType.Predator, AnimalSize.Large,
            attackDamage: 0.90, attackName: "massive fangs", attackType: DamageType.Pierce,
            speedMps: 9.0, pursuitCommitment: 45.0,
            disengageAfterMaul: 0.15)  // Big cat, likely to finish kill
        {
            TrackingDifficulty = 7
        };
        animal.GenerateTraits();
        return animal;
    }

    public static Animal MakeCaribou()
    {
        var bodyStats = new BodyCreationInfo
        {
            type = BodyTypes.Quadruped,
            overallWeight = 120,
            fatPercent = 0.15,
            musclePercent = 0.60
        };

        // 0-1 damage scale: 0.25 = defensive bruising/gash
        var animal = new Animal("Caribou", bodyStats, AnimalBehaviorType.Prey, AnimalSize.Large,
            attackDamage: 0.25, attackName: "antlers", attackType: DamageType.Blunt,
            isHostile: false)
        {
            TrackingDifficulty = 4
        };
        animal.GenerateTraits();
        return animal;
    }

    public static Animal MakeRabbit()
    {
        var bodyStats = new BodyCreationInfo
        {
            type = BodyTypes.Quadruped,
            overallWeight = 2,
            fatPercent = 0.10,
            musclePercent = 0.50
        };

        // 0-1 damage scale: 0.02 = negligible, prey animal
        var animal = new Animal("Rabbit", bodyStats, AnimalBehaviorType.Prey, AnimalSize.Small,
            attackDamage: 0.02, attackName: "teeth", attackType: DamageType.Blunt,
            isHostile: false)
        {
            TrackingDifficulty = 6
        };
        animal.GenerateTraits();
        return animal;
    }

    public static Animal MakePtarmigan()
    {
        var bodyStats = new BodyCreationInfo
        {
            type = BodyTypes.Flying,
            overallWeight = 0.5,
            fatPercent = 0.15,
            musclePercent = 0.60
        };

        // 0-1 damage scale: 0.02 = negligible, prey bird
        var animal = new Animal("Ptarmigan", bodyStats, AnimalBehaviorType.Prey, AnimalSize.Small,
            attackDamage: 0.02, attackName: "beak", attackType: DamageType.Blunt,
            isHostile: false)
        {
            TrackingDifficulty = 7
        };
        animal.GenerateTraits();
        return animal;
    }

    public static Animal MakeFox()
    {
        var bodyStats = new BodyCreationInfo
        {
            type = BodyTypes.Quadruped,
            overallWeight = 6,
            fatPercent = 0.20,
            musclePercent = 0.55
        };

        // 0-1 damage scale: 0.12 = minor wound, small predator
        var animal = new Animal("Fox", bodyStats, AnimalBehaviorType.Scavenger, AnimalSize.Small,
            attackDamage: 0.12, attackName: "sharp teeth", attackType: DamageType.Pierce,
            isHostile: false)
        {
            TrackingDifficulty = 6
        };
        animal.GenerateTraits();
        return animal;
    }

    public static Animal MakeMegaloceros()
    {
        var bodyStats = new BodyCreationInfo
        {
            type = BodyTypes.Quadruped,
            overallWeight = 600,
            fatPercent = 0.15,
            musclePercent = 0.60
        };

        // 0-1 damage scale: 0.50 = gored/trampled, massive antlers
        var animal = new Animal("Megaloceros", bodyStats, AnimalBehaviorType.DangerousPrey, AnimalSize.Large,
            attackDamage: 0.50, attackName: "massive antlers", attackType: DamageType.Blunt,
            speedMps: 7.0, pursuitCommitment: 20.0,
            isHostile: false,
            disengageAfterMaul: 0.6)  // Defensive - leaves once threat neutralized
        {
            TrackingDifficulty = 3
        };
        animal.GenerateTraits();
        return animal;
    }

    public static Animal MakeSteppeBison()
    {
        var bodyStats = new BodyCreationInfo
        {
            type = BodyTypes.Quadruped,
            overallWeight = 800,
            fatPercent = 0.20,
            musclePercent = 0.55
        };

        // 0-1 damage scale: 0.55 = gored, dangerous prey
        var animal = new Animal("Steppe Bison", bodyStats, AnimalBehaviorType.DangerousPrey, AnimalSize.Large,
            attackDamage: 0.55, attackName: "horns", attackType: DamageType.Pierce,
            speedMps: 6.5, pursuitCommitment: 25.0,
            isHostile: false,
            disengageAfterMaul: 0.6)  // Defensive - leaves once threat neutralized
        {
            TrackingDifficulty = 2
        };
        animal.GenerateTraits();
        return animal;
    }

    public static Animal MakeCaveHyena()
    {
        var bodyStats = new BodyCreationInfo
        {
            type = BodyTypes.Quadruped,
            overallWeight = 70,
            fatPercent = 0.15,
            musclePercent = 0.65
        };

        // 0-1 damage scale: 0.40 = crushing bite, bone-crushing jaws
        var animal = new Animal("Cave Hyena", bodyStats, AnimalBehaviorType.Scavenger, AnimalSize.Large,
            attackDamage: 0.40, attackName: "crushing jaws", attackType: DamageType.Blunt,
            speedMps: 7.5, pursuitCommitment: 50.0,
            disengageAfterMaul: 0.3)  // Scavenger, may leave if prey plays dead
        {
            TrackingDifficulty = 5
        };
        animal.GenerateTraits();
        return animal;
    }
}

