using Xunit.Abstractions;

namespace Survival.Tests;

/// Fire in predator boldness: deterrence = fire-fear × intensity × min(1, (6 m / d)²),
/// one formula for the grid and the map. On the map a predator holds on the next tile unless its resolve at the
/// person's position, fire included, reaches the Pressing entry.
public class FireDeterrenceTests(ITestOutputHelper output)
{
    static readonly AgentId P = AgentId.Player;
    static SpeciesStats Wolf => SpeciesTable.Of(Species.Wolf);

    // ---- the formula ----

    [Fact]
    public void Deterrence_IsFullWithin6m_AndFallsOffWithTheSquareOfTheDistanceBeyond()
    {
        double At(double m) => Combat.FireDeterrence(Wolf, [(1.0, m)]);
        double fear = Fire.Deterrence.FireFear;
        Assert.Equal(fear, At(0), 9);
        Assert.Equal(fear, At(3), 9);
        Assert.Equal(fear, At(6), 9);
        Assert.Equal(fear / 4, At(12), 9);
        Assert.Equal(fear * 36 / 225, At(15), 9);
        Assert.True(At(7) < At(6) && At(30) < At(15));
        // Half the intensity, half the deterrence; two flames add.
        Assert.Equal(fear / 2, Combat.FireDeterrence(Wolf, [(0.5, 2)]), 9);
        Assert.Equal(fear * 1.25, Combat.FireDeterrence(Wolf, [(1.0, 2), (0.25, 2)]), 9);
    }

    [Fact]
    public void People_AreNotDeterred() =>
        Assert.Equal(0.0, Combat.FireDeterrence(SpeciesTable.Of(Species.Human), [(1.0, 0)]));

    [Fact]
    public void Intensity_IsFullForAStrongFire_LessAsItDies_AndNothingInEmbers()
    {
        var strong = Pit(new TileId(0, 0), 60, pineKg: 6);
        Assert.Equal(1.0, Fire.IntensityPct(strong), 9);
        var small = Pit(new TileId(0, 0), 60, pineKg: 0, kindlingKg: 2);
        Assert.InRange(Fire.IntensityPct(small), 0.01, 0.99);
        Assert.Equal(0.0, Fire.IntensityPct(new FireState { Tile = new TileId(0, 0), LastUpdatedMinute = 0, HasEmbers = true, EmberDurationHours = 1, EmberHoursRemaining = 1, EmberStartTempF = 600 }));
        Assert.Equal(Fire.Deterrence.TorchIntensityPct, Fire.IntensityPct(new FireState { Tile = new TileId(0, 0), LastUpdatedMinute = 0, CarriedBy = P, TorchMinutesLeft = 30 }));
    }

    // ---- on the map: the hold ----

    static readonly GeneratedMap HoldMap = TestWorlds.Map(1000);

    /// A night on open ground: the player resting on one tile, a pack of three on the next whose den is
    /// the player's tile, worn out, so it makes for its den to sleep there.
    static (World W, TileId Here, AgentId Pack) Camp(int seed)
    {
        var m = HoldMap;
        var here = Enumerable.Range(3, m.Width - 6).SelectMany(x => Enumerable.Range(3, m.Height - 6).Select(y => new TileId(x, y)))
            .First(t => m.TerrainAt(t) == Terrain.Plain && m.CaveAt(t) == Cave.None && MapGen.Manhattan(t, m.Camp) > 30
                && MapGen.Neighbors(t).All(n => Map.Blocked(m, t, n) == null));
        var next = MapGen.Neighbors(here).First();
        var w = new World { Seed = seed, GeneratedMap = m, Minute = 13 * 60 }; // 22:00
        w.Agents[P] = new Agent { Id = P, Tile = here };
        var pack = Agents.SpawnGroup(w, Species.Wolf, next, 3, den: here);
        foreach (var wolf in pack)
        {
            CombatTests.Normalize(w, wolf);
            TestKit.SetHunger(w.Agent(wolf), 0.5);
            w.Agent(wolf).Body.EnergyMinutes = 0.2 * Body.MaxEnergyMinutes; // under Need, above Critical: a fire still holds it off
        }
        return (w, here, pack[0]);
    }

