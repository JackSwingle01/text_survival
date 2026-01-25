using text_survival.Actors.Animals;
using text_survival.Environments.Features;
namespace text_survival.Environments.Factories;

/// <summary>
/// Factory methods for creating discoverable features.
/// Centralizes feature creation logic used by DiscoveryGenerator terrain pools.
/// Uses Random.Shared internally for variance.
/// </summary>
public static class DiscoveryFeatureFactory
{
    public static LocationFeature CreateNaturalShelter()
    {
        double insulation = 0.3 + Random.Shared.NextDouble() * 0.3;  // 0.3 - 0.6
        double windProtection = 0.4 + Random.Shared.NextDouble() * 0.3;  // 0.4 - 0.7

        return new ShelterFeature
        {
            Name = "Rock Overhang",
            TemperatureInsulation = insulation,
            OverheadCoverage = 0.8,
            WindCoverage = windProtection,
            ShelterType = ShelterType.RockOverhang
        };
    }

    public static LocationFeature CreateFlintOutcrop()
    {
        int quantity = 3 + Random.Shared.Next(5);  // 3-7 pieces

        var outcrop = new HarvestableFeature("flint_outcrop", "Flint Outcrop")
        {
            Description = "Sharp flint nodules erode from the rock face here. Good for tools.",
            MinutesToHarvest = 5
        };
        outcrop.AddResource("flint nodules", Resource.Flint, maxQuantity: quantity, weightPerUnit: 0.15, respawnHoursPerUnit: 0);
        return outcrop;
    }

    public static LocationFeature CreatePyriteSeam()
    {
        int quantity = 2 + Random.Shared.Next(4);  // 2-5 pieces

        var seam = new HarvestableFeature("pyrite_seam", "Iron Pyrite Seam")
        {
            Description = "A vein of iron pyrite runs through the rock. Sparks well against flint.",
            MinutesToHarvest = 8
        };
        seam.AddResource("pyrite chunks", Resource.Pyrite, maxQuantity: quantity, weightPerUnit: 0.08, respawnHoursPerUnit: 0);
        return seam;
    }

    public static LocationFeature CreateBonePile()
    {
        int quantity = 2 + Random.Shared.Next(4);  // 2-5 bones

        var pile = new HarvestableFeature("bone_pile", "Old Bones")
        {
            Description = "Weathered bones from some long-dead animal. Still useful.",
            MinutesToHarvest = 3
        };
        pile.AddBone("bones", maxQuantity: quantity, weightPerUnit: 0.2, respawnHoursPerUnit: 0);
        return pile;
    }

    public static LocationFeature CreateMedicinePatch()
    {
        var patch = new HarvestableFeature("medicine_patch", "Herb Patch")
        {
            Description = "A sheltered spot where medicinal plants thrive.",
            MinutesToHarvest = 8
        };

        // Always add shelf fungus
        patch.AddResource("shelf fungus", Resource.BirchPolypore,
            maxQuantity: 3 + Random.Shared.Next(3), weightPerUnit: 0.1, respawnHoursPerUnit: 336);

        // 50% chance of willow bark
        if (Random.Shared.NextDouble() < 0.5)
            patch.AddResource("willow bark", Resource.WillowBark,
                maxQuantity: 2 + Random.Shared.Next(2), weightPerUnit: 0.08, respawnHoursPerUnit: 336);

        // 40% chance of old man's beard
        if (Random.Shared.NextDouble() < 0.4)
            patch.AddResource("old man's beard", Resource.Usnea,
                maxQuantity: 2 + Random.Shared.Next(2), weightPerUnit: 0.05, respawnHoursPerUnit: 336);

        return patch;
    }

    public static LocationFeature CreateResinPocket()
    {
        int quantity = 2 + Random.Shared.Next(4);  // 2-5 globs

        var pocket = new HarvestableFeature("resin_pocket", "Resin-Weeping Pine")
        {
            Description = "Thick amber resin oozes from a wound in this pine. Burns hot and long.",
            MinutesToHarvest = 5
        };
        pocket.AddResource("pine resin", Resource.PineResin,
            maxQuantity: quantity, weightPerUnit: 0.05, respawnHoursPerUnit: 168);
        return pocket;
    }

