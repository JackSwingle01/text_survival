namespace text_survival.Items;

public enum RewardPool
{
    None,
    BasicSupplies,      // Sticks, tinder, berries - common finds
    AbandonedCamp,      // Left-behind tool + supplies
    HiddenCache,        // Valuable tool + good fuel
    BasicMeat,          // Small amount of raw meat (scavenged)
    LargeMeat,          // Significant meat haul (thorough butchering)
    MassiveMeat,        // Megafauna kill (mammoth, cave bear - huge yield)
    GameTrailDiscovery, // Minor supplies (info reward placeholder)
    SquirrelCache,      // Nuts, dried berries - rodent food stores
    HoneyHarvest,       // Honey from beehives
    MedicinalForage,    // Medicinal plants and fungi

    // Extended pools
    CraftingMaterials,  // Stone, bone, plant fiber
    ScrapTool,          // Damaged tool that still works
    WaterSource,        // Water find
    TinderBundle,       // Just tinder
    BoneHarvest,        // Bones from carcass
    SmallGame,          // Small animal meat (rabbit, bird)
    HideScrap,          // Piece of usable hide
    FrozenBirdFind      // Lucky windfall - frozen ptarmigan with meat and feathers
}

public static class RewardGenerator
{
    public static Inventory Generate(RewardPool pool, double densityFactor = 1.0)
    {
        return pool switch
        {
            RewardPool.BasicSupplies => GenerateBasicSupplies(),
            RewardPool.AbandonedCamp => GenerateAbandonedCamp(),
            RewardPool.HiddenCache => GenerateHiddenCache(),
            RewardPool.BasicMeat => GenerateBasicMeat(),
            RewardPool.LargeMeat => GenerateLargeMeat(),
            RewardPool.MassiveMeat => GenerateMassiveMeat(),
            RewardPool.GameTrailDiscovery => GenerateGameTrailDiscovery(),
            RewardPool.SquirrelCache => GenerateSquirrelCache(densityFactor),
            RewardPool.HoneyHarvest => GenerateHoneyHarvest(densityFactor),
            RewardPool.MedicinalForage => GenerateMedicinalForage(densityFactor),
            RewardPool.CraftingMaterials => GenerateCraftingMaterials(),
            RewardPool.ScrapTool => GenerateScrapTool(),
            RewardPool.WaterSource => GenerateWaterSource(),
            RewardPool.TinderBundle => GenerateTinderBundle(),
            RewardPool.BoneHarvest => GenerateBoneHarvest(densityFactor),
            RewardPool.SmallGame => GenerateSmallGame(),
            RewardPool.HideScrap => GenerateHideScrap(),
            RewardPool.FrozenBirdFind => GenerateFrozenBirdFind(),
            _ => new Inventory()
        };
    }

    private static Inventory GenerateBasicSupplies()
    {
        var resources = new Inventory();

        // Roll 1-2 items
        int itemCount = Random.Shared.Next(1, 3);
        var options = new List<Action>
        {
            () => resources.Add(Resource.Stick, RandomWeight(0.2, 0.5)),
            () => resources.Add(Resource.Tinder, RandomWeight(0.1, 0.3)),
            () => resources.Add(Resource.Berries, RandomWeight(0.1, 0.25)),
            () => resources.Add(RandomWoodType(), RandomWeight(0.8, 1.5))
        };

        // Shuffle and pick
        var shuffled = options.OrderBy(_ => Random.Shared.Next()).Take(itemCount);
        foreach (var add in shuffled)
        {
            add();
        }

        return resources;
    }

    private static Inventory GenerateAbandonedCamp()
    {
        var resources = new Inventory();

        // Random tool left behind
        var tools = new Func<Gear>[]
        {
            () => Gear.Knife("Bone Knife"),
            () => Gear.Axe("Stone Axe"),
            () => Gear.Spear("Wooden Spear"),
            () => Gear.Torch("Simple Torch")
        };

        resources.Tools.Add(tools[Random.Shared.Next(tools.Length)]());

        // Plus some tinder or a stick
        if (Random.Shared.Next(2) == 0)
            resources.Add(Resource.Tinder, RandomWeight(0.2, 0.4));
        else
            resources.Add(Resource.Stick, RandomWeight(0.2, 0.4));

        return resources;
    }

