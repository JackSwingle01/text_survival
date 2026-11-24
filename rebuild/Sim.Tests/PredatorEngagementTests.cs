using Xunit.Abstractions;

namespace Survival.Tests;

/// Predators come for people as for any other prey: a pack's hunt choice weighs every person in reach
/// by its resolve toward them (Combat.ResolveToward), the same line that lets it onto their tile. It
/// walks to them tile by tile, and the fight opens ready or as an ambush by what the person saw.
/// A person who sees a predator come into sight or nearer stops what they are doing.
public class PredatorEngagementTests(ITestOutputHelper output)
{
    static readonly AgentId P = AgentId.Player;
    static readonly GeneratedMap Valley = TestWorlds.Map(1000);
    /// 22:00 and 12:00: world minute 0 is 09:00.
    const long Night = 13 * 60, Noon = 3 * 60;

    /// A tile of open plain with a straight run of open plain east of it, far from camp: nobody but who
    /// a test puts there.
    static (World W, TileId Here) Open(long minute)
    {
        var m = Valley;
        var here = Enumerable.Range(3, m.Width - 12).SelectMany(x => Enumerable.Range(3, m.Height - 6).Select(y => new TileId(x, y)))
            .First(t => MapGen.Manhattan(t, m.Camp) > 30 && Enumerable.Range(-1, 8).All(dx =>
                Enumerable.Range(-1, 3).All(dy => m.TerrainAt(new TileId(t.X + dx, t.Y + dy)) == Terrain.Plain
                    && m.CaveAt(new TileId(t.X + dx, t.Y + dy)) == Cave.None)));
        var w = new World { Seed = 1, GeneratedMap = m, Minute = minute };
        w.Agents[P] = new Agent { Id = P, Tile = here };
        return (w, here);
    }

    static TileId East(TileId t, int tiles) => new(t.X + tiles, t.Y);

    /// The tests' minutes (Night, Noon) fall on its stalking clock (Fauna.ComeForPeople, every half hour).
    static AgentId Pack(World w, TileId tile, int count = 3, double hunger = 0.75)
    {
        var pack = Agents.SpawnGroup(w, Species.Wolf, tile, count);
        foreach (var wolf in pack)
        {
            CombatTests.Normalize(w, wolf);
            TestKit.SetHunger(w.Agent(wolf), hunger);
        }
        return pack[0];
    }

    /// The person a test is about: the player where they stand, or an NPC there with the player sent far off.
    static AgentId Person(World w, TileId here, bool player)
    {
        if (player) return P;
        w.Agent(P).Tile = new TileId(here.X, here.Y - 40 >= 0 ? here.Y - 40 : here.Y + 40);
        return Npc.Spawn(w, here);
    }