    /// A pit lit from kindling with pine laid on, burned for the half hour before this minute so the pine has caught.
    internal static FireState Pit(TileId tile, long minute, double pineKg, double kindlingKg = 1.0)
    {
        const int LitMinutesBefore = 30;
        var f = new FireState { Tile = tile, LastUpdatedMinute = minute - LitMinutesBefore, Pit = PitType.Open };
        Fire.AddFuel(f, Resource.Kindling, kindlingKg);
        Fire.IgniteAll(f);
        if (pineKg > 0) Fire.AddFuel(f, Resource.Pine, pineKg);
        while (f.LastUpdatedMinute < minute) Fire.Tick(f, new FireId(0), ++f.LastUpdatedMinute, []);
        return f;
    }

    /// Minutes until the pack steps onto the player's tile, the player resting there, fed and watered;
    /// and the fire's intensity then. Null when it never comes within the limit.
    static (int? Minutes, double IntensityPct) UntilItComes(World w, TileId here, AgentId pack, FireState? pit, int limitMinutes)
    {
        var next = w.Agent(pack).Tile;
        for (int minute = 1; minute <= limitMinutes; minute++)
        {
            var me = w.Agent(P).Body;
            Body.Eat(me, 50);
            me.HydrationMl = Body.MaxHydrationMl;
            Sim.Step(w, new Rest(P), 1);
            if (w.Agent(pack).Tile == here) return (minute, pit == null ? 0 : Fire.IntensityPct(pit));
            Assert.Equal(next, w.Agent(pack).Tile); // held, it waits on the next tile, not somewhere else
        }
        return (null, pit == null ? 0 : Fire.IntensityPct(pit));
    }

    [Fact]
    public void WithNoFire_ThePackComesOntoYourTile()
    {
        var (w, here, pack) = Camp(1);
        var (minutes, _) = UntilItComes(w, here, pack, null, 120);
        output.WriteLine($"came at minute {minutes}");
        Assert.NotNull(minutes);
    }

    [Fact]
    public void AtNight_AStrongFireHoldsThePackOnTheNextTile_AndItComesInAsTheFireDies()
    {
        var (w, here, pack) = Camp(1);
        var pit = Pit(here, w.Minute, pineKg: 5);
        w.Fires[new FireId(w.NextFireId++)] = pit;
        Assert.Equal(1.0, Fire.IntensityPct(pit), 9);
        Assert.False(Combat.WouldEnter(w, w.Agent(pack), here));

        var (minutes, intensity) = UntilItComes(w, here, pack, pit, 12 * 60);
        output.WriteLine($"came at minute {minutes}, the fire at {intensity:P0} ({Fire.Phase(pit)})");
        Assert.NotNull(minutes);
        Assert.True(minutes > 60, $"it came in after {minutes} minutes, beside a strong fire");
        Assert.True(intensity < 0.5, $"it came in with the fire still at {intensity:P0}");
    }

    [Fact]
    public void AnNpcsFire_HoldsAPackOffToo()
    {
        var (w, here, pack) = Camp(1);
        w.Agent(P).Tile = MapGen.Neighbors(here).Last();
        Npc.Spawn(w, here);
        Assert.True(Combat.WouldEnter(w, w.Agent(pack), here));
        w.Fires[new FireId(w.NextFireId++)] = Pit(here, w.Minute, pineKg: 5);
        Assert.False(Combat.WouldEnter(w, w.Agent(pack), here));
    }

    [Fact]
    public void AHeadlessFight_ReadsAPitLeftUntendedSinceEarlier()
    {
        var (w, t) = CombatTests.Field();
        var pit = Pit(t, w.Minute - 20, pineKg: 5);
        w.Fires[new FireId(w.NextFireId++)] = pit;
        var npc = Npc.Spawn(w, t);
        var herd = CombatTests.Herd(w, Species.Wolf, 1, t);
        var e = Combat.Create(w, [new Fighter(npc)], [new Fighter(herd)], t, 5, Awareness.Engaged, Awareness.Engaged);
        Combat.ResolveHeadless(w, e, []);
        Assert.Equal(w.Minute, pit.LastUpdatedMinute);
    }

    [Fact]
    public void TheHold_MeasuresThePackAtYourPosition_FireIncluded()
    {
        var (w, here, pack) = Camp(1);
        double open = Combat.ResolveOnTile(w, w.Agent(pack), here)!.Value;
        Assert.True(open >= Combat.PressingEnterResolve, $"three wolves at a lone person's side: resolve {open:F2}");
        var pit = Pit(here, w.Minute, pineKg: 2);
        w.Fires[new FireId(w.NextFireId++)] = pit;
        double lit = Combat.ResolveOnTile(w, w.Agent(pack), here)!.Value;
        Assert.InRange(Fire.IntensityPct(pit), 0.1, 0.99);
        Assert.Equal(open - Fire.Deterrence.FireFear * Fire.IntensityPct(pit), lit, 9);
        // Nobody there: nothing to hold it off.
        Assert.Null(Combat.ResolveOnTile(w, w.Agent(pack), MapGen.Neighbors(here).Last()));
    }

