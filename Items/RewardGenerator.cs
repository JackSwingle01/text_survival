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
    FrozenBirdFind,     // Lucky windfall - frozen ptarmigan with meat and feathers

    // Discovery pools - used by both events and salvage features
    TrapperStash,       // Trapping supplies - sinew, fiber, cord
    HuntersBlind,       // Hunting gear - spear, bones
    ResinPocket,        // Pine resin
    CharDeposit,        // Old fire charcoal
    AbandonedDen,       // Predator den scraps - bones, hide

    // Fishing
    FishCatch,          // Raw fish from fishing

    // Elite pools
    EliteShelter,       // Well-preserved abandoned camp
    MegafaunaGraveyard, // Natural deposit of bones/hides
    AncientCache        // Hidden high-value stash
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
            RewardPool.TrapperStash => GenerateTrapperStash(),
            RewardPool.HuntersBlind => GenerateHuntersBlind(),
            RewardPool.ResinPocket => GenerateResinPocket(),
            RewardPool.CharDeposit => GenerateCharDeposit(),
            RewardPool.AbandonedDen => GenerateAbandonedDen(),
            RewardPool.FishCatch => GenerateFishCatch(densityFactor),
            RewardPool.EliteShelter => GenerateEliteShelter(),
            RewardPool.MegafaunaGraveyard => GenerateMegafaunaGraveyard(),
            RewardPool.AncientCache => GenerateAncientCache(),
            _ => new Inventory()
        };
    }

    private static Inventory GenerateBasicSupplies()
    {
        var resources = new Inventory();

        // Roll 2-4 items (buffed from 1-3)
        int itemCount = Random.Shared.Next(2, 5);
        var options = new List<Action>
        {
            () => resources.Add(Resource.Stick, RandomWeight(0.4, 0.8)),      // +75%
            () => resources.Add(Resource.Tinder, RandomWeight(0.2, 0.5)),     // +67%
            () => resources.Add(Resource.Berries, RandomWeight(0.2, 0.4)),    // +60%
            () => resources.Add(RandomWoodType(), RandomWeight(1.5, 2.5))     // +67%
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

        // Random tool left behind - with durability variance (30-70%)
        var tool = Random.Shared.Next(4) switch
        {
            0 => Gear.Knife("Bone Knife"),
            1 => Gear.Axe("Stone Axe"),
            2 => Gear.Spear("Wooden Spear"),
            _ => Gear.Torch("Simple Torch")
        };

        tool.Durability = Random.Shared.Next(
            (int)(tool.MaxDurability * 0.3),
            (int)(tool.MaxDurability * 0.7) + 1
        );
        resources.Tools.Add(tool);

        // Always both tinder and sticks (buffed from either/or)
        resources.Add(Resource.Tinder, RandomWeight(0.15, 0.3));
        resources.Add(Resource.Stick, RandomWeight(0.3, 0.5));

        // 30% chance of bone
        if (Random.Shared.NextDouble() < 0.3)
            resources.Add(Resource.Bone, RandomWeight(0.2, 0.4));

        return resources;
    }

    private static Inventory GenerateHiddenCache()
    {
        var resources = new Inventory();

        // Valuable tool - fire striker is most valuable, with durability variance (60-100%)
        Gear tool;
        if (Random.Shared.Next(3) == 0)
        {
            tool = Gear.FireStriker("Flint and Steel");
        }
        else
        {
            tool = Random.Shared.Next(2) switch
            {
                0 => Gear.Knife("Flint Knife"),
                _ => Gear.Axe("Flint Axe")
            };
        }

        tool.Durability = Random.Shared.Next(
            (int)(tool.MaxDurability * 0.6),
            (int)(tool.MaxDurability * 1.0) + 1
        );
        resources.Tools.Add(tool);

        // Better fuel (buffed from 1.5-2.5)
        resources.Add(RandomWoodType(), RandomWeight(2.0, 3.5));

        // 40% chance of tinder
        if (Random.Shared.NextDouble() < 0.4)
            resources.Add(Resource.Tinder, RandomWeight(0.2, 0.4));

        // 25% chance of dried meat
        if (Random.Shared.NextDouble() < 0.25)
            resources.Add(Resource.DriedMeat, RandomWeight(0.3, 0.5));

        return resources;
    }

    private static Inventory GenerateBasicMeat()
    {
        var resources = new Inventory();
        // Quick scavenge - modest amount of meat (buffed +67%)
        resources.Add(Resource.RawMeat, RandomWeight(0.5, 0.9));
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
        // Meaningful discovery reward (reworked from nearly useless)
        resources.Add(Resource.Stick, RandomWeight(0.3, 0.5));
        resources.Add(Resource.Tinder, RandomWeight(0.15, 0.3));

        // 50% chance of bone (evidence of kills)
        if (Random.Shared.NextDouble() < 0.5)
            resources.Add(Resource.Bone, RandomWeight(0.2, 0.4));

        // 30% chance of hide scrap
        if (Random.Shared.NextDouble() < 0.3)
            resources.Add(Resource.Hide, RandomWeight(0.15, 0.3));

        return resources;
    }

    private static Inventory GenerateSquirrelCache(double densityFactor)
    {
        var resources = new Inventory();

        // Scale item count by density: 2-4 base (buffed from 1-3)
        int baseItems = Random.Shared.Next(2, 5);
        int itemCount = (int)Math.Ceiling(baseItems * densityFactor);

        var options = new List<Action>
        {
            () => resources.Add(Resource.Nuts, RandomWeight(0.2, 0.35) * densityFactor),          // +75%
            () => resources.Add(Resource.DriedBerries, RandomWeight(0.1, 0.25) * densityFactor),  // +67%
            () => resources.Add(Resource.Berries, RandomWeight(0.2, 0.35) * densityFactor),       // +75%
            () => resources.Add(Resource.Nuts, RandomWeight(0.25, 0.4) * densityFactor)           // +60%
        };

        var shuffled = options.OrderBy(_ => Random.Shared.Next()).Take(itemCount);
        foreach (var add in shuffled) add();

        return resources;
    }

    private static Inventory GenerateHoneyHarvest(double densityFactor)
    {
        var resources = new Inventory();

        // Honey is dense calories - scale by density (buffed +75%)
        resources.Add(Resource.Honey, RandomWeight(0.4, 0.7) * densityFactor);

        return resources;
    }

    private static Inventory GenerateMedicinalForage(double densityFactor)
    {
        var resources = new Inventory();

        // Scale item count by density: 2-4 base (buffed from 1-3)
        int baseItems = Random.Shared.Next(2, 5);
        int itemCount = (int)Math.Ceiling(baseItems * densityFactor);

        // Weights doubled (was 0.03-0.1 range, now 0.06-0.2 range)
        var options = new List<Action>
        {
            () => resources.Add(Resource.BirchPolypore, RandomWeight(0.1, 0.2) * densityFactor),
            () => resources.Add(Resource.Chaga, RandomWeight(0.1, 0.2) * densityFactor),
            () => resources.Add(Resource.Amadou, RandomWeight(0.06, 0.16) * densityFactor),
            () => resources.Add(Resource.RoseHip, RandomWeight(0.1, 0.2) * densityFactor),
            () => resources.Add(Resource.WillowBark, RandomWeight(0.1, 0.2) * densityFactor),
            () => resources.Add(Resource.Usnea, RandomWeight(0.06, 0.16) * densityFactor)
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

        // Roll 2-4 crafting material types (buffed from 1-3)
        int itemCount = Random.Shared.Next(2, 5);
        var options = new List<Action>
        {
            () => resources.Add(Resource.Stone, RandomWeight(0.4, 0.7)),       // +75%
            () => resources.Add(Resource.Bone, RandomWeight(0.4, 0.8)),        // +60%
            () => resources.Add(Resource.PlantFiber, RandomWeight(0.2, 0.5))   // +67%
        };

        var shuffled = options.OrderBy(_ => Random.Shared.Next()).Take(itemCount);
        foreach (var add in shuffled)
        {
            add();
        }

        // 30% chance of sinew
        if (Random.Shared.NextDouble() < 0.3)
            resources.Add(Resource.Sinew, RandomWeight(0.1, 0.2));

        // 25% chance of hide
        if (Random.Shared.NextDouble() < 0.25)
            resources.Add(Resource.Hide, RandomWeight(0.2, 0.4));

        return resources;
    }

    private static Inventory GenerateScrapTool()
    {
        var resources = new Inventory();

        // A damaged tool with limited durability (20-60% range)
        var tool = Random.Shared.Next(3) switch
        {
            0 => Gear.Knife("Worn Knife"),
            1 => Gear.Axe("Damaged Axe"),
            _ => Gear.Spear("Cracked Spear")
        };

        tool.Durability = Random.Shared.Next(
            (int)(tool.MaxDurability * 0.2),
            (int)(tool.MaxDurability * 0.6) + 1
        );
        resources.Tools.Add(tool);

        return resources;
    }

    private static Inventory GenerateWaterSource()
    {
        var resources = new Inventory();
        // Buffed +67%
        resources.WaterLiters += RandomWeight(1.0, 2.5);
        return resources;
    }

    private static Inventory GenerateTinderBundle()
    {
        var resources = new Inventory();
        // Buffed +75%
        resources.Add(Resource.Tinder, RandomWeight(0.4, 0.8));
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
        // Small animal - more realistic yield (buffed +75%)
        resources.Add(Resource.RawMeat, RandomWeight(0.4, 0.7));

        // 70% chance of bone (was 50%)
        if (Random.Shared.NextDouble() < 0.7)
            resources.Add(Resource.Bone, RandomWeight(0.15, 0.3));

        // 40% chance of hide scrap
        if (Random.Shared.NextDouble() < 0.4)
            resources.Add(Resource.Hide, RandomWeight(0.1, 0.2));

        return resources;
    }

    private static Inventory GenerateHideScrap()
    {
        var resources = new Inventory();
        // Buffed +50%
        resources.Add(Resource.Hide, RandomWeight(0.5, 0.9));
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

    // Discovery pool generators - shared between events and salvage features

    private static Inventory GenerateTrapperStash()
    {
        var resources = new Inventory();
        // Buffed weights
        resources.Add(Resource.PlantFiber, RandomWeight(0.15, 0.3));    // +50%
        resources.Add(Resource.Sinew, RandomWeight(0.12, 0.25));        // +50%
        resources.Add(Resource.Stick, RandomWeight(0.5, 0.8));          // +60%

        // 50% chance of preserved bait hide (was 30%)
        if (Random.Shared.NextDouble() < 0.5)
            resources.Add(Resource.Hide, RandomWeight(0.3, 0.5));       // +25%

        // 30% chance of rope
        if (Random.Shared.NextDouble() < 0.3)
            resources.Add(Resource.Rope, RandomWeight(0.1, 0.2));

        return resources;
    }

    private static Inventory GenerateHuntersBlind()
    {
        var resources = new Inventory();
        // Buffed weights
        resources.Add(Resource.Stick, RandomWeight(0.6, 1.0));          // +50%

        // 50% chance of an abandoned spear with durability variance (30-70%)
        if (Random.Shared.NextDouble() < 0.5)
        {
            var spear = Gear.Spear("Crude Spear");
            spear.Durability = Random.Shared.Next(
                (int)(spear.MaxDurability * 0.3),
                (int)(spear.MaxDurability * 0.7) + 1
            );
            resources.Tools.Add(spear);
        }

        // 50% chance of bones (was 30%)
        if (Random.Shared.NextDouble() < 0.5)
            resources.Add(Resource.Bone, RandomWeight(0.3, 0.5));       // +50%

        // 40% chance of tinder
        if (Random.Shared.NextDouble() < 0.4)
            resources.Add(Resource.Tinder, RandomWeight(0.15, 0.3));

        return resources;
    }

    private static Inventory GenerateResinPocket()
    {
        var resources = new Inventory();
        // Pine resin globs - good for fire-starting and waterproofing
        // Buffed: 5-10 globs (was 2-5), weight 0.08-0.15 each (was 0.04-0.08)
        // Total: 0.4-1.5kg (was 0.08-0.4kg)
        int globs = Random.Shared.Next(5, 11);
        for (int i = 0; i < globs; i++)
        {
            resources.Add(Resource.PineResin, RandomWeight(0.08, 0.15));
        }
        return resources;
    }

    private static Inventory GenerateCharDeposit()
    {
        var resources = new Inventory();
        // Charcoal from old fires - useful for crafting and fuel
        // Buffed: 6-12 pieces (was 3-7), weight 0.1-0.18 each (was 0.06-0.12)
        // Total: 0.6-2.2kg (was 0.18-0.84kg)
        int pieces = Random.Shared.Next(6, 13);
        for (int i = 0; i < pieces; i++)
        {
            resources.Add(Resource.Charcoal, RandomWeight(0.1, 0.18));
        }

        // 30% chance of tinder
        if (Random.Shared.NextDouble() < 0.3)
            resources.Add(Resource.Tinder, RandomWeight(0.1, 0.2));

        return resources;
    }

    private static Inventory GenerateAbandonedDen()
    {
        var resources = new Inventory();
        // Bones from old kills
        // Buffed: 3-6 bones (was 2-4), weight 0.25-0.45 each (was 0.15-0.3)
        int bones = Random.Shared.Next(3, 7);
        for (int i = 0; i < bones; i++)
        {
            resources.Add(Resource.Bone, RandomWeight(0.25, 0.45));
        }

        // 50% chance of usable hide scraps (was 40%)
        if (Random.Shared.NextDouble() < 0.5)
            resources.Add(Resource.Hide, RandomWeight(0.25, 0.5));      // +50%

        // 30% chance of sinew
        if (Random.Shared.NextDouble() < 0.3)
            resources.Add(Resource.Sinew, RandomWeight(0.08, 0.15));

        return resources;
    }

    private static Inventory GenerateFishCatch(double densityFactor)
    {
        var resources = new Inventory();
        // Fish - slightly less calorie-dense than meat but good protein source
        // Weight range similar to small game
        resources.Add(Resource.RawFish, RandomWeight(0.3, 0.6) * densityFactor);

        // 50% chance of fish bones
        if (Random.Shared.NextDouble() < 0.5)
            resources.Add(Resource.Bone, RandomWeight(0.05, 0.1) * densityFactor);

        return resources;
    }

    // Elite loot pool generators

    private static Inventory GenerateEliteShelter()
    {
        var inv = new Inventory();

        // Guaranteed: good-condition tool (70-100% durability)
        Gear tool = Random.Shared.Next(3) switch
        {
            0 => Gear.Knife("Flint Knife"),
            1 => Gear.Axe("Flint Axe"),
            _ => Gear.Spear("Wooden Spear")
        };
        tool.Durability = Random.Shared.Next(
            (int)(tool.MaxDurability * 0.7),
            (int)(tool.MaxDurability * 1.0) + 1
        );
        inv.Tools.Add(tool);

        // Guaranteed: preserved food
        inv.Add(Resource.DriedMeat, RandomWeight(0.8, 1.5));

        // 25% chance: high-tier clothing (70-95% durability)
        if (Random.Shared.NextDouble() < 0.25)
        {
            Gear clothing = Random.Shared.Next(2) switch
            {
                0 => Gear.BearHideChest("Bear Hide Coat", 150),
                _ => Gear.MammothHideChest("Mammoth Hide Coat", 200)
            };
            clothing.Durability = Random.Shared.Next(
                (int)(clothing.MaxDurability * 0.7),
                (int)(clothing.MaxDurability * 0.95) + 1
            );
            if (clothing.Slot.HasValue)
                inv.Equipment[clothing.Slot.Value] = clothing;
        }

        // 30% chance: capacity expansion (60-90% durability)
        if (Random.Shared.NextDouble() < 0.3)
        {
            Gear bag = Random.Shared.Next(2) switch
            {
                0 => Gear.LargeBag(100),
                _ => Gear.ProperBelt(150)
            };
            bag.Durability = Random.Shared.Next(
                (int)(bag.MaxDurability * 0.6),
                (int)(bag.MaxDurability * 0.9) + 1
            );
            inv.Tools.Add(bag);
        }

        // Materials
        inv.Add(Resource.CuredHide, RandomWeight(0.5, 1.0));
        if (Random.Shared.NextDouble() < 0.5)
            inv.Add(Resource.Sinew, RandomWeight(0.2, 0.4));

        return inv;
    }

    private static Inventory GenerateMegafaunaGraveyard()
    {
        var inv = new Inventory();

        // Guaranteed: bones (3-6 pieces)
        int boneCount = Random.Shared.Next(3, 7);
        for (int i = 0; i < boneCount; i++)
            inv.Add(Resource.Bone, RandomWeight(0.4, 0.8));

        // Guaranteed: mammoth hide material
        inv.Add(Resource.MammothHide, RandomWeight(1.5, 2.5));

        // 60% chance: ivory
        if (Random.Shared.NextDouble() < 0.6)
            inv.Add(Resource.Ivory, RandomWeight(0.5, 1.2));

        // 25% chance: intact mammoth hide clothing (50-80% durability)
        if (Random.Shared.NextDouble() < 0.25)
        {
            Gear clothing = Random.Shared.Next(2) switch
            {
                0 => Gear.MammothHideChest("Ancient Mammoth Coat", 200),
                _ => Gear.MammothHideLegs("Ancient Mammoth Leggings", 200)
            };
            clothing.Durability = Random.Shared.Next(
                (int)(clothing.MaxDurability * 0.5),
                (int)(clothing.MaxDurability * 0.8) + 1
            );
            if (clothing.Slot.HasValue)
                inv.Equipment[clothing.Slot.Value] = clothing;
        }

        return inv;
    }

    private static Inventory GenerateAncientCache()
    {
        var inv = new Inventory();

        // Guaranteed: high-quality tool (80-100% durability)
        Gear tool = Random.Shared.Next(3) switch
        {
            0 => Gear.BowDrill("Quality Bow Drill"),
            1 => Gear.FireStriker("Flint and Steel"),
            _ => Gear.Knife("Flint Knife")
        };
        tool.Durability = Random.Shared.Next(
            (int)(tool.MaxDurability * 0.8),
            (int)(tool.MaxDurability * 1.0) + 1
        );
        inv.Tools.Add(tool);

        // 40% chance: fishing gear (60-90% durability)
        if (Random.Shared.NextDouble() < 0.4)
        {
            Gear fishing = Random.Shared.Next(2) switch
            {
                0 => Gear.FishingRod("Fishing Rod", 15),
                _ => Gear.FishingNet("Fishing Net", 8)
            };
            fishing.Durability = Random.Shared.Next(
                (int)(fishing.MaxDurability * 0.6),
                (int)(fishing.MaxDurability * 0.9) + 1
            );
            inv.Tools.Add(fishing);
        }

        // 30% chance: large waterskin (70-100% durability)
        if (Random.Shared.NextDouble() < 0.3)
        {
            var waterskin = Gear.WaterContainer("Large Waterskin", weight: 0.4);
            waterskin.Durability = Random.Shared.Next(
                (int)(waterskin.MaxDurability * 0.7),
                (int)(waterskin.MaxDurability * 1.0) + 1
            );
            inv.Tools.Add(waterskin);
        }

        // Fire materials
        inv.Add(Resource.Tinder, RandomWeight(0.3, 0.6));
        inv.Add(Resource.BirchBark, RandomWeight(0.1, 0.2));

        // Preserved food
        if (Random.Shared.NextDouble() < 0.5)
            inv.Add(Resource.DriedMeat, RandomWeight(0.5, 1.0));

        return inv;
    }
}