    private static Inventory GenerateHiddenCache()
    {
        var resources = new Inventory();

        // Valuable tool - fire striker is most valuable
        if (Random.Shared.Next(3) == 0)
        {
            resources.Tools.Add(Gear.FireStriker("Flint and Steel"));
        }
        else
        {
            var tools = new Func<Gear>[]
            {
                () => Gear.Knife("Flint Knife"),
                () => Gear.Axe("Flint Axe")
            };
            resources.Tools.Add(tools[Random.Shared.Next(tools.Length)]());
        }

        // Plus good fuel
        resources.Add(RandomWoodType(), RandomWeight(1.5, 2.5));

        return resources;
    }

    private static Inventory GenerateBasicMeat()
    {
        var resources = new Inventory();
        // Quick scavenge - small amount of meat
        resources.Add(Resource.RawMeat, RandomWeight(0.3, 0.6));
        return resources;
    }

    private static Inventory GenerateLargeMeat()
    {
        var resources = new Inventory();
        // Thorough butchering - significant haul
        int cuts = Random.Shared.Next(2, 4); // 2-3 portions
        for (int i = 0; i < cuts; i++)
        {
            resources.Add(Resource.RawMeat, RandomWeight(0.4, 0.8));
        }
        return resources;
    }

    private static Inventory GenerateMassiveMeat()
    {
        var resources = new Inventory();
        // Megafauna kill - massive haul (~50kg total)
        // Player can't carry all at once, will need multiple trips or caching
        int cuts = Random.Shared.Next(5, 8); // 5-7 portions
        for (int i = 0; i < cuts; i++)
        {
            resources.Add(Resource.RawMeat, RandomWeight(8.0, 12.0));
        }
        return resources;
    }

    private static Inventory GenerateGameTrailDiscovery()
    {
        var resources = new Inventory();
        // Minor supplies as placeholder - info reward in future
        if (Random.Shared.Next(2) == 0)
            resources.Add(Resource.Stick, RandomWeight(0.2, 0.4));
        else
            resources.Add(Resource.Tinder, RandomWeight(0.1, 0.2));
        return resources;
    }

    private static Inventory GenerateSquirrelCache(double densityFactor)
    {
        var resources = new Inventory();

        // Scale item count by density: 1-2 base, up to 2-4 for deep cache
        int baseItems = Random.Shared.Next(1, 3);
        int itemCount = (int)Math.Ceiling(baseItems * densityFactor);

        var options = new List<Action>
        {
            () => resources.Add(Resource.Nuts, RandomWeight(0.1, 0.2) * densityFactor),
            () => resources.Add(Resource.DriedBerries, RandomWeight(0.05, 0.15) * densityFactor),
            () => resources.Add(Resource.Berries, RandomWeight(0.1, 0.2) * densityFactor),
            () => resources.Add(Resource.Nuts, RandomWeight(0.15, 0.25) * densityFactor)
        };

        var shuffled = options.OrderBy(_ => Random.Shared.Next()).Take(itemCount);
        foreach (var add in shuffled) add();

        return resources;
    }

    private static Inventory GenerateHoneyHarvest(double densityFactor)
    {
        var resources = new Inventory();

        // Honey is dense calories - scale by density
        resources.Add(Resource.Honey, RandomWeight(0.2, 0.4) * densityFactor);

        return resources;
    }

    private static Inventory GenerateMedicinalForage(double densityFactor)
    {
        var resources = new Inventory();

        // Scale item count by density
        int baseItems = Random.Shared.Next(1, 3);
        int itemCount = (int)Math.Ceiling(baseItems * densityFactor);

        var options = new List<Action>
        {
            () => resources.Add(Resource.BirchPolypore, RandomWeight(0.05, 0.1) * densityFactor),
            () => resources.Add(Resource.Chaga, RandomWeight(0.05, 0.1) * densityFactor),
            () => resources.Add(Resource.Amadou, RandomWeight(0.03, 0.08) * densityFactor),
            () => resources.Add(Resource.RoseHip, RandomWeight(0.05, 0.1) * densityFactor),
            () => resources.Add(Resource.WillowBark, RandomWeight(0.05, 0.1) * densityFactor),
            () => resources.Add(Resource.Usnea, RandomWeight(0.03, 0.08) * densityFactor)
        };

        var shuffled = options.OrderBy(_ => Random.Shared.Next()).Take(itemCount);
        foreach (var add in shuffled) add();

        return resources;
    }