    public static LocationFeature CreateAbandonedDen()
    {
        var den = new HarvestableFeature("abandoned_den", "Old Den")
        {
            Description = "An abandoned predator den. Gnawed bones and fur scraps remain.",
            MinutesToHarvest = 10
        };

        den.AddBone("old bones", maxQuantity: 2 + Random.Shared.Next(3), weightPerUnit: 0.2, respawnHoursPerUnit: 0);

        // 40% chance of fur scraps
        if (Random.Shared.NextDouble() < 0.4)
            den.AddResource("fur scraps", Resource.Hide,
                maxQuantity: 1, weightPerUnit: 0.15, respawnHoursPerUnit: 0);

        return den;
    }

    public static LocationFeature CreateHiddenSpring()
    {
        return new WaterFeature("hidden_spring", "Hidden Spring")
            .AsOpenWater()
            .WithDescription("Water seeps from between the rocks. Unfrozen even in deep cold.");
    }

    public static LocationFeature CreateCharDeposit()
    {
        int quantity = 3 + Random.Shared.Next(5);  // 3-7 pieces

        var deposit = new HarvestableFeature("char_deposit", "Old Fire Scar")
        {
            Description = "Blackened earth from an old fire. Charcoal fragments scattered in the ash.",
            MinutesToHarvest = 5
        };
        deposit.AddResource("charcoal", Resource.Charcoal,
            maxQuantity: quantity, weightPerUnit: 0.08, respawnHoursPerUnit: 0);
        return deposit;
    }

    public static LocationFeature CreateFrozenSmallGame()
    {
        // 50/50 rabbit or ptarmigan
        AnimalType type = Random.Shared.NextDouble() < 0.5 ? AnimalType.Rabbit : AnimalType.Ptarmigan;
        string name = type == AnimalType.Rabbit ? "Rabbit" : "Ptarmigan";
        double weight = type == AnimalType.Rabbit ? 2.0 : 0.5;

        var carcass = new CarcassFeature
        {
            Name = $"frozen_{name.ToLower()}_carcass",
            AnimalType = type,
            AnimalName = $"Frozen {name}",
            BodyWeightKg = weight,
            RawHoursSinceDeath = 0,
            EffectiveHoursSinceDeath = 0,
            LastKnownTemperatureF = 10  // Frozen
        };

        // Initialize yields based on body weight (simplified from CarcassFeature logic)
        double meatYield = weight * 0.40;
        double boneYield = weight * 0.15;
        double hideYield = weight * 0.10;

        carcass.MeatRemainingKg = meatYield;
        carcass.BoneRemainingKg = boneYield;
        carcass.HideRemainingKg = hideYield;
        carcass.SinewRemainingKg = weight * 0.03;
        carcass.FatRemainingKg = weight * 0.05;

        return carcass;
    }

    public static LocationFeature CreateIceCave()
    {
        double insulation = 0.25 + Random.Shared.NextDouble() * 0.15;  // 0.25 - 0.40 (cold but stable)
        double windProtection = 0.7 + Random.Shared.NextDouble() * 0.2;  // 0.7 - 0.9

        return new ShelterFeature("Ice Cave", ShelterType.Cave,
            tempInsulation: insulation,
            overheadCoverage: 0.95,
            windCoverage: windProtection,
            insulationCap: 0.45,
            overheadCap: 0.98,
            windCap: 0.95);
    }

    public static LocationFeature CreateAntlerShed()
    {
        int quantity = 2 + Random.Shared.Next(3);  // 2-4 antler pieces

        var shed = new HarvestableFeature("antler_shed", "Shed Antlers")
        {
            Description = "Antlers shed by caribou or megaloceros. Dense bone, good for tools.",
            MinutesToHarvest = 5
        };
        shed.AddBone("antler pieces", maxQuantity: quantity, weightPerUnit: 0.4, respawnHoursPerUnit: 0);
        return shed;
    }

