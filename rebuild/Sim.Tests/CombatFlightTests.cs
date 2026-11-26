namespace Survival.Tests;

/// Flight and catch-up: the closed-form chase, its odds and thresholds, what a
/// caught and an uncaught flight leave, the player's chase, and breaking off at 30 m.
public class CombatFlightTests
{
    static readonly AgentId P = AgentId.Player;
    static CombatUnit Me(Encounter e) => Combat.UnitOf(e, P);
    static CombatUnit B(Encounter e, int i = 0) => e.Units.Where(u => u.Team == 1).ElementAt(i);

    static (World W, Encounter E) PlayerVs(Species species, int distanceM, int count = 1)
    {
        var (w, t) = CombatTests.Field();
        var herd = CombatTests.Herd(w, species, count, t);
        CombatTests.Normalize(w, herd);
        return (w, Combat.Create(w, [new Fighter(P)], [.. Enumerable.Range(0, count).Select(i => new Fighter(herd[i]))], t, distanceM,
            Awareness.Engaged, Awareness.Engaged));
    }

    static void Gap(Encounter e, CombatUnit a, CombatUnit b, int metres) => b.Position = new Cell(a.Position.X, a.Position.Y + metres);

    // ---- the odds (research §5.4, frontier Q13's consequences) ----

    [Fact]
    public void APerson_NeverCatchesAHealthyCaribou()
    {
        var (w, e) = PlayerVs(Species.Caribou, 15);
        Assert.True(Combat.CatchOdds(w, Me(e), B(e)).Pct < 0.05);
    }

    [Fact]
    public void AWolf_AlwaysCatchesAPerson()
    {
        var (w, e) = PlayerVs(Species.Wolf, 10);
        Assert.True(Combat.CatchOdds(w, B(e), Me(e)).Pct > 0.95);
    }

    static (World W, Encounter E) WolfAndCaribou(int distanceM)
    {
        var (w, t) = CombatTests.Field();
        var wolf = CombatTests.Herd(w, Species.Wolf, 1, t);
        var caribou = CombatTests.Herd(w, Species.Caribou, 1, t);
        CombatTests.Normalize(w, wolf);
        CombatTests.Normalize(w, caribou);
        var e = Combat.Create(w, [new Fighter(wolf)], [new Fighter(caribou)], t, distanceM, Awareness.Engaged, Awareness.Engaged);
        Gap(e, e.Units[0], e.Units[1], distanceM);
        return (w, e);
    }

    /// Research §5.4's worked example, by hand: a rested wolf 100 m behind a rested caribou. The caribou's
    /// 20 s sprint opens the gap; the wolf's 40 s one closes it, just, at about 40 s. With the square-root
    /// fade the game's runners use, the caribou's sprint averages 13 m/s (7 + 9 x 2/3) and the wolf's 12
    /// (8 + 6 x 2/3): 260 + 7 x 20 = 400 m against 480 m at 40 s, short of the 99 m needed, so the wolf
    /// rarely makes it inside its 240 s limit at endurance speeds of 8 against 7: a long chase, about even.
    [Fact]
    public void AWolf_100mBehindACaribou_IsALongChase_AboutEven()
    {
        var (w, e) = WolfAndCaribou(100);
        var odds = Combat.CatchOdds(w, e.Units[0], e.Units[1]);
        Assert.InRange(odds.Pct, 0.3, 0.99);
        Assert.InRange(odds.Seconds, 40, 120); // the worked example closes at about 40 s; the fade puts it later
    }

    /// Noticed at 15 m, the pack's wolf usually runs a caribou down (about 0.91 with today's table; the frontier
    /// guessed about half, research §5.4 says most real chases fail): the number the tuner will move.
    [Fact]
    public void AWolf_RunsDownACaribouNoticedAt15m_Usually()
    {
        var (w, e) = WolfAndCaribou(15);
        Assert.InRange(Combat.CatchOdds(w, e.Units[0], e.Units[1]).Pct, 0.8, 0.97);
    }

    [Fact]
    public void AFasterRunner_GetsAway_EvenFromContact()
    {
        var (w, e) = PlayerVs(Species.Caribou, 1);
        Gap(e, Me(e), B(e), 1);
        Assert.True(Combat.CatchOdds(w, Me(e), B(e)).Pct < 0.05);
    }