    // ---- in a fight ----

    /// The player and a lone wolf 5 m apart, the player at the given distance from the tile's strong fire,
    /// the wolf on the far side of the player from it.
    static (World W, Encounter E, CombatUnit Wolf, CombatUnit Me) Fight(int playerFromFireM, bool campfire = true, bool torch = false)
    {
        var (w, t) = CombatTests.Field();
        var herd = CombatTests.Herd(w, Species.Wolf, 1, t);
        CombatTests.Normalize(w, herd);
        if (campfire) w.Fires[new FireId(w.NextFireId++)] = Pit(t, w.Minute, pineKg: 6);
        if (torch) w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = t, LastUpdatedMinute = w.Minute, CarriedBy = P, TorchMinutesLeft = 60 };
        var e = Combat.Create(w, [new Fighter(P)], [new Fighter(herd)], t, 5, Awareness.Engaged, Awareness.Engaged);
        var me = Combat.UnitOf(e, P);
        var wolf = e.Units.Single(u => u.Team == 1);
        me.Position = new Cell(e.FireCell.X, e.FireCell.Y - playerFromFireM);
        wolf.Position = new Cell(me.Position.X, me.Position.Y - 5);
        return (w, e, wolf, me);
    }

    [Fact]
    public void InAFight_AFighterByTheFire_HoldsOffAWolfThatPressesOneStanding15mAway()
    {
        var (w, e, wolf, me) = Fight(playerFromFireM: 1);
        var byTheFire = Combat.NextState(FighterState.Standoff, Combat.Resolve(w, e, wolf, me));
        var (w2, e2, wolf2, me2) = Fight(playerFromFireM: 15);
        var away = Combat.NextState(FighterState.Standoff, Combat.Resolve(w2, e2, wolf2, me2));
        output.WriteLine($"by the fire: {Combat.Resolve(w, e, wolf, me):F2} {byTheFire}; 15 m away: {Combat.Resolve(w2, e2, wolf2, me2):F2} {away}");
        Assert.Equal(FighterState.Pressing, away);
        Assert.True(byTheFire is FighterState.GivingGround or FighterState.Fleeing, $"by the fire the wolf is {byTheFire}");
    }

    [Fact]
    public void ATorch_DetersLessThanACampfire_AndGoesWhereItsBearerGoes()
    {
        double Deterred(bool campfire, bool torch, int playerFromFireM)
        {
            var (w, e, wolf, me) = Fight(playerFromFireM, campfire, torch);
            var (wn, en, wolfn, men) = Fight(playerFromFireM, campfire: false);
            return Combat.Resolve(wn, en, wolfn, men) - Combat.Resolve(w, e, wolf, me);
        }
        double torchNear = Deterred(campfire: false, torch: true, playerFromFireM: 0);
        double torchFar = Deterred(campfire: false, torch: true, playerFromFireM: 20);
        double campNear = Deterred(campfire: true, torch: false, playerFromFireM: 0);
        double campFar = Deterred(campfire: true, torch: false, playerFromFireM: 20);
        output.WriteLine($"torch {torchNear:F2} / {torchFar:F2}; campfire {campNear:F2} / {campFar:F2}");
        Assert.True(torchNear > 0 && torchNear < campNear);
        // The wolf is 5 m from the torch wherever its bearer stands; the campfire stays where it is.
        Assert.Equal(torchNear, torchFar, 9);
        Assert.Equal(Fire.Deterrence.FireFear * Fire.Deterrence.TorchIntensityPct, torchNear, 9);
        Assert.True(campFar < campNear / 4);
    }

    [Fact]
    public void InAFight_ThePersonsOwnResolve_IsUntouchedByTheFire()
    {
        var (w, e, wolf, me) = Fight(playerFromFireM: 0);
        var (wn, en, wolfn, men) = Fight(playerFromFireM: 0, campfire: false);
        Assert.Equal(Combat.Resolve(wn, en, men, wolfn), Combat.Resolve(w, e, me, wolf), 9);
    }
}
