namespace Survival.Tests;

/// Verbs.At: the one answer to "what can I do here" (emergent-world-design §3.5), read by the CLI,
/// the UI host and these tests alike.
public class VerbsTests
{
    static readonly AgentId P = AgentId.Player;

    static World Camp(out TileId camp)
    {
        var w = World.New(17);
        camp = Map.Of(w).Camp;
        return w;
    }

    [Fact]
    public void AtCamp_TheTileVerbs_AreWaitRestSleep_AndAWalkEachWay()
    {
        var w = Camp(out _);
        var here = Verbs.At(w, P);
        Assert.Equal(new Activity[] { new Wait(P), new Rest(P), new Sleep(P) },
            here.Tile.Where(v => v.Activity is Wait or Rest or Sleep).Select(v => v.Activity));
        Assert.Equal(new[] { Direction.North, Direction.East, Direction.South, Direction.West },
            here.Walk.Where(v => ((Travel)v.Activity).Pace == Pace.Quick).Select(v => ((Travel)v.Activity).Dir));
    }

    /// Every verb listed, run on a copy of the world, is refused exactly when it says it is, with
    /// the same words; and listing them changes nothing.
    static void AssertEveryVerbIsTrue(World w, AgentId who)
    {
        string before = Save.ToJson(w);
        var verbs = All(Verbs.At(w, who)).ToList();
        Assert.Equal(before, Save.ToJson(w));
        Assert.NotEmpty(verbs);
        foreach (var v in verbs)
        {
            var copy = Save.FromJson(before);
            copy.AccessGates = w.AccessGates; // never saved
            var result = Sim.Step(copy, v.Activity, v.Minutes);
            if (v.Refused != null) Assert.True(result.MinutesElapsed == 0 && result.InterruptedReason == v.Refused,
                $"{v.Label}: listed refused '{v.Refused}', Step said '{result.InterruptedReason}' after {result.MinutesElapsed} min");
            else Assert.True(result.MinutesElapsed > 0, $"{v.Label}: listed allowed, Step refused '{result.InterruptedReason}'");
            // A walk that can start says how long it takes: exactly the crossing Step makes.
            if (v.Activity is Travel && v.Allowed)
                Assert.True(result.InterruptedReason is not ("arrived" or "crossed the pass") || result.MinutesElapsed == v.Trip!.TotalMinutes,
                    $"{v.Label}: listed {v.Trip!.TotalMinutes} min, took {result.MinutesElapsed}");
        }
    }

    static IEnumerable<Verb> All(Here here) => here.All;

    [Fact]
    public void EveryVerb_AtTheStart_IsRefusedExactlyWhenStepRefusesIt()
    {
        var w = Camp(out _);
        Sim.Step(w, new Wait(P), 1);
        AssertEveryVerbIsTrue(w, P);
    }

    /// Against the map's edge and the mountain wall a walk is listed, refused with the map's reason.
    [Fact]
    public void AWalkIntoTheMountains_IsListed_RefusedWithTheMapsReason()
    {
        var w = Camp(out _);
        var m = Map.Of(w);
        var wall = Enumerable.Range(1, m.Height - 1).SelectMany(y => Enumerable.Range(0, m.Width).Select(x => new TileId(x, y)))
            .First(t => m.TerrainAt(t).IsPassable() && !m.TerrainAt(Map.Step(t, Direction.North)).IsPassable());
        w.Agent(P).Tile = wall;
        var north = Verbs.At(w, P).Walk.Single(v => v.Activity == new Travel(P, Direction.North));
        Assert.Equal(Map.Blocked(m, wall, Map.Step(wall, Direction.North)), north.Refused);
        Assert.NotNull(north.Refused);
        AssertEveryVerbIsTrue(w, P);
    }

    /// Pace only matters on hazardous ground, so only a crossing with some is offered carefully too.
    [Fact]
    public void ACarefulWalk_IsOffered_OnlyWhereTheCrossingHasHazardousGround()
    {
        var w = Camp(out _);
        var m = Map.Of(w);
        var start = Enumerable.Range(1, m.Height - 2).SelectMany(y => Enumerable.Range(1, m.Width - 2).Select(x => new TileId(x, y)))
            .First(t => m.TerrainAt(t).IsPassable() && !Traversal.IsHazardous(w, t)
                && Directions.Any(d => Traversal.IsHazardous(w, Map.Step(t, d))) && Directions.Any(d => !Traversal.IsHazardous(w, Map.Step(t, d))));
        w.Agent(P).Tile = start;
        var careful = Verbs.At(w, P).Walk.Where(v => ((Travel)v.Activity).Pace == Pace.Careful).Select(v => ((Travel)v.Activity).Dir);
        Assert.Equal(Directions.Where(d => Traversal.IsHazardous(w, Map.Step(start, d))), careful);
        AssertEveryVerbIsTrue(w, P);
    }