    [Fact]
    public void ASpentRunner_IsEasierToCatch()
    {
        var (w, e) = WolfAndCaribou(60);
        double fresh = Combat.CatchOdds(w, e.Units[0], e.Units[1]).Pct;
        Combat.BodyOf(w, e.Units[1]).SprintSpentLevel = 1;
        double spent = Combat.CatchOdds(w, e.Units[0], e.Units[1]).Pct;
        Assert.True(fresh < 0.99 && spent > fresh + 0.05, $"fresh {fresh:0.000}, spent {spent:0.000}");
    }

    // ---- resolving a flight ----

    [Fact]
    public void UnderFivePercent_TheRunnerIsGoneAtOnce()
    {
        var (w, e) = PlayerVs(Species.Wolf, 10);
        var wolf = B(e);
        Me(e).State = FighterState.Pressing;
        wolf.State = FighterState.Standoff;
        Me(e).Target = e.Units.IndexOf(wolf);
        Combat.BodyOf(w, Me(e)).SprintSpentLevel = 0;
        Assert.True(Combat.CatchOdds(w, Me(e), wolf).Pct < 0.05); // a person cannot catch a wolf
        Assert.Null(Combat.Apply(w, e, wolf, CombatAction.Flee, []).Refused);
        Assert.Equal(FighterState.Gone, Combat.StateOf(w, wolf));
        Assert.NotNull(wolf.FledHeading);
    }

    [Fact]
    public void Caught_TheFightGoesOnAtContact_TheRunnerUndefended()
    {
        var (w, e) = PlayerVs(Species.Wolf, 3);
        var wolf = B(e);
        wolf.State = FighterState.Pressing;
        var events = new List<WorldEvent>();
        Combat.Apply(w, e, Me(e), CombatAction.Flee, events);
        Assert.True(Me(e).InField, "a wolf three metres off let a person get away");
        Assert.True(wolf.Position.DistanceTo(Me(e).Position) <= 1.5);
        Assert.Equal(FighterState.Fleeing, Me(e).State);
        Assert.Equal(0, Combat.DefenceChance(w, e, Me(e), wolf));
    }

    [Fact]
    public void NotCaught_TheChasersAreGoneToo_AndTheHerdRunsOnAlongTheHeading()
    {
        // Two wolves after a fresh caribou 25 m off: rolled once, away about one chase in a hundred and fifty, so
        // the scan starts at a seed where it got away (re-pinned to 224, the first such seed on 2026-11-26, when the
        // scan from 150 passed CI's 5 s limit: each seed builds a whole world).
        for (int seed = 224; seed <= 524; seed++)
        {
            var (w, t) = CombatTests.Field(World.New(seed));
            var pack = CombatTests.Herd(w, Species.Wolf, 2, t);
            var caribou = CombatTests.Herd(w, Species.Caribou, 1, t);
            var e = Combat.Create(w, [new Fighter(pack[0]), new Fighter(pack[1])], [new Fighter(caribou)], t, 25, Awareness.Engaged, Awareness.Engaged);
            foreach (var u in e.Units.Where(u => u.Team == 0)) u.State = FighterState.Pressing;
            var chasers = e.Units.Where(u => u.Team == 0 && Combat.CatchOdds(w, u, e.Units[2]).Pct >= 0.25).ToList();
            Assert.NotEmpty(chasers);
            var events = new List<WorldEvent>();
            Combat.Apply(w, e, e.Units[2], CombatAction.Flee, events);
            if (e.Units[2].InField) continue; // caught
            Assert.All(chasers, c => Assert.Equal(FighterState.Gone, Combat.StateOf(w, c)));
            Assert.All(chasers, c => Assert.True(c.LeftChasing && c.FledTiles >= 1, $"{c.FledTiles} tiles"));
            Assert.True(e.Rounds >= 3, "a four-minute chase was charged no time"); // the wolves' 240 s limit
            Combat.ResolveHeadless(w, e, events);
            Assert.NotEqual(t, w.Agent(caribou).Tile); // it ran on the way it fled
            Assert.All(chasers, c => Assert.NotEqual(t, w.Agent(c.Agent).Tile)); // and the wolves that chased it after it
            return;
        }
        throw new InvalidOperationException("no seed had the caribou get away from a chase");
    }

