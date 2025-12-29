using text_survival.Bodies;
using text_survival.Items;

namespace text_survival.Actors.Animals;

public static class AnimalFactory
{
    /// <summary>
    /// Create an animal from a name string.
    /// Used for territories and event-based carcass creation.
    /// </summary>
    public static Animal? FromName(string animalName)
    {
        return animalName.ToLower() switch
        {
            "caribou" => MakeCaribou(),
            "rabbit" => MakeRabbit(),
            "ptarmigan" => MakePtarmigan(),
            "fox" => MakeFox(),
            "wolf" => MakeWolf(),
            "bear" => MakeBear(),
            "cave bear" => MakeCaveBear(),
            "hyena" or "cave hyena" => MakeCaveHyena(),
            "mammoth" or "woolly mammoth" => MakeWoollyMammoth(),
            "saber-tooth" or "saber-tooth tiger" or "sabertooth" => MakeSaberToothTiger(),
            "megaloceros" => MakeMegaloceros(),
            "bison" or "steppe bison" => MakeSteppeBison(),
            "rat" => MakeRat(),
            _ => null
        };
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

        var animal = new Animal("Rat", bodyStats, AnimalBehaviorType.Prey, AnimalSize.Small,
            attackDamage: 2, attackName: "teeth", attackType: DamageType.Pierce)
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

        var animal = new Animal("Wolf", bodyStats, AnimalBehaviorType.Predator, AnimalSize.Large,
            attackDamage: 10, attackName: "fangs", attackType: DamageType.Pierce,
            speedMps: 8.0, pursuitCommitment: 60.0)
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

        var animal = new Animal("Bear", bodyStats, AnimalBehaviorType.Predator, AnimalSize.Large,
            attackDamage: 20, attackName: "claws", attackType: DamageType.Sharp,
            speedMps: 5.0, pursuitCommitment: 30.0)
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

        var animal = new Animal("Cave Bear", bodyStats, AnimalBehaviorType.Predator, AnimalSize.Large,
            attackDamage: 25, attackName: "massive claws", attackType: DamageType.Sharp,
            speedMps: 4.5, pursuitCommitment: 25.0)
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

        var animal = new Animal("Woolly Mammoth", bodyStats, AnimalBehaviorType.DangerousPrey, AnimalSize.Large,
            attackDamage: 35, attackName: "tusks", attackType: DamageType.Pierce)
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

        var animal = new Animal("Saber-Tooth Tiger", bodyStats, AnimalBehaviorType.Predator, AnimalSize.Large,
            attackDamage: 30, attackName: "massive fangs", attackType: DamageType.Pierce,
            speedMps: 9.0, pursuitCommitment: 45.0)
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

        var animal = new Animal("Caribou", bodyStats, AnimalBehaviorType.Prey, AnimalSize.Large,
            attackDamage: 6, attackName: "antlers", attackType: DamageType.Blunt,
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

        var animal = new Animal("Rabbit", bodyStats, AnimalBehaviorType.Prey, AnimalSize.Small,
            attackDamage: 1, attackName: "teeth", attackType: DamageType.Blunt,
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

        var animal = new Animal("Ptarmigan", bodyStats, AnimalBehaviorType.Prey, AnimalSize.Small,
            attackDamage: 1, attackName: "beak", attackType: DamageType.Blunt,
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

        var animal = new Animal("Fox", bodyStats, AnimalBehaviorType.Scavenger, AnimalSize.Small,
            attackDamage: 4, attackName: "sharp teeth", attackType: DamageType.Pierce,
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

        var animal = new Animal("Megaloceros", bodyStats, AnimalBehaviorType.DangerousPrey, AnimalSize.Large,
            attackDamage: 15, attackName: "massive antlers", attackType: DamageType.Blunt,
            speedMps: 7.0, pursuitCommitment: 20.0,
            isHostile: false)
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

        var animal = new Animal("Steppe Bison", bodyStats, AnimalBehaviorType.DangerousPrey, AnimalSize.Large,
            attackDamage: 20, attackName: "horns", attackType: DamageType.Pierce,
            speedMps: 6.5, pursuitCommitment: 25.0,
            isHostile: false)
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

        var animal = new Animal("Cave Hyena", bodyStats, AnimalBehaviorType.Scavenger, AnimalSize.Large,
            attackDamage: 12, attackName: "crushing jaws", attackType: DamageType.Blunt,
            speedMps: 7.5, pursuitCommitment: 50.0)
        {
            TrackingDifficulty = 5
        };
        animal.GenerateTraits();
        return animal;
    }
}