    static void StrongFire(World w, TileId tile) => w.Fires[new FireId(w.NextFireId++)] = FireDeterrenceTests.Pit(tile, w.Minute, pineKg: 5);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AHungryPack_OnALonePersonsTile_AtNight_ComesForThem_PlayerAndNpcAlike(bool player)
    {
        var (w, here) = Open(Night);
        var who = Person(w, here, player);
        var pack = Pack(w, here);
        var (activity, _) = Fauna.Decide(w, w.Agent(pack), []);
        var hunt = Assert.IsType<Hunt>(activity);
        Assert.Equal(who, hunt.Prey);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BesideAStrongFire_TheSamePackDoesNotComeFor_PlayerOrNpc(bool player)
    {
        var (w, here) = Open(Night);
        var who = Person(w, here, player);
        StrongFire(w, here);
        var pack = Pack(w, here);
        var (activity, _) = Fauna.Decide(w, w.Agent(pack), []);
        Assert.False(activity is Hunt h && h.Prey == who, $"it chose {activity} beside a roaring fire");
    }

    /// The player resting with a hungry pack come onto their tile: the rest stops, the fight is on.
    [Fact]
    public void ThePacksArrival_StopsThePlayersRest_AndTheFightOpens()
    {
        var (w, here) = Open(Night);
        var pack = Pack(w, here);
        var r = Sim.Step(w, new Rest(P), 120);
        Assert.Equal(StepOutcome.Interrupted, r.Outcome);
        Assert.Equal(Sim.SetUpon, r.Reason);
        var enc = Assert.IsType<Encounter>(Combat.EncounterOf(w, P));
        Assert.Contains(enc.Units, u => u.Agent == pack);
        Assert.Equal(1, r.MinutesElapsed);
    }

    /// How the fight with the player opens, the pack hungry on their tile at night: whether they start
    /// unaware, and how far off the nearest wolf is.
    static (FighterState State, double NearestM) Opening(bool sawIt, Activity doing)
    {
        var (w, here) = Open(Night);
        var pack = Pack(w, here);
        if (sawIt) Memories.Of(w.Agent(P)).Sightings[pack] = new Sighting(East(here, 1), Species.Wolf, 3, w.Minute);
        Sim.Step(w, doing, 5);
        var enc = Assert.IsType<Encounter>(Combat.EncounterOf(w, P));
        var me = Combat.UnitOf(enc, P);
        return (Combat.StateOf(w, me), enc.Units.Where(u => u.Team == 1).Min(u => u.Position.DistanceTo(me.Position)));
    }

    /// The opening reads what the person saw: a fresh sighting of this pack, ready and further off; none, an ambush.
    [Fact]
    public void SawItComing_MeetsItReadyFurtherOff_ElseIsAmbushed()
    {
        var ready = Opening(sawIt: true, new Rest(P));
        var ambushed = Opening(sawIt: false, new Rest(P));
        output.WriteLine($"saw it: {ready}; did not: {ambushed}");
        Assert.NotEqual(FighterState.Unaware, ready.State);
        Assert.Equal(FighterState.Unaware, ambushed.State);
        Assert.True(ready.NearestM > ambushed.NearestM);
    }

    /// Asleep, a sighting from before lying down does not make you ready.
    [Fact]
    public void LyingDownAfterSeeingIt_IsStillAnAmbush() =>
        Assert.Equal(FighterState.Unaware, Opening(sawIt: true, new Sleep(P)).State);

    /// Set upon at the start of a crossing: the step stops there, on the fight's tile, and no walking is done.
    [Fact]
    public void SetUponWhileSettingOut_TheCrossingStopsOnTheFightsTile()
    {
        var (w, here) = Open(Night);
        Pack(w, here);
        var r = Sim.Step(w, new Travel(P, Direction.East), 30);
        Assert.Equal(Sim.SetUpon, r.Reason);
        Assert.Equal(here, w.Agent(P).Tile);
        Assert.NotNull(Combat.EncounterOf(w, P));
    }

    /// A player who has crossed the pass is out of the world: nothing comes for them there.
    [Fact]
    public void APlayerWhoHasCrossed_IsNoOnesPrey()
    {
        var (w, _) = Open(Night);
        var exit = Map.Of(w).Exit;
        w.Agent(P).Tile = exit;
        var pack = Pack(w, exit);
        var (activity, _) = Fauna.Decide(w, w.Agent(pack), []);
        Assert.False(activity is Hunt, $"it chose {activity}");
    }

    /// Hungry, it walks toward a lone person it senses; once a strong fire is lit by them, or someone
    /// comes to stand with them, its resolve no longer reaches the line and it stops coming.
    [Theory]
    [InlineData("fire")]
    [InlineData("company")]
    public void APackWhoseResolveDrops_OnTheWay_StopsComing(string change)
    {
        var (w, here) = Open(Night);
        var start = East(here, 4);
        var pack = Pack(w, start, count: 2);
        var (first, _) = Fauna.Decide(w, w.Agent(pack), []);
        var step = Assert.IsType<Travel>(first);
        Assert.True(MapGen.Manhattan(Map.Step(start, step.Dir), here) < MapGen.Manhattan(start, here), $"stepped {step.Dir}, away from the person");

        if (change == "fire") StrongFire(w, here);
        else Npc.Spawn(w, here);
        var (then, _) = Fauna.Decide(w, w.Agent(pack), []);
        // Still hungry, it may roam off for prey (a food plan to a tile that pays nothing), whichever way that lies.
        bool roaming = Fauna.MindOf(w.Agent(pack)).Plan is { Stat: Stat.Food, At: not null, PerMinute: 0 };
        Assert.False(!roaming && then is Travel t && MapGen.Manhattan(Map.Step(start, t.Dir), here) < MapGen.Manhattan(start, here),
            $"with {change} it still came on: {then}");
    }

    /// Each other person on the target's tile takes off what each member of a pack adds (the mirror of
    /// the pack-size term), and the resolve falls further for the pressure they put on it.
    [Fact]
    public void AnotherPersonOnTheTile_LowersThePacksBoldnessAndResolve()
    {
        var (w, here) = Open(Noon);
        var pack = Pack(w, East(here, 1));
        double alone = Combat.BoldnessToward(w, w.Agent(pack), w.Agent(P)), resolveAlone = Combat.ResolveToward(w, w.Agent(pack), w.Agent(P));
        Npc.Spawn(w, here);
        double withCompany = Combat.BoldnessToward(w, w.Agent(pack), w.Agent(P)), resolveWith = Combat.ResolveToward(w, w.Agent(pack), w.Agent(P));
        Assert.Equal(alone - SpeciesTable.Of(Species.Wolf).Temperament.PerMember, withCompany, 9);
        Assert.True(resolveWith < resolveAlone);
    }

    // ---- the long-action rule ----

    static void Fed(World w)
    {
        var me = w.Agent(P).Body;
        Body.Eat(me, 500);
        me.HydrationMl = Body.MaxHydrationMl;
    }

    [Fact]
    public void APredatorComingIntoSight_StopsALongRest_AndOneThatStaysPutDoesNot()
    {
        var (w, here) = Open(Noon);
        Fed(w);
        var pack = Pack(w, East(here, 5), hunger: 0.05);
        var first = Sim.Step(w, new Rest(P), 120);
        Assert.Equal(StepOutcome.Interrupted, first.Outcome);
        Assert.Equal(Sim.PredatorInSight, first.Reason);

        var again = Sim.Step(w, new Rest(P), 40);
        Assert.Equal(StepOutcome.Finished, again.Outcome);
        Assert.Equal(40, again.MinutesElapsed);

        w.Agent(pack).Tile = East(here, 3);
        var closer = Sim.Step(w, new Rest(P), 40);
        Assert.Equal(Sim.PredatorInSight, closer.Reason);
        Assert.True(closer.MinutesElapsed <= Memories.GlanceMinutes, $"it took {closer.MinutesElapsed} minutes to notice");
    }

    /// Walking up to a pack that stands still is not the pack coming on.
    [Fact]
    public void WalkingTowardAPackStandingStill_DoesNotStopTheWalk()
    {
        var (w, here) = Open(Noon);
        Fed(w);
        Pack(w, East(here, 6), hunger: 0.05);
        Sim.Step(w, new Rest(P), 1); // seen
        var r = Sim.Step(w, new Travel(P, Direction.East), 30);
        Assert.Equal(Traversal.Arrived, r.Reason);
    }

    [Fact]
    public void Asleep_NobodyLooks_SoAPredatorComingIntoSightDoesNotWakeYou()
    {
        var (w, here) = Open(Noon);
        Fed(w);
        Pack(w, East(here, 5), hunger: 0.05);
        var r = Sim.Step(w, new Sleep(P), 30);
        Assert.NotEqual(Sim.PredatorInSight, r.Reason);
    }

    [Fact]
    public void AnNpcKeepingAtSomething_StopsWhenAPredatorComesIntoSight()
    {
        var (w, here) = Open(Noon);
        var npc = Person(w, here, player: false);
        var rest = new Rest(npc);
        w.Agent(npc).Doing = new Doing { Plain = rest, MinutesLeft = 120 };
        Pack(w, East(here, 5), hunger: 0.05);
        for (int i = 0; i < Memories.GlanceMinutes && w.Agent(npc).Doing?.Activity == rest; i++) Sim.Advance(w, 1);
        Assert.NotSame(rest, w.Agent(npc).Doing?.Activity);
    }

    [Fact]
    public void AnNpcSetUponMidActivity_DecidesAfresh()
    {
        var (w, here) = Open(Night);
        var npc = Person(w, here, player: false);
        var rest = new Rest(npc);
        w.Agent(npc).Doing = new Doing { Plain = rest, MinutesLeft = 600 };
        var pack = Pack(w, here);
        var events = Sim.Advance(w, 1);
        Assert.Contains(events.OfType<EncounterEnded>(), f => f.Agent == npc);
        Assert.Contains(events.OfType<EncounterEnded>(), f => f.Agent == pack);
        Assert.NotSame(rest, w.Agent(npc).Doing?.Activity);
    }
}
