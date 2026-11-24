using Survival;
using Xunit;
using Xunit.Abstractions;

/// Packs and predators on the planner (spec 2026-11-22-animal-life, "Agents and herds", "The planner", "The animal mind"):
/// packs come from familiarity, and predators hunt through the shared planner. Driven through the world on the planner's strip.
public class PackTests(ITestOutputHelper output)
{
    static TileId T(int x, int y = 2) => new(x, y);

    static List<Agent> Wolves(World w, TileId at, int count, double hunger)
    {
        var pack = Agents.SpawnGroup(w, Species.Wolf, at, count).Select(w.Agent).ToList();
        foreach (var wolf in pack)
        {
            TestKit.SetHunger(wolf, hunger);
            wolf.Body.EnergyMinutes = Body.MaxEnergyMinutes;
        }
        return pack;
    }

    /// A herd of four bison six tiles off on the strip: a pair of wolves leaves them alone (a kill chance of 0.39 against a
    /// herd of four, under an even chance), and a pack of eight (six hunting) takes them on (0.89), through Combat.KillChance.
    [Theory]
    [InlineData(2, false)]
    [InlineData(8, true)]
    public void APairOfWolves_LeavesBisonAlone_AndAPackOfEightTakesThemOn(int wolves, bool hunts)
    {
        var w = PlannerTests.Strip();
        var bison = Agents.SpawnGroup(w, Species.Bison, T(26), 4);
        var pack = Wolves(w, T(20), wolves, hunger: 0.8);
        var fought = new List<WorldEvent>();
        for (int i = 0; i < 6 * 60 && fought.Count == 0; i++)
            fought.AddRange(Sim.Advance(w, 1).Where(e => e is EncounterEnded f && bison.Contains(f.Agent) || e is Killed k && bison.Contains(k.Agent)));
        output.WriteLine($"{wolves} wolves: {string.Join(", ", fought)}");
        Assert.Equal(hunts, fought.Count > 0);
    }

    /// With nothing sensed, a hungry pack roams: it heads off to the edge of its sight, together, and goes on from there.
    [Fact]
    public void APackWithNothingSensed_Roams_Together()
    {
        var w = PlannerTests.Strip();
        var pack = Wolves(w, T(20), 3, hunger: 0.8);
        var visited = new HashSet<TileId>();
        int apart = 0;
        for (int i = 0; i < 12 * 60; i++)
        {
            Sim.Advance(w, 1);
            visited.Add(pack[0].Tile);
            if (pack.Max(a => MapGen.Manhattan(a.Tile, pack[0].Tile)) > 1) apart++;
        }
        output.WriteLine($"{visited.Count} tiles, apart {apart} minutes");
        Assert.True(visited.Count >= 10, $"it roamed over {visited.Count} tiles");
        Assert.True(visited.Max(t => Math.Abs(t.X - 20)) >= 8, "it never went far");
        Assert.True(apart < 60, $"the pack was spread over more than a tile for {apart} minutes");
    }

    /// Unfamiliar wolves are rivals: two hungry packs three tiles apart with nothing to hunt range about, and keep apart,
    /// giving way when they come near.
    [Fact]
    public void RivalPacks_GiveWay_AndKeepApart()
    {
        const double Hungry = 0.8;
        var w = PlannerTests.Strip();
        // Dens three tiles apart.
        var one = Agents.SpawnGroup(w, Species.Wolf, T(18), 3).Select(w.Agent).ToList();
        var other = Agents.SpawnGroup(w, Species.Wolf, T(22), 3).Select(w.Agent).ToList();
        foreach (var wolf in one.Concat(other)) TestKit.SetHunger(wolf, Hungry);
        Assert.DoesNotContain(other, o => Fauna.IsKin(one[0], o));
        int together = 0;
        var visited = new HashSet<TileId>();
        for (int i = 0; i < 24 * 60; i++)
        {
            Sim.Advance(w, 1);
            foreach (var wolf in one.Concat(other)) visited.Add(wolf.Tile);
            if (i % 60 == 0) foreach (var wolf in one.Concat(other)) TestKit.SetHunger(wolf, Hungry);
            if (one.Any(a => other.Any(b => MapGen.Manhattan(a.Tile, b.Tile) <= 1))) together++;
        }
        output.WriteLine($"within a tile of each other {together} of {24 * 60} minutes; tiles {visited.Count}");
        Assert.True(visited.Count >= 5, $"the packs kept to {visited.Count} tiles: they never ranged");
        // Giving way, a pack steps off a rival it has just seen.
        Assert.True(together < 240, $"the rival packs were within a tile of each other {together} minutes");
        Assert.DoesNotContain(other, o => one.Any(a => Fauna.IsKin(a, o)));
    }