    [Fact]
    public void Caught_ItCannotRunAgainThatRound()
    {
        var (w, e) = PlayerVs(Species.Wolf, 3);
        B(e).State = FighterState.Pressing;
        Combat.Apply(w, e, Me(e), CombatAction.Flee, []);
        Assert.True(Me(e).InField);
        Assert.Equal("You were run down.", Combat.Refusal(w, e, Me(e), CombatAction.Flee)?.Reason);
    }

    [Fact]
    public void APerson_RunsFromWhatItCanOutrun()
    {
        var (w, t) = CombatTests.Field();
        var mammoth = CombatTests.Herd(w, Species.Mammoth, 1, t);
        var npc = Npc.Spawn(w, t);
        var e = Combat.Create(w, [new Fighter(npc)], [new Fighter(mammoth)], t, 15, Awareness.Engaged, Awareness.Engaged);
        e.Units[1].State = FighterState.Pressing;
        e.Units[0].BoldnessModifier = -3;
        Combat.AiTurn(w, e, e.Units[0], []);
        Assert.Equal(FighterState.Gone, Combat.StateOf(w, e.Units[0])); // a mammoth cannot run a person down
    }

    [Fact]
    public void APerson_GivesGroundRatherThanRunFromWhatWouldCatchIt()
    {
        var (w, t) = CombatTests.Field();
        var wolf = CombatTests.Herd(w, Species.Wolf, 1, t);
        var npc = Npc.Spawn(w, t);
        var e = Combat.Create(w, [new Fighter(npc)], [new Fighter(wolf)], t, 2, Awareness.Engaged, Awareness.Engaged);
        e.Units[1].State = FighterState.Pressing;
        e.Units[0].BoldnessModifier = -3; // its resolve says run
        Combat.AiTurn(w, e, e.Units[0], []);
        Assert.Equal(FighterState.GivingGround, e.Units[0].State);
        Assert.True(e.Units[0].InField);
    }

    // ---- the player's chase ----

    [Fact]
    public void ThePlayer_IsOfferedTheChase_AndLetsItGoByDoingAnythingElse()
    {
        var (w, e) = PlayerVs(Species.Wolf, 2);
        var wolf = B(e);
        Me(e).State = FighterState.Pressing;
        Combat.BodyOf(w, wolf).SprintSpentLevel = 1;
        var leg = Combat.BodyOf(w, wolf).Regions.First(r => r.Name.Contains("Leg"));
        leg.Muscle.Condition = 0.1; leg.Bone.Condition = 0.3; // a lamed, blown wolf: a person might run it down
        Combat.Apply(w, e, wolf, CombatAction.Flee, []);
        var offer = Combat.ChaseOffer(w, e, Me(e));
        Assert.NotNull(offer);
        Assert.InRange(offer!.Value.Odds.Pct, 0.05, 1);
        Assert.Null(Combat.Refusal(w, e, Me(e), CombatAction.Chase));
        Sim.Step(w, new Fight(P, CombatAction.Wait), 1);
        Assert.Null(Combat.ChaseOffer(w, e, Me(e)));
        Assert.Equal(FighterState.Gone, Combat.StateOf(w, wolf));
    }

    [Fact]
    public void Chase_IsRefused_WhenNobodyRuns()
    {
        var (w, e) = PlayerVs(Species.Wolf, 5);
        Assert.Equal("Nobody is running from you.", Combat.Refusal(w, e, Me(e), CombatAction.Chase)?.Reason);
    }