    public static LocationFeature CreateOldButcheringSite()
    {
        var site = new HarvestableFeature("old_butchering_site", "Old Kill Site")
        {
            Description = "Scattered remains of an old butchering. Sinew strips and bone fragments left behind.",
            MinutesToHarvest = 8
        };

        // Always some bone scraps
        site.AddBone("bone scraps", maxQuantity: 2 + Random.Shared.Next(2), weightPerUnit: 0.15, respawnHoursPerUnit: 0);

        // Usually some sinew
        if (Random.Shared.NextDouble() < 0.7)
            site.AddResource("dried sinew", Resource.Sinew,
                maxQuantity: 1 + Random.Shared.Next(2), weightPerUnit: 0.05, respawnHoursPerUnit: 0);

        // Sometimes hide scraps
        if (Random.Shared.NextDouble() < 0.4)
            site.AddResource("hide scraps", Resource.Hide,
                maxQuantity: 1, weightPerUnit: 0.2, respawnHoursPerUnit: 0);

        return site;
    }

    public static LocationFeature CreateKnappingScatter()
    {
        int quantity = 2 + Random.Shared.Next(4);  // 2-5 usable pieces

        var scatter = new HarvestableFeature("knapping_scatter", "Knapping Scatter")
        {
            Description = "Flint flakes and cores scattered around a flat rock. Someone made tools here.",
            MinutesToHarvest = 4
        };
        scatter.AddResource("flint pieces", Resource.Flint,
            maxQuantity: quantity, weightPerUnit: 0.12, respawnHoursPerUnit: 0);

        // Sometimes a chunk of shale too
        if (Random.Shared.NextDouble() < 0.3)
            scatter.AddResource("shale chunk", Resource.Shale,
                maxQuantity: 1, weightPerUnit: 0.2, respawnHoursPerUnit: 0);

        return scatter;
    }

    public static LocationFeature CreateTallowPot()
    {
        int quantity = 1 + Random.Shared.Next(3);  // 1-3 portions

        var cache = new SalvageFeature("tallow_pot", "Tallow Cache")
        {
            DiscoveryText = "Rendered fat sealed in birch bark. Someone cached this here.",
            NarrativeHook = "Someone else's preparation. Now yours.",
            MinutesToSalvage = 5
        };

        for (int i = 0; i < quantity; i++)
            cache.Resources.Add(Resource.Tallow, 0.25);

        return cache;
    }

    public static LocationFeature CreateDryWoodStack()
    {
        int logQuantity = 3 + Random.Shared.Next(4);  // 3-6 logs
        int stickQuantity = 4 + Random.Shared.Next(5);  // 4-8 sticks

        // Random wood type
        Resource woodType = Random.Shared.Next(3) switch
        {
            0 => Resource.Pine,
            1 => Resource.Birch,
            _ => Resource.Oak
        };
        string woodName = woodType.ToString().ToLower();

        var stack = new HarvestableFeature("dry_wood_stack", "Dry Wood Stack")
        {
            Description = $"A stack of dry {woodName} logs under a rock ledge. Ready to burn.",
            MinutesToHarvest = 4
        };
        stack.AddLogs($"dry {woodName} logs", woodType, maxQuantity: logQuantity, weightPerUnit: 1.5, respawnHoursPerUnit: 0);
        stack.AddSticks("dry sticks", maxQuantity: stickQuantity, weightPerUnit: 0.15, respawnHoursPerUnit: 0);
        return stack;
    }

    public static LocationFeature CreateCordageBundle()
    {
        var cache = new SalvageFeature("cordage_bundle", "Cordage Bundle")
        {
            DiscoveryText = "Coiled rope and bundles of processed fiber wrapped in bark.",
            NarrativeHook = "Someone else's preparation. Now yours.",
            MinutesToSalvage = 5
        };

        // Always some plant fiber
        int fiberQuantity = 3 + Random.Shared.Next(3);
        for (int i = 0; i < fiberQuantity; i++)
            cache.Resources.Add(Resource.PlantFiber, 0.08);

        // Usually some rope
        if (Random.Shared.NextDouble() < 0.7)
        {
            int ropeQuantity = 1 + Random.Shared.Next(2);
            for (int i = 0; i < ropeQuantity; i++)
                cache.Resources.Add(Resource.Rope, 0.15);
        }

        return cache;
    }