    static readonly Direction[] Directions = [Direction.North, Direction.East, Direction.South, Direction.West];

    /// The player kills a caribou on this tile with a spear in hand: the fight's own aftermath leaves the carcass.
    static Carcass PlayerKill(World w, TileId tile)
    {
        var caribou = Agents.Spawn(w, Species.Caribou, tile, 1);
        var spear = Items.Make("wooden_spear");
        Items.Pack(w, P).Gear.Add(spear);
        var enc = Combat.Create(w, [new Fighter(P) { Weapon = spear }], [new Fighter(caribou)], tile, 2, Awareness.Engaged, Awareness.Unaware);
        w.Agent(caribou).Body.BloodPct = 0;
        Combat.Finish(w, enc, []);
        return w.Tiles[tile].Carcasses!.MaxBy(c => c.Id)!;
    }

    /// A stash of one thing shows taking it as its action; a stash of two is Interact.
    [Fact]
    public void AStash_OffersTakingEachThingInIt()
    {
        var w = Camp(out _);
        TestKit.Supply(w);
        Sim.Step(w, new Stash(P, Resource.Pine, 2), 1);
        Assert.Equal(new Unstash(P, Resource.Pine, 2), Listed(w, ThingKind.Stash).Action!.Activity);
        Sim.Step(w, new Stash(P, Resource.Birch, 3), 1);
        var stash = Listed(w, ThingKind.Stash);
        Assert.True(stash.Interact);
        Assert.Equal(new Activity[] { new Unstash(P, Resource.Pine, 2), new Unstash(P, Resource.Birch, 3) }, stash.Menu.Select(v => v.Activity));
        AssertEveryVerbIsTrue(w, P);
    }

    /// With no pit, lighting one is a tile verb, the best starter and tinder in the pack, with its odds.
    /// A pit is a thing with several verbs: fuel for each fuel carried, melting snow, and lighting it
    /// again once it is out.
    [Fact]
    public void TheFire_IsATileVerbToLight_ThenAPitWithAMenu()
    {
        var w = Camp(out var camp);
        TestKit.Supply(w);
        Sim.Step(w, new Wait(P), 1);
        var light = Verbs.At(w, P).Tile.Single(v => v.Activity is StartFire);
        Assert.Equal(new StartFire(P, ToolType.FireStriker, Resource.BirchBark, 0.05, 1.0), light.Activity);
        var odds = Fire.StartOdds(w, w.Agent(P), ToolType.FireStriker, Resource.BirchBark);
        Assert.Equal(odds.TotalPct, light.Odds!.TotalPct);
        Assert.Equal(odds.Terms, light.Odds.Terms);
        AssertEveryVerbIsTrue(w, P);

        for (int i = 0; i < 20 && !(Fire.PitAt(w, camp) is FireId f && Fire.IsActive(w.Fires[f])); i++) Sim.Step(w, light.Activity, 1);
        var here = Verbs.At(w, P);
        Assert.DoesNotContain(here.Tile, v => v.Activity is StartFire); // the pit's own verb now
        var pit = here.Things.Single(t => t.Thing.Kind == ThingKind.Fire);
        Assert.True(pit.Interact);
        var fuels = Items.Pack(w, P).Kg.Keys.Where(Fire.IsFuel).ToList();
        Assert.Equal(fuels, pit.Menu.Select(v => v.Activity).OfType<AddFuel>().Select(a => a.Fuel));
        Assert.Contains(pit.Menu, v => v.Activity is MeltSnow);
        Assert.Contains(here.Tile, v => v.Activity is LightTorch && v.Allowed);
        AssertEveryVerbIsTrue(w, P);
    }

    /// A thing behind a blocking condition shows it. With no clearing verb yet (step 4.4), the action
    /// shown is the thing's own, refused with the condition's reason.
    [Fact]
    public void ABuriedFind_ShowsItsBlockingCondition_AndItsActionRefusedByIt()
    {
        var w = Camp(out var camp);
        var m = Map.Of(w);
        var (tile, pool) = Enumerable.Range(1, 60).SelectMany(r => new[] { new TileId(camp.X + r, camp.Y), new TileId(camp.X - r, camp.Y), new TileId(camp.X, camp.Y + r) })
            .Where(t => m.InBounds(t) && m.TerrainAt(t).IsPassable())
            .Select(t => (t, Pools.At(w, t).ToList().FindIndex(p => p is StockPool { RevealAtHours: > 0 }))).First(x => x.Item2 >= 0);
        ((PersonMind)w.Agent(P).Mind).Knows.Discovered.Add(new PoolId(tile, pool));
        w.Agent(P).Tile = tile;
        Sim.Step(w, new Wait(P), 1);
        Ground.CatchUp(w, tile).Layers.Add(new SurfaceLayer { Material = SurfaceMaterial.Snow, ThicknessM = 3, CompactionPct = 0.3, FrozenFractionPct = 1 });
        var unblocked = Verbs.At(w, P).Things.Single(t => t.Thing == new ThingRef(ThingKind.Pool, tile, pool));
        Assert.Empty(unblocked.Conditions); // the gate is not wired: buried bars nothing yet

        w.AccessGates = Access.Gate.All;
        var find = Verbs.At(w, P).Things.Single(t => t.Thing == new ThingRef(ThingKind.Pool, tile, pool));
        Assert.Equal([new Condition("buried", Blocking: true)], find.Conditions);
        Assert.Equal(Access.BuriedReason, find.Action!.Refused);
        AssertEveryVerbIsTrue(w, P);
    }