    private static double RandomWeight(double min, double max)
    {
        return min + Random.Shared.NextDouble() * (max - min);
    }

    private static Resource RandomWoodType()
    {
        return Random.Shared.Next(3) switch
        {
            0 => Resource.Pine,
            1 => Resource.Birch,
            _ => Resource.Oak
        };
    }

    // Extended pool generators

    private static Inventory GenerateCraftingMaterials()
    {
        var resources = new Inventory();

        // Roll 1-2 crafting material types
        int itemCount = Random.Shared.Next(1, 3);
        var options = new List<Action>
        {
            () => resources.Add(Resource.Stone, RandomWeight(0.2, 0.4)),
            () => resources.Add(Resource.Bone, RandomWeight(0.2, 0.5)),
            () => resources.Add(Resource.PlantFiber, RandomWeight(0.1, 0.3))
        };

        var shuffled = options.OrderBy(_ => Random.Shared.Next()).Take(itemCount);
        foreach (var add in shuffled)
        {
            add();
        }

        return resources;
    }

    private static Inventory GenerateScrapTool()
    {
        var resources = new Inventory();

        // A damaged tool with limited durability
        var tools = new Func<Gear>[]
        {
            () => { var t = Gear.Knife("Worn Knife"); t.Durability = Random.Shared.Next(3, 8); return t; },
            () => { var t = Gear.Axe("Damaged Axe"); t.Durability = Random.Shared.Next(2, 6); return t; },
            () => { var t = Gear.Spear("Cracked Spear"); t.Durability = Random.Shared.Next(3, 8); return t; }
        };

        resources.Tools.Add(tools[Random.Shared.Next(tools.Length)]());

        return resources;
    }

    private static Inventory GenerateWaterSource()
    {
        var resources = new Inventory();
        resources.WaterLiters += RandomWeight(0.5, 1.5);
        return resources;
    }

    private static Inventory GenerateTinderBundle()
    {
        var resources = new Inventory();
        resources.Add(Resource.Tinder, RandomWeight(0.2, 0.5));
        return resources;
    }

    private static Inventory GenerateBoneHarvest(double densityFactor)
    {
        var resources = new Inventory();
        int baseBones = Random.Shared.Next(1, 4); // 1-3 bones
        int boneCount = (int)Math.Ceiling(baseBones * densityFactor);
        for (int i = 0; i < boneCount; i++)
        {
            resources.Add(Resource.Bone, RandomWeight(0.2, 0.5) * densityFactor);
        }
        return resources;
    }

    private static Inventory GenerateSmallGame()
    {
        var resources = new Inventory();
        // Small animal - modest meat, maybe some bone
        resources.Add(Resource.RawMeat, RandomWeight(0.2, 0.4));
        if (Random.Shared.Next(2) == 0)
            resources.Add(Resource.Bone, RandomWeight(0.1, 0.2));
        return resources;
    }

    private static Inventory GenerateHideScrap()
    {
        var resources = new Inventory();
        resources.Add(Resource.Hide, RandomWeight(0.3, 0.6));
        return resources;
    }

    private static Inventory GenerateFrozenBirdFind()
    {
        var resources = new Inventory();
        // A frozen ptarmigan - generous early game windfall
        // More meat than typical small game (0.5-0.8kg vs 0.2-0.4kg)
        resources.Add(Resource.RawMeat, RandomWeight(0.5, 0.8));
        // Feathers for tinder and insulation
        resources.Add(Resource.Tinder, RandomWeight(0.08, 0.15));
        // Some bone
        resources.Add(Resource.Bone, RandomWeight(0.1, 0.2));
        return resources;
    }
}