    public static LocationFeature CreateTinderCache()
    {
        var cache = new SalvageFeature("tinder_cache", "Tinder Cache")
        {
            DiscoveryText = "Birch bark strips and resin wrapped tight and tucked into a crevice.",
            NarrativeHook = "Someone else's preparation. Now yours.",
            MinutesToSalvage = 5
        };

        // Birch bark - prime tinder
        int barkQuantity = 3 + Random.Shared.Next(3);
        for (int i = 0; i < barkQuantity; i++)
            cache.Resources.Add(Resource.BirchBark, 0.03);

        // Pine resin
        int resinQuantity = 2 + Random.Shared.Next(2);
        for (int i = 0; i < resinQuantity; i++)
            cache.Resources.Add(Resource.PineResin, 0.05);

        // Sometimes some amadou
        if (Random.Shared.NextDouble() < 0.3)
        {
            int amadouQuantity = 1 + Random.Shared.Next(2);
            for (int i = 0; i < amadouQuantity; i++)
                cache.Resources.Add(Resource.Amadou, 0.02);
        }

        return cache;
    }

    public static LocationFeature CreateMedicineStash()
    {
        var cache = new SalvageFeature("medicine_stash", "Medicine Stash")
        {
            DiscoveryText = "Dried herbs and fungi bundled together. Someone's healing supplies.",
            NarrativeHook = "Someone else's preparation. Now yours.",
            MinutesToSalvage = 10
        };

        // Always some birch polypore
        int polyporeQuantity = 2 + Random.Shared.Next(2);
        for (int i = 0; i < polyporeQuantity; i++)
            cache.Resources.Add(Resource.BirchPolypore, 0.08);

        // Randomly add 2-3 other medicines
        var medicines = new List<(Resource resource, double weight)>
        {
            (Resource.WillowBark, 0.06),
            (Resource.Chaga, 0.1),
            (Resource.Usnea, 0.04),
            (Resource.SphagnumMoss, 0.05),
            (Resource.RoseHip, 0.03),
            (Resource.JuniperBerry, 0.03)
        };

        // Shuffle and pick 2-3
        var shuffled = medicines.OrderBy(_ => Random.Shared.Next()).ToList();
        int count = 2 + Random.Shared.Next(2);

        for (int i = 0; i < count && i < shuffled.Count; i++)
        {
            var (resource, weight) = shuffled[i];
            int quantity = 2 + Random.Shared.Next(2);
            for (int j = 0; j < quantity; j++)
                cache.Resources.Add(resource, weight);
        }

        return cache;
    }

    // Terrain-specific animal territory wrappers
    public static AnimalTerritoryFeature CreateForestGameTrail()
    {
        double density = 0.4 + Random.Shared.NextDouble() * 0.3;
        return FeatureFactory.CreateMixedPreyAnimals(density);
    }

    public static AnimalTerritoryFeature CreateClearingGameTrail()
    {
        double density = 0.3 + Random.Shared.NextDouble() * 0.25;
        return FeatureFactory.CreateMixedPreyAnimals(density);
    }

    public static AnimalTerritoryFeature CreatePlainSmallGame()
    {
        double density = 0.25 + Random.Shared.NextDouble() * 0.2;
        return FeatureFactory.CreateSmallGameAnimals(density);
    }

    public static AnimalTerritoryFeature CreateHillsSmallGame()
    {
        double density = 0.2 + Random.Shared.NextDouble() * 0.15;
        return FeatureFactory.CreateSmallGameAnimals(density);
    }

    public static AnimalTerritoryFeature CreateRockySmallGame()
    {
        double density = 0.15 + Random.Shared.NextDouble() * 0.15;
        return FeatureFactory.CreateSmallGameAnimals(density);
    }

    public static AnimalTerritoryFeature CreateMarshWaterfowl()
    {
        double density = 0.3 + Random.Shared.NextDouble() * 0.2;
        return FeatureFactory.CreateWaterfowlAnimals(density);
    }
}
