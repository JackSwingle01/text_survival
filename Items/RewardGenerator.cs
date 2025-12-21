namespace text_survival.Items;

public enum RewardPool
{
    None,
    BasicSupplies,      // Sticks, tinder, berries - common finds
    AbandonedCamp,      // Left-behind tool + supplies
    HiddenCache,        // Valuable tool + good fuel
    BasicMeat,          // Small amount of raw meat (scavenged)
    LargeMeat,          // Significant meat haul (thorough butchering)
    GameTrailDiscovery  // Minor supplies (info reward placeholder)
}

public static class RewardGenerator
{
    public static FoundResources Generate(RewardPool pool)
    {
        return pool switch
        {
            RewardPool.BasicSupplies => GenerateBasicSupplies(),
            RewardPool.AbandonedCamp => GenerateAbandonedCamp(),
            RewardPool.HiddenCache => GenerateHiddenCache(),
            RewardPool.BasicMeat => GenerateBasicMeat(),
            RewardPool.LargeMeat => GenerateLargeMeat(),
            RewardPool.GameTrailDiscovery => GenerateGameTrailDiscovery(),
            _ => new FoundResources()
        };
    }

    private static FoundResources GenerateBasicSupplies()
    {
        var resources = new FoundResources();

        // Roll 1-2 items
        int itemCount = Random.Shared.Next(1, 3);
        var options = new List<Action>
        {
            () => resources.AddStick(RandomWeight(0.2, 0.5), "a sturdy branch"),
            () => resources.AddTinder(RandomWeight(0.1, 0.3), "some dry bark"),
            () => resources.AddBerries(RandomWeight(0.1, 0.25), null),
            () => resources.AddLog(RandomWeight(0.8, 1.5), "a small log")
        };

        // Shuffle and pick
        var shuffled = options.OrderBy(_ => Random.Shared.Next()).Take(itemCount);
        foreach (var add in shuffled)
        {
            add();
        }

        return resources;
    }

    private static FoundResources GenerateAbandonedCamp()
    {
        var resources = new FoundResources();

        // Random tool left behind
        var tools = new (Func<Tool> create, string desc)[]
        {
            (() => Tool.Knife("Bone Knife"), "an abandoned bone knife"),
            (() => Tool.Axe("Stone Axe"), "a worn stone axe"),
            (() => Tool.Spear("Wooden Spear"), "a forgotten spear")
        };

        var (create, desc) = tools[Random.Shared.Next(tools.Length)];
        resources.AddTool(create(), desc);

        // Plus some tinder or a stick
        if (Random.Shared.Next(2) == 0)
            resources.AddTinder(RandomWeight(0.2, 0.4), "a leftover tinder bundle");
        else
            resources.AddStick(RandomWeight(0.2, 0.4), "some kindling");

        return resources;
    }

    private static FoundResources GenerateHiddenCache()
    {
        var resources = new FoundResources();

        // Valuable tool - fire striker is most valuable
        if (Random.Shared.Next(3) == 0)
        {
            resources.AddTool(Tool.FireStriker("Flint and Steel"), "flint and steel");
        }
        else
        {
            var tools = new (Func<Tool> create, string desc)[]
            {
                (() => Tool.Knife("Flint Knife"), "a sharp flint knife"),
                (() => Tool.Axe("Flint Axe"), "a quality flint axe")
            };
            var (create, desc) = tools[Random.Shared.Next(tools.Length)];
            resources.AddTool(create(), desc);
        }

        // Plus good fuel
        resources.AddLog(RandomWeight(1.5, 2.5), "a seasoned hardwood log");

        return resources;
    }

    private static FoundResources GenerateBasicMeat()
    {
        var resources = new FoundResources();
        // Quick scavenge - small amount of meat
        resources.AddRawMeat(RandomWeight(0.3, 0.6), "some scavenged meat");
        return resources;
    }

    private static FoundResources GenerateLargeMeat()
    {
        var resources = new FoundResources();
        // Thorough butchering - significant haul
        int cuts = Random.Shared.Next(2, 4); // 2-3 portions
        for (int i = 0; i < cuts; i++)
        {
            resources.AddRawMeat(RandomWeight(0.4, 0.8), null);
        }
        return resources;
    }

    private static FoundResources GenerateGameTrailDiscovery()
    {
        var resources = new FoundResources();
        // Minor supplies as placeholder - info reward in future
        if (Random.Shared.Next(2) == 0)
            resources.AddStick(RandomWeight(0.2, 0.4), "a walking stick");
        else
            resources.AddTinder(RandomWeight(0.1, 0.2), "some dried grass");
        return resources;
    }

    private static double RandomWeight(double min, double max)
    {
        return min + Random.Shared.NextDouble() * (max - min);
    }
}