    /// Familiarity grows on one tile and fades apart: two wolves that barely know each other (just over kin) are fast
    /// packmates after a day together, and strangers after two days apart.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Familiarity_GrowsTogether_AndFadesApart(bool together)
    {
        var w = PlannerTests.Strip();
        var a = Wolves(w, T(5), 1, hunger: 0)[0];
        var b = Wolves(w, together ? T(5) : T(35), 1, hunger: 0)[0];
        Fauna.MindOf(a).Familiar = new() { [b.Id] = 0.26 };
        Fauna.MindOf(b).Familiar = new() { [a.Id] = 0.26 };
        for (int h = 0; h < (together ? 1 : 2) * 24; h++)
        {
            Sim.Advance(w, 60);
            foreach (var wolf in new[] { a, b }) TestKit.SetHunger(wolf, 0); // fed, so neither roams to the other
        }
        output.WriteLine($"{Fauna.Familiarity(a, b.Id):F3} / {Fauna.Familiarity(b, a.Id):F3}");
        Assert.Equal(together, Fauna.IsKin(a, b));
        Assert.Equal(together, Fauna.IsKin(b, a));
        if (together) Assert.True(Fauna.Familiarity(a, b.Id) > 0.9);
    }

    /// One packmate attacking another: they are strangers after the fight, both ways.
    [Fact]
    public void PackmatesWhoFight_AreStrangersAfter()
    {
        var w = PlannerTests.Strip();
        var pack = Wolves(w, T(20), 3, hunger: 0);
        var enc = Combat.Create(w, [new Fighter(pack[0].Id)], [new Fighter(pack[1].Id)], T(20), 5, Awareness.Engaged, Awareness.Engaged);
        Combat.ResolveHeadless(w, enc, []);
        Assert.False(Fauna.IsKin(pack[0], pack[1]));
        Assert.False(Fauna.IsKin(pack[1], pack[0]));
        Assert.True(Fauna.IsKin(pack[0], pack[2]));
    }

    /// A kill with six wolves on it is defended harder than one with two: a hunter with a spear coming for a wolf on its
    /// kill drives the pair off more often than the six (each defends with its own boldness and the packmates there).
    [Fact]
    public void AKillWithSixWolves_IsDefendedHarder_ThanOneWithTwo()
    {
        const int Trials = 100;
        double hurt = 0;
        int GivenUp(int wolves)
        {
            int given = 0;
            hurt = 0;
            for (int seed = 0; seed < Trials; seed++)
            {
                var w = PlannerTests.Strip();
                var tile = T(20);
                for (int skip = 0; skip < seed; skip++) Agents.Remove(w, Agents.Spawn(w, Species.Hare, tile));
                Combat.LeaveCarcass(w, tile, Species.Caribou, BodyState.Create(SpeciesTable.Of(Species.Caribou).Body, 110, 0.08, 0.47), new Cause(null, "a test"));
                var pack = Wolves(w, tile, wolves, hunger: 0.3);
                foreach (var wolf in pack) Fauna.MindOf(wolf).Choice = new Feed(wolf.Id);
                Assert.True(Fauna.HoldsAKill(w, pack[0]));
                var hunter = Npc.Spawn(w, tile);
                var spear = Items.Make("wooden_spear");
                Items.PackOf(w.Agent(hunter))!.Gear.Add(spear);
                var enc = Fauna.BeginHunt(w, hunter, pack[0].Id, spear);
                var result = Combat.ResolveHeadless(w, enc, []);
                if (result is CombatResult.Victory or CombatResult.AnimalFled or CombatResult.AnimalDisengaged) given++;
                hurt += w.Agents.TryGetValue(hunter, out var h) ? Combat.WoundLevel(h.Body) : 1;
            }
            return given;
        }
        int two = GivenUp(2);
        double hurtByTwo = hurt;
        int six = GivenUp(6);
        output.WriteLine($"hunter's wounds: two {hurtByTwo / Trials:F2}, six {hurt / Trials:F2}");
        output.WriteLine($"given up: two {two} of {Trials}, six {six} of {Trials}");
        Assert.True(six < two, $"six gave up {six} of {Trials}, two {two}");
    }

    /// A young wolf leaves a big hungry pack when going alone pays: six starving wolves sensing a caribou, whose share of
    /// it is a sixth, lose their youngest; three, whose third is worth more than a lone wolf's chance at the whole, keep it.
    [Theory]
    [InlineData(6, true)]
    [InlineData(3, false)]
    public void AYoungWolf_LeavesABigHungryPack_WhenGoingAlonePays(int size, bool leaves)
    {
        var w = PlannerTests.Strip();
        var pack = Wolves(w, T(20), size, hunger: 0.95);
        foreach (var wolf in pack) wolf.Body.FatKg = 0.005 * wolf.Body.WeightKg; // a fat store all but gone: room for most of a kill
        Agents.Spawn(w, Species.Caribou, T(26));
        Sim.Advance(w, 2);
        var youngest = pack.MaxBy(a => a.Id)!;
        Assert.Equal(leaves, !pack.Where(a => a != youngest).Any(a => Fauna.IsKin(a, youngest)));
        Assert.All(pack.Where(a => a != youngest), a => Assert.True(pack.Where(b => b != youngest && b != a).All(b => Fauna.IsKin(a, b))));
    }
}
