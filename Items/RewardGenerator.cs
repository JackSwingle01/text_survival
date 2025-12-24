namespace text_survival.Items;

public enum RewardPool
{
    None,
    BasicSupplies,      // Sticks, tinder, berries - common finds
    AbandonedCamp,      // Left-behind tool + supplies
    HiddenCache,        // Valuable tool + good fuel
    BasicMeat,          // Small amount of raw meat (scavenged)
    LargeMeat,          // Significant meat haul (thorough butchering)
    GameTrailDiscovery, // Minor supplies (info reward placeholder)

    // Extended pools
    CraftingMaterials,  // Stone, bone, plant fiber
    ScrapTool,          // Damaged tool that still works
    WaterSource,        // Water find
    TinderBundle,       // Just tinder
    BoneHarvest,        // Bones from carcass
    SmallGame,          // Small animal meat (rabbit, bird)
    HideScrap           // Piece of usable hide
}

public static class RewardGenerator
{
    public static Inventory Generate(RewardPool pool)
    {
        return pool switch
        {
            RewardPool.BasicSupplies => GenerateBasicSupplies(),
            RewardPool.AbandonedCamp => GenerateAbandonedCamp(),
            RewardPool.HiddenCache => GenerateHiddenCache(),
            RewardPool.BasicMeat => GenerateBasicMeat(),
            RewardPool.LargeMeat => GenerateLargeMeat(),
            RewardPool.GameTrailDiscovery => GenerateGameTrailDiscovery(),
            RewardPool.CraftingMaterials => GenerateCraftingMaterials(),
            RewardPool.ScrapTool => GenerateScrapTool(),
            RewardPool.WaterSource => GenerateWaterSource(),
            RewardPool.TinderBundle => GenerateTinderBundle(),
            RewardPool.BoneHarvest => GenerateBoneHarvest(),
            RewardPool.SmallGame => GenerateSmallGame(),
            RewardPool.HideScrap => GenerateHideScrap(),
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
            () => resources.Sticks.Push(RandomWeight(0.2, 0.5)),
            () => resources.Tinder.Push(RandomWeight(0.1, 0.3)),
            () => resources.Berries.Push(RandomWeight(0.1, 0.25)),
            () => resources.Logs.Push(RandomWeight(0.8, 1.5))
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
        var tools = new Func<Tool>[]
        {
            () => Tool.Knife("Bone Knife"),
            () => Tool.Axe("Stone Axe"),
            () => Tool.Spear("Wooden Spear")
        };

        resources.Tools.Add(tools[Random.Shared.Next(tools.Length)]());

        // Plus some tinder or a stick
        if (Random.Shared.Next(2) == 0)
            resources.Tinder.Push(RandomWeight(0.2, 0.4));
        else
            resources.Sticks.Push(RandomWeight(0.2, 0.4));

        return resources;
    }

    private static Inventory GenerateHiddenCache()
    {
        var resources = new Inventory();

        // Valuable tool - fire striker is most valuable
        if (Random.Shared.Next(3) == 0)
        {
            resources.Tools.Add(Tool.FireStriker("Flint and Steel"));
        }
        else
        {
            var tools = new Func<Tool>[]
            {
                () => Tool.Knife("Flint Knife"),
                () => Tool.Axe("Flint Axe")
            };
            resources.Tools.Add(tools[Random.Shared.Next(tools.Length)]());
        }

        // Plus good fuel
        resources.Logs.Push(RandomWeight(1.5, 2.5));

        return resources;
    }

    private static Inventory GenerateBasicMeat()
    {
        var resources = new Inventory();
        // Quick scavenge - small amount of meat
        resources.RawMeat.Push(RandomWeight(0.3, 0.6));
        return resources;
    }

    private static Inventory GenerateLargeMeat()
    {
        var resources = new Inventory();
        // Thorough butchering - significant haul
        int cuts = Random.Shared.Next(2, 4); // 2-3 portions
        for (int i = 0; i < cuts; i++)
        {
            resources.RawMeat.Push(RandomWeight(0.4, 0.8));
        }
        return resources;
    }

    private static Inventory GenerateGameTrailDiscovery()
    {
        var resources = new Inventory();
        // Minor supplies as placeholder - info reward in future
        if (Random.Shared.Next(2) == 0)
            resources.Sticks.Push(RandomWeight(0.2, 0.4));
        else
            resources.Tinder.Push(RandomWeight(0.1, 0.2));
        return resources;
    }

    private static double RandomWeight(double min, double max)
    {
        return min + Random.Shared.NextDouble() * (max - min);
    }

    // Extended pool generators

    private static Inventory GenerateCraftingMaterials()
    {
        var resources = new Inventory();

        // Roll 1-2 crafting material types
        int itemCount = Random.Shared.Next(1, 3);
        var options = new List<Action>
        {
            () => resources.Stone.Push(RandomWeight(0.2, 0.4)),
            () => resources.Bone.Push(RandomWeight(0.2, 0.5)),
            () => resources.PlantFiber.Push(RandomWeight(0.1, 0.3))
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
        var tools = new Func<Tool>[]
        {
            () => { var t = Tool.Knife("Worn Knife"); t.Durability = Random.Shared.Next(3, 8); return t; },
            () => { var t = Tool.Axe("Damaged Axe"); t.Durability = Random.Shared.Next(2, 6); return t; },
            () => { var t = Tool.Spear("Cracked Spear"); t.Durability = Random.Shared.Next(3, 8); return t; }
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
        resources.Tinder.Push(RandomWeight(0.2, 0.5));
        return resources;
    }

    private static Inventory GenerateBoneHarvest()
    {
        var resources = new Inventory();
        int boneCount = Random.Shared.Next(1, 4); // 1-3 bones
        for (int i = 0; i < boneCount; i++)
        {
            resources.Bone.Push(RandomWeight(0.2, 0.5));
        }
        return resources;
    }

    private static Inventory GenerateSmallGame()
    {
        var resources = new Inventory();
        // Small animal - modest meat, maybe some bone
        resources.RawMeat.Push(RandomWeight(0.2, 0.4));
        if (Random.Shared.Next(2) == 0)
            resources.Bone.Push(RandomWeight(0.1, 0.2));
        return resources;
    }

    private static Inventory GenerateHideScrap()
    {
        var resources = new Inventory();
        resources.Hide.Push(RandomWeight(0.3, 0.6));
        return resources;
    }
}