    /// A lamed, blown wolf runs from the player: given chase, it is run down (the fight goes on) or it gets
    /// away, and then the player drove it off and its trail runs the way it went.
    [Fact]
    public void ThePlayersChase_EitherRunsItDown_OrItGetsAway_LeavingATrail()
    {
        (World, Encounter, CombatUnit) Setup(int seed, double legs)
        {
            var (w, t) = CombatTests.Field(World.New(seed));
            var herd = CombatTests.Herd(w, Species.Wolf, 1, t);
            var e = Combat.Create(w, [new Fighter(P)], [new Fighter(herd)], t, 2, Awareness.Engaged, Awareness.Engaged);
            var wolf = B(e);
            Me(e).State = FighterState.Pressing;
            var body = Combat.BodyOf(w, wolf);
            body.SprintSpentLevel = 1;
            foreach (var leg in body.Regions.Where(r => r.Name.Contains("Leg"))) { leg.Muscle.Condition = legs; leg.Bone.Condition = Math.Min(1, legs + 0.2); }
            Combat.Apply(w, e, wolf, CombatAction.Flee, []);
            return (w, e, wolf);
        }
        // How lame the wolf must be for the chase to be a real gamble.
        double legs = Enumerable.Range(1, 18).Select(i => i * 0.05).First(l =>
        {
            var (w, e, _) = Setup(1, l);
            return Combat.ChaseOffer(w, e, Me(e)) is var (_, odds) && odds.Pct is > 0.2 and < 0.8;
        });
        bool caught = false, lost = false;
        for (int seed = 1; seed <= 60 && !(caught && lost); seed++)
        {
            var (w, e, wolf) = Setup(seed, legs);
            if (Combat.ChaseOffer(w, e, Me(e)) == null) continue;
            var r = Sim.Step(w, new Fight(P, CombatAction.Chase), 1);
            if (w.Encounters.Contains(e)) { caught = true; Assert.True(wolf.InField); continue; }
            lost = true;
            Assert.Equal(CombatResult.AnimalFled, r.Events.OfType<EncounterEnded>().Single(f => f.Agent == P).Result);
            Assert.NotNull(r.Events.OfType<WoundedEscaped>().Single().Heading);
        }
        Assert.True(caught && lost, $"caught {caught}, lost {lost} at legs {legs}");
    }

    [Fact]
    public void APendingChase_SurvivesASave()
    {
        var (w, e) = PlayerVs(Species.Wolf, 2);
        Me(e).State = FighterState.Pressing;
        Combat.BodyOf(w, B(e)).SprintSpentLevel = 1;
        foreach (var leg in Combat.BodyOf(w, B(e)).Regions.Where(r => r.Name.Contains("Leg"))) { leg.Muscle.Condition = 0.1; leg.Bone.Condition = 0.3; }
        Combat.Apply(w, e, B(e), CombatAction.Flee, []);
        Assert.NotNull(e.PendingFlight);
        var loaded = TestWorlds.Load(Save.ToJson(w));
        Assert.Equal(Save.ToJson(w), Save.ToJson(loaded));
        var again = loaded.Encounters.Single();
        Assert.NotNull(Combat.ChaseOffer(loaded, again, Combat.UnitOf(again, P)));
    }

    // ---- breaking off ----

    [Fact]
    public void StandingOff30mFromEveryEnemy_BreaksOff()
    {
        var (w, e) = PlayerVs(Species.Wolf, 5);
        Gap(e, Me(e), B(e), 30);
        Me(e).State = FighterState.Standoff;
        B(e).State = FighterState.Standoff;
        B(e).BoldnessModifier = -1.2; // it holds off rather than closing in
        Sim.Step(w, new Fight(P, CombatAction.Wait), 1);
        Assert.False(Me(e).InField);
    }

    [Fact]
    public void AStalkerFarFromPreyThatHasNotSeenIt_DoesNotBreakOff()
    {
        var (w, t) = CombatTests.Field();
        var herd = CombatTests.Herd(w, Species.Caribou, 1, t);
        var e = Combat.Create(w, [new Fighter(P)], [new Fighter(herd)], t, 34, Awareness.Engaged, Awareness.Unaware);
        Sim.Step(w, new Fight(P, CombatAction.Wait), 1);
        Assert.True(Me(e).InField);
    }

    [Fact]
    public void TheGridsEdge_HoldsAFighter()
    {
        var (w, e) = PlayerVs(Species.Wolf, 10);
        Combat.Move(w, e, Me(e), new Cell(Me(e).Position.X, -5));
        Assert.True(Me(e).InField);
        Assert.Equal(0, Me(e).Position.Y);
    }
}
