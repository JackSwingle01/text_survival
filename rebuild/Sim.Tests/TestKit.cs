using Survival;

/// Gives an agent the gear and fuel the fire tests used to take for granted.
static class TestKit
{
    public static World Supply(World world, AgentId? who = null)
    {
        var pack = Items.Pack(world, who ?? AgentId.Player);
        pack.Gear.Add(Items.Make("fire_striker"));
        for (int i = 0; i < 3; i++) pack.Gear.Add(Items.Make("torch"));
        foreach (var (r, kg) in new[] { (Resource.BirchBark, 1.0), (Resource.Kindling, 10.0), (Resource.Birch, 10.0), (Resource.Pine, 5.0), (Resource.Bone, 2.0) })
            Items.Add(pack, r, kg);
        return world;
    }
}
