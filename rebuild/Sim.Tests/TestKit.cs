using Survival;

/// Gives an agent the gear and fuel the fire tests used to take for granted.
static class TestKit
{
    /// Sets a herd's hunger (Fauna.Hunger) by emptying each member's calorie store that far.
    public static void SetHunger(Agent herd, double hunger)
    {
        foreach (var b in herd.Members) b.CaloriesKcal = Body.CaloriesCapacityKcal(b) * (1 - hunger);
    }

    /// The old game's baseline adult (75 kg, 15% fat, 30% muscle) in the starting clothes: the body
    /// parity goldens were measured on. The rebuild's baseline is the species round's 68 kg man.
    public static BodyState OldBaselineHuman()
    {
        var b = BodyState.Create(BodyKind.Human, 75, 0.15, 0.30);
        b.Worn.AddRange(Items.StartingClothes());
        return b;
    }

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