    /// The same rules for anyone: an NPC's list is as true as the player's.
    [Fact]
    public void AnNpcsVerbs_AreAsTrueAsThePlayers()
    {
        var w = Camp(out var camp);
        var npc = Npc.Spawn(w, camp);
        TestKit.Supply(w, npc);
        Sim.Step(w, new Wait(P), 1);
        Assert.DoesNotContain(Verbs.At(w, npc).Things, t => t.Thing == Things.OfAgent(w, npc));
        Assert.Contains(Verbs.At(w, npc).Things, t => t.Thing == Things.OfAgent(w, P));
        AssertEveryVerbIsTrue(w, npc);
    }

    /// In a fight nothing but the fight's own actions can start (they are the combat screen's, Combat.Actions).
    [Fact]
    public void InAFight_EveryVerbIsRefused_AsStepWouldRefuseIt()
    {
        var w = Camp(out var camp);
        Sim.Step(w, new Wait(P), 1);
        var wolf = Agents.Spawn(w, Species.Wolf, camp, 1);
        Combat.Create(w, [new Fighter(P)], [new Fighter(wolf)], camp, 10, Awareness.Engaged, Awareness.Engaged);
        Assert.All(Verbs.At(w, P).All, v => Assert.Equal(Sim.InAFight, v.Refused));
        AssertEveryVerbIsTrue(w, P);
    }

    static ThingVerbs Listed(World w, ThingKind kind) => Verbs.At(w, P).Things.Single(t => t.Thing.Kind == kind);

    /// A carcass offers butchering three ways: several verbs, so Interact; once begun one way, only that way.
    [Fact]
    public void ACarcass_OffersEachWayToButcherIt_UntilOneIsBegun()
    {
        var w = Camp(out var camp);
        Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
        var c = PlayerKill(w, camp);
        Sim.Step(w, new Wait(P), 1);
        var carcass = Listed(w, ThingKind.Carcass);
        Assert.True(carcass.Interact);
        Assert.Equal(Enum.GetValues<ButcherMode>().Select(mode => (Activity)new Butcher(P, c.Id, mode)), carcass.Menu.Select(v => v.Activity));
        AssertEveryVerbIsTrue(w, P);

        Sim.Step(w, new Butcher(P, c.Id, ButcherMode.Careful), 5);
        var begun = Listed(w, ThingKind.Carcass);
        Assert.Equal(new Butcher(P, c.Id, ButcherMode.Careful), begun.Action!.Activity);
        AssertEveryVerbIsTrue(w, P);
    }

    // ---- things here ----

    /// What the person has not found is not there for them; what they know shows its one action.
    [Fact]
    public void TheTilesPools_ShowOnlyTheKnownOnes_EachWithItsOneAction()
    {
        var w = Camp(out var start);
        var m = Map.Of(w);
        // The nearest tile with forage and something still hidden.
        var camp = Enumerable.Range(1, 60).SelectMany(r => new[] { new TileId(start.X + r, start.Y), new TileId(start.X - r, start.Y), new TileId(start.X, start.Y + r) })
            .First(t => m.InBounds(t) && m.TerrainAt(t).IsPassable() && Pools.At(w, t) is var ps && ps.Count > 0 && ps[0] is ForagePool
                && ps.Select((_, i) => new PoolId(t, i)).Any(id => !Pools.Knows(w, P, id)));
        w.Agent(P).Tile = camp;
        Sim.Step(w, new Wait(P), 1);
        var pools = Pools.At(w, camp);
        Assert.Contains(pools.Select((_, i) => new PoolId(camp, i)), id => !Pools.Knows(w, P, id)); // something here is still hidden
        var listed = Verbs.At(w, P).Things.Where(t => t.Thing.Kind == ThingKind.Pool).ToList();
        Assert.Equal(pools.Select((_, i) => i).Where(i => Pools.Knows(w, P, new PoolId(camp, i))), listed.Select(t => t.Thing.Index));
        var forage = listed.Single(t => pools[t.Thing.Index] is ForagePool);
        Assert.Equal(new Gather(P, forage.Thing.Index), forage.Action!.Activity);
        Assert.Equal("Forage", forage.Action.Label);
        AssertEveryVerbIsTrue(w, P);
    }
}
