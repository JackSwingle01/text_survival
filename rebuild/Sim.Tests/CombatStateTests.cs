namespace Survival.Tests;

/// The fighter state machine (combat grilling rounds 1, 2 and 6, 2026-11-21): one resolve with hysteresis
/// bands, alertness that rises and fades, actions as state within distance, the defence roll, targets,
/// and morale read off what the target does. Each threshold has its own test.
public class CombatStateTests
{
    static readonly AgentId P = AgentId.Player;
    static CombatUnit Me(Encounter e) => Combat.UnitOf(e, P);
    static CombatUnit B(Encounter e, int i = 0) => e.Units.Where(u => u.Team == 1).ElementAt(i);

    static (World W, Encounter E) PlayerVs(Species species, int distanceM, Awareness b = Awareness.Engaged, int count = 1, bool spear = false)
    {
        var (w, t) = CombatTests.Field();
        var herd = CombatTests.Herd(w, species, count, t);
        CombatTests.Normalize(w, herd);
        Gear? weapon = spear ? CombatTests.Spear(w) : null;
        var e = Combat.Create(w, [new Fighter(P) { Weapon = weapon }], [.. Enumerable.Range(0, count).Select(i => new Fighter(herd[i]))],
            t, distanceM, Awareness.Engaged, b);
        return (w, e);
    }

    // ---- the resolve bands, one threshold each ----

    [Fact]
    public void Standoff_PressesFromPlusPoint2() =>
        Assert.Equal((FighterState.Standoff, FighterState.Pressing), (Combat.NextState(FighterState.Standoff, 0.19), Combat.NextState(FighterState.Standoff, 0.2)));

    [Fact]
    public void Pressing_HoldsDownTo0_ThenStandsOff() =>
        Assert.Equal((FighterState.Pressing, FighterState.Standoff), (Combat.NextState(FighterState.Pressing, 0.0), Combat.NextState(FighterState.Pressing, -0.01)));

    [Fact]
    public void Standoff_GivesGroundBelowMinusPoint3() =>
        Assert.Equal((FighterState.Standoff, FighterState.GivingGround), (Combat.NextState(FighterState.Standoff, -0.3), Combat.NextState(FighterState.Standoff, -0.31)));

    [Fact]
    public void GivingGround_RecoversAboveMinusPoint1() =>
        Assert.Equal((FighterState.GivingGround, FighterState.Standoff), (Combat.NextState(FighterState.GivingGround, -0.1), Combat.NextState(FighterState.GivingGround, -0.09)));

    [Fact]
    public void AnyStand_RunsBelowMinusPoint8() =>
        Assert.Equal((FighterState.GivingGround, FighterState.Fleeing), (Combat.NextState(FighterState.GivingGround, -0.8), Combat.NextState(FighterState.Pressing, -0.81)));

    [Fact]
    public void Fleeing_RecoversAboveMinusPoint4_ToGivingGround() =>
        Assert.Equal((FighterState.Fleeing, FighterState.GivingGround), (Combat.NextState(FighterState.Fleeing, -0.4), Combat.NextState(FighterState.Fleeing, -0.39)));

    // ---- alertness, one threshold each ----

    static (World, Encounter, CombatUnit) Watcher(double alertness, FighterState state)
    {
        var (w, e) = PlayerVs(Species.Caribou, 30, Awareness.Unaware);
        var prey = B(e);
        prey.State = state;
        prey.AlertnessLevel = alertness;
        return (w, e, prey);
    }

    /// A notice adds 0.35 and a look that sees nothing takes 0.1 (Combat.NoticeAlertnessDelta, FadeAlertnessDelta):
    /// stepped in from just under each threshold, the band moves on the step that crosses it.
    [Fact]
    public void Unaware_BecomesWaryAt0Point3()
    {
        var (w, e, prey) = Watcher(0.29 - Combat.NoticeAlertnessDelta, FighterState.Unaware);
        Notice(w, e, prey);
        Assert.Equal(FighterState.Unaware, prey.State);
        prey.AlertnessLevel = 0.3 - Combat.NoticeAlertnessDelta;
        Notice(w, e, prey);
        Assert.Equal(FighterState.Wary, prey.State);
    }

    [Fact]
    public void Wary_CalmsBelow0Point1()
    {
        var (w, e, prey) = Watcher(0.2, FighterState.Wary);
        Overlook(w, e, prey);
        Assert.Equal(FighterState.Wary, prey.State); // 0.1: not yet below
        Overlook(w, e, prey);
        Assert.Equal(FighterState.Unaware, prey.State);
    }

    [Fact]
    public void Wary_EngagesAt0Point7_AndSkittishPreyRuns()
    {
        var (w, e, prey) = Watcher(0.69 - Combat.NoticeAlertnessDelta, FighterState.Wary);
        Notice(w, e, prey);
        Assert.Equal(FighterState.Wary, prey.State);
        prey.AlertnessLevel = 0.7 - Combat.NoticeAlertnessDelta;
        Notice(w, e, prey);
        Assert.Equal(FighterState.Fleeing, prey.State); // a caribou: skittish prey always flees once engaged (round 1)
    }

    // The bands move only through detection; drive it with a certain notice, or none, by distance.
    static void Notice(World w, Encounter e, CombatUnit prey)
    {
        Me(e).Position = new Cell(prey.Position.X, prey.Position.Y - 1); // a metre off: 95% to notice
        for (int i = 0; i < 50; i++)
        {
            double before = prey.AlertnessLevel;
            var state = prey.State;
            Combat.RunDetection(w, e, Me(e), 0, 1000); // so much movement that it cannot be missed
            if (prey.AlertnessLevel > before || prey.State != state) return;
        }
        throw new InvalidOperationException("never noticed");
    }

    static void Overlook(World w, Encounter e, CombatUnit prey) => Combat.RunDetection(w, e, Me(e), 0, 1e-9);

    [Fact]
    public void Freezing_IsNotASureThing()
    {
        var (w, e) = PlayerVs(Species.Caribou, 6, Awareness.Unaware);
        int noticed = 0;
        for (int i = 0; i < 400; i++)
        {
            B(e).State = FighterState.Unaware;
            B(e).AlertnessLevel = 0.3 - Combat.NoticeAlertnessDelta;
            Combat.RunDetection(w, e, Me(e), 0, Combat.FrozenExposureMeters);
            if (B(e).State == FighterState.Wary) noticed++;
        }
        Assert.InRange(noticed, 1, 200);
    }

    // ---- states and actions ----

    [Fact]
    public void TheAI_IsHeldToItsState_AndThePlayerIsNot()
    {
        var (w, e) = PlayerVs(Species.Wolf, 2);
        var wolf = B(e);
        wolf.State = FighterState.Standoff;
        Assert.NotNull(Combat.Refusal(w, e, wolf, CombatAction.Advance)); // standing off is not advancing
        wolf.State = FighterState.Pressing;
        Assert.NotNull(Combat.Refusal(w, e, wolf, CombatAction.Retreat));
        wolf.State = FighterState.GivingGround;
        Assert.NotNull(Combat.Refusal(w, e, wolf, CombatAction.Shove));
        Me(e).State = FighterState.Standoff;
        Assert.Null(Combat.Refusal(w, e, Me(e), CombatAction.Advance)); // the player's state follows from the action
    }

    [Theory]
    [InlineData(CombatAction.Advance, FighterState.Pressing, 3)]
    [InlineData(CombatAction.Attack, FighterState.Pressing, 1)] // bare hands reach a neighbouring cell
    [InlineData(CombatAction.Wait, FighterState.Standoff, 3)]
    [InlineData(CombatAction.Intimidate, FighterState.Standoff, 3)]
    [InlineData(CombatAction.Retreat, FighterState.GivingGround, 3)]
    public void ThePlayersStateFollowsFromTheirAction(CombatAction action, FighterState state, int distanceM)
    {
        var (w, e) = PlayerVs(Species.Wolf, distanceM);
        B(e).BoldnessModifier = -0.9; // it gives ground, short of running (a runner would be gone at once)
        var r = Sim.Step(w, new Fight(P, action), 1);
        Assert.True(r.InterruptedReason is null or "done", r.InterruptedReason);
        Assert.Equal(state, Me(e).State);
    }

    [Fact]
    public void Fleeing_WorksAtAnyDistance()
    {
        var (w, e) = PlayerVs(Species.Wolf, 1);
        B(e).BoldnessModifier = -2; // too daunted to give chase, even with the chase instinct
        Assert.Null(Combat.Refusal(w, e, Me(e), CombatAction.Flee));
        Combat.Apply(w, e, Me(e), CombatAction.Flee, []);
        Assert.Equal(FighterState.Gone, Combat.StateOf(w, Me(e)));
    }

    /// Down is read off the faculties: with neither speed nor dexterity it can neither move nor strike.
    [Fact]
    public void AnUnconsciousFighter_IsDown_AndCanOnlyLieThere()
    {
        var (w, e) = PlayerVs(Species.Wolf, 5);
        CrippledVerbsTests.KnockedOut(Combat.BodyOf(w, Me(e)));
        Assert.Equal(FighterState.Down, Combat.StateOf(w, Me(e)));
        Assert.Null(Combat.Refusal(w, e, Me(e), CombatAction.Wait));
        Assert.NotNull(Combat.Refusal(w, e, Me(e), CombatAction.Attack));
    }

    /// Legs gone and arms whole: still in the fight, but it cannot run, give chase or step, and says why.
    [Fact]
    public void ALeglessFighter_WithItsArms_IsNotDown_AndIsRefusedRunningAndStepping_NamingTheLegs()
    {
        var (w, e) = PlayerVs(Species.Wolf, 5);
        CrippledVerbsTests.Legless(Combat.BodyOf(w, Me(e)));
        Assert.NotEqual(FighterState.Down, Combat.StateOf(w, Me(e)));
        Assert.Null(Combat.RunningOf(w, Me(e)));
        Assert.Equal(0, Combat.Allowance(w, Me(e)));
        foreach (var action in new[] { CombatAction.Flee, CombatAction.Advance, CombatAction.Retreat, CombatAction.Move })
        {
            var refused = Combat.Refusal(w, e, Me(e), action);
            Assert.True(refused?.Missing is MissingFaculty { Faculty: Faculty.Speed }, $"{action}: {refused}");
            Assert.Contains("leg", refused!.Reason);
        }
        Assert.Equal(1, Combat.CatchOdds(w, B(e), Me(e)).Pct);
        Assert.Equal(0, Combat.CatchOdds(w, Me(e), B(e)).Pct);
    }

    /// A wolf has no hands: with its legs gone it has neither speed nor dexterity, and is out of the fight.
    [Fact]
    public void ALeglessWolf_IsDown()
    {
        var (w, e) = PlayerVs(Species.Wolf, 5);
        foreach (var leg in Combat.BodyOf(w, B(e)).Regions.Where(r => r.Name.Contains("Leg")))
            foreach (var layer in leg.Layers) layer.Condition = 0;
        Assert.Equal(FighterState.Down, Combat.StateOf(w, B(e)));
    }

    // ---- the AI's priority lists ----

    [Fact]
    public void APerson_ThrowsOnlyAtFiftyPercentOrBetter()
    {
        var (w, t) = CombatTests.Field();
        var wolf = CombatTests.Herd(w, Species.Wolf, 1, t);
        var npc = Npc.Spawn(w, t);
        var spear = Items.Make("wooden_spear");
        Items.Pack(w, npc).Gear.Add(spear);
        var e = Combat.Create(w, [new Fighter(npc) { Weapon = spear }], [new Fighter(wolf)], t, 12, Awareness.Engaged, Awareness.Engaged);
        var me = e.Units[0];
        me.State = FighterState.Pressing;
        var target = Combat.TargetOf(e, me)!;
        for (int d = 4; d <= 19; d++)
        {
            target.Position = new Cell(me.Position.X, me.Position.Y + d);
            bool throws = Combat.ChooseAction(w, e, me).Action == CombatAction.Throw;
            Assert.Equal(throws, Combat.ThrowChance(w, me, target, spear) >= Combat.PersonThrowMinPct && d > 3);
        }
    }

    [Fact]
    public void AThrownWeapon_CanBePickedUpAgain()
    {
        var (w, e) = PlayerVs(Species.Bison, 10, spear: true);
        B(e).BoldnessModifier = -100;
        var spear = Me(e).Weapon!;
        Combat.Apply(w, e, Me(e), CombatAction.Throw, []);
        Assert.NotNull(Combat.Refusal(w, e, Me(e), CombatAction.PickUp)); // it lies over there
        Me(e).Position = e.Dropped![0].At;
        Assert.Null(Combat.Apply(w, e, Me(e), CombatAction.PickUp, []).Refused);
        Assert.Same(spear, Me(e).Weapon);
        Assert.Empty(e.Dropped!);
    }

    /// The hold: standing off, an animal comes on as far as its resolve takes it, never inside the longer
    /// reach and half a metre; a spear's reach keeps it a metre further out than bare hands do.
    [Theory]
    [InlineData(false, 2.0)]
    [InlineData(true, 3.0)]
    public void InStandoff_AnAnimalComesNoNearerThanTheLongerReachAndHalfAMetre(bool spear, double holdM)
    {
        var (w, e) = PlayerVs(Species.Wolf, 12, spear: spear);
        var wolf = B(e);
        wolf.State = FighterState.Standoff;
        wolf.BoldnessModifier = 10; // a resolve that would carry it all the way in
        Assert.Equal(holdM, Combat.HoldLineM(w, wolf, Me(e)), 9);
        bool closed = false;
        for (int i = 0; i < 50; i++)
        {
            var (action, to, _) = Combat.ChooseAction(w, e, wolf);
            if (action != CombatAction.Move) continue;
            double gap = wolf.Position.DistanceTo(Me(e).Position);
            Assert.True(to!.Value.DistanceTo(Me(e).Position) >= Math.Min(gap, holdM), $"{action} to {to}, {gap:0.0} m out");
            closed |= to.Value.DistanceTo(Me(e).Position) < gap;
            wolf.Position = to.Value;
        }
        Assert.True(closed, "it never came on");
        Assert.InRange(wolf.Position.DistanceTo(Me(e).Position), holdM, holdM + 1.5);
    }

    /// In the deadband it stops or circles; it never steps nearer.
    [Fact]
    public void InStandoff_AnAnimalAtAnEvenResolveHoldsOrStepsAside()
    {
        var (w, e) = PlayerVs(Species.Wolf, 8);
        var wolf = B(e);
        wolf.State = FighterState.Standoff;
        wolf.BoldnessModifier = -Combat.Resolve(w, e, wolf, Me(e)) + wolf.BoldnessModifier;
        double gap = wolf.Position.DistanceTo(Me(e).Position);
        for (int i = 0; i < 50; i++)
        {
            var (action, to, _) = Combat.ChooseAction(w, e, wolf);
            Assert.True(action == CombatAction.Wait || action == CombatAction.Move && to is Cell c && c.DistanceTo(Me(e).Position) >= gap - 0.5,
                $"{action} to {to}");
        }
    }

    // ---- targets ----

    [Fact]
    public void APredator_GoesForTheMostVulnerable()
    {
        var (w, t) = CombatTests.Field();
        var wolf = CombatTests.Herd(w, Species.Wolf, 1, t);
        var hurt = CombatTests.Herd(w, Species.Human, 1, t);
        w.Agents[hurt] = new Agent { Id = hurt, Tile = t, Members = [BodyState.BaselineHuman()], Mind = new PlayerMind() };
        w.Agent(hurt).Body.BloodPct = 0.6;
        var e = Combat.Create(w, [new Fighter(P), new Fighter(hurt)], [new Fighter(wolf)], t, 10, Awareness.Engaged, Awareness.Engaged);
        Assert.Equal(hurt, Combat.BestTarget(w, e, B(e))!.Agent);
    }

    [Fact]
    public void TurningToAnotherTarget_CostsTheRound()
    {
        var (w, e) = PlayerVs(Species.Wolf, 10, count: 2);
        var other = Combat.Targets(w, e, Me(e)).Single(t => !t.Facing);
        var before = Me(e).Position;
        Sim.Step(w, new Fight(P, CombatAction.Advance, other.Index), 1);
        Assert.Equal(before, Me(e).Position); // it turned; it did not advance
        Assert.Equal(other.Index, Me(e).Target);
    }

    [Fact]
    public void Targets_ListEveryEnemy_NearestFirst_WithTheOneFaced()
    {
        var (w, e) = PlayerVs(Species.Wolf, 10, count: 3);
        var targets = Combat.Targets(w, e, Me(e));
        Assert.Equal(3, targets.Count);
        Assert.Single(targets, t => t.Facing);
        Assert.True(targets.Zip(targets.Skip(1)).All(p => p.First.DistanceM <= p.Second.DistanceM));
        Assert.All(targets.Where(t => !t.Facing), t => Assert.Null(t.TurnRefusal));
    }

    // ---- morale ----

    [Fact]
    public void AWound_CostsMorale_ByVitalityAndLostBlood()
    {
        var body = BodyState.BaselineHuman();
        Assert.Equal(0, Combat.HealthPenalty(body, Species.Human), 9);
        body.BloodPct = 0.8;
        // 0.5 x the blood lost, and what the circulation it costs (0.6) takes from the faculties: about 0.4 of vitality.
        Assert.Equal(0.5 * 0.2 + (1 - Body.Vitality(body, Species.Human)), Combat.HealthPenalty(body, Species.Human), 9);
        Assert.True(Combat.HealthPenalty(body, Species.Human) > 0.4);
    }

    [Fact]
    public void TurningToRun_EmboldensThePredatorAfterIt()
    {
        var (w, e) = PlayerVs(Species.Wolf, 10);
        double before = B(e).BoldnessModifier;
        Combat.Apply(w, e, Me(e), CombatAction.Flee, []);
        Assert.Equal(before + 0.3, B(e).BoldnessModifier, 9);
    }

    [Fact]
    public void AStandoff_WearsAFighterDown()
    {
        var (w, e) = PlayerVs(Species.Wolf, 12);
        var wolf = B(e);
        wolf.BoldnessModifier = -1.25; // standing off: resolve a little under 0
        Me(e).State = FighterState.Standoff;
        Combat.AiTurn(w, e, wolf, []);
        double after = wolf.BoldnessModifier;
        Assert.Equal(FighterState.Standoff, wolf.State);
        Combat.AiTurn(w, e, wolf, []);
        Assert.Equal(after - 0.02, wolf.BoldnessModifier, 9);
    }

    [Fact]
    public void ATargetGivingGround_Emboldens_AndOnePressingIn_Daunts()
    {
        var (w, e) = PlayerVs(Species.Wolf, 30); // far enough that two rounds' closing in come nowhere near a bite
        var wolf = B(e);
        wolf.State = FighterState.Pressing;
        Me(e).State = FighterState.GivingGround;
        double m0 = wolf.BoldnessModifier;
        Combat.AiTurn(w, e, wolf, []);
        Assert.Equal(m0 + 0.05, wolf.BoldnessModifier, 9);
        Me(e).State = FighterState.Pressing;
        wolf.State = FighterState.Pressing;
        double m1 = wolf.BoldnessModifier;
        Combat.AiTurn(w, e, wolf, []);
        Assert.Equal(m1 - 0.05 * Combat.Threat(w, Me(e)), wolf.BoldnessModifier, 9);
    }

    [Fact]
    public void OtherEnemiesPressingWithin5m_WeighOnTheResolve()
    {
        var (w, e) = PlayerVs(Species.Wolf, 3, count: 2);
        var (a, b) = (B(e, 0), B(e, 1));
        var faced = Combat.TargetOf(e, Me(e))!;
        var other = faced == a ? b : a;
        other.Target = e.Units.IndexOf(Me(e));
        other.State = FighterState.Standoff;
        double alone = Combat.Resolve(w, e, Me(e), faced);
        other.State = FighterState.Pressing;
        Assert.Equal(alone - 0.5 * Combat.Threat(w, other), Combat.Resolve(w, e, Me(e), faced), 9);
    }

    // ---- the defence roll ----

    [Fact]
    public void TheDefence_IsHigherGivingGround_LowerPressing_NoneUnaware()
    {
        var (w, e) = PlayerVs(Species.Wolf, 1);
        var me = Me(e);
        var wolf = Combat.TargetOf(e, me)!;
        double Chance(FighterState s) { me.State = s; return Combat.DefenceChance(w, e, me, wolf); }
        Assert.Equal(0.6, Chance(FighterState.Standoff), 9);
        Assert.True(Chance(FighterState.GivingGround) > 0.6);
        Assert.True(Chance(FighterState.Pressing) < 0.6);
        Assert.Equal(0, Chance(FighterState.Unaware));
        Assert.Equal(0, Chance(FighterState.Fleeing));
    }

    [Fact]
    public void TheDefence_GuardsOnlyAgainstTheTarget()
    {
        var (w, e) = PlayerVs(Species.Wolf, 2, count: 2);
        var me = Me(e);
        var faced = Combat.TargetOf(e, me)!;
        var flank = B(e, 0) == faced ? B(e, 1) : B(e, 0);
        Assert.True(Combat.DefenceChance(w, e, me, faced) > 0);
        Assert.Equal(0, Combat.DefenceChance(w, e, me, flank)); // a blow from the side lands unguarded (round 6)
    }

    [Fact]
    public void TheDefence_ReadsRunningAndTheReserve()
    {
        var (w, e) = PlayerVs(Species.Wolf, 1);
        var me = Me(e);
        var wolf = Combat.TargetOf(e, me)!;
        double fresh = Combat.DefenceChance(w, e, me, wolf);
        TestKit.Give(Items.Pack(w, P), Resource.RawMeat, 40);
        Assert.True(Combat.DefenceChance(w, e, me, wolf) < fresh, "a load did not slow the defence");
        Combat.BodyOf(w, me).SprintSpentLevel = 1;
        Assert.Equal(0, Combat.DefenceChance(w, e, me, wolf)); // no breath for it
    }

    /// A blow the defence turns aside is turned aside whatever the defender holds: a weapon that let the
    /// blow through, or held the defender at Close where it cannot shout, would make standing armed worse.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ATurnedBlow_DoesNoHarm_AndDrivesTheDefenderBack_ArmedOrNot(bool spear)
    {
        var (w, t) = CombatTests.Field();
        var weapon = spear ? Items.Make("wooden_spear", durability: 100_000) : null;
        int turned = 0;
        for (int i = 0; i < 200; i++)
        {
            w.Encounters.Clear();
            w.Agent(P).Members[0] = BodyState.BaselineHuman();
            if (weapon != null) Items.Pack(w, P).Gear.Add(weapon);
            var herd = CombatTests.Herd(w, Species.Bear, 1, t);
            var e = Combat.Create(w, [new Fighter(P) { Weapon = weapon }], [new Fighter(herd)], t, 1, Awareness.Engaged, Awareness.Engaged);
            var me = Me(e);
            var bear = B(e);
            double before = me.Position.DistanceTo(bear.Position);
            var r = Combat.Apply(w, e, bear, CombatAction.Attack, []);
            w.Agents.Remove(herd);
            if (!r.Dodged && !r.Blocked) continue;
            turned++;
            Assert.Equal(spear, r.Blocked);
            Assert.Null(r.Damage);
            Assert.Equal(1.0, Combat.BodyOf(w, me).Regions.Min(g => g.Condition));
            Assert.True(me.Position.DistanceTo(bear.Position) > before, $"held at {before} m");
        }
        Assert.InRange(turned, 60, 160); // about 0.9 x 0.6 of 200
    }

    // ---- an NPC's fight or flight ----

    [Fact]
    public void AnNpc_PressesAWolf_StandsUpToABear_AndRunsFromACaveBear_ByItsResolve()
    {
        var (w, t) = CombatTests.Field();
        var npc = Npc.Spawn(w, t);
        var me = new Fighter(npc);
        Assert.Equal(FighterState.Pressing, Combat.WouldStand(w, me, w.Agent(CombatTests.Herd(w, Species.Wolf, 1, t)), 8));
        // A bear at 8 m: boldness 1.0 against 2.5 x 0.44 of threat, a resolve of -0.1: it stands its ground.
        Assert.Equal(FighterState.Standoff, Combat.WouldStand(w, me, w.Agent(CombatTests.Herd(w, Species.Bear, 1, t)), 8));
        Assert.Equal(FighterState.GivingGround, Combat.WouldStand(w, me, w.Agent(CombatTests.Herd(w, Species.CaveBear, 1, t)), 8));
        // The same every time: no dice (round 2).
        Assert.Equal(Combat.WouldStand(w, me with { BoldnessDelta = 0.2 }, w.Agent(CombatTests.Herd(w, Species.Bear, 1, t)), 8),
            Combat.WouldStand(w, me with { BoldnessDelta = 0.2 }, w.Agent(CombatTests.Herd(w, Species.Bear, 1, t)), 8));
    }

    // ---- the player's hunt, as an activity ----

    [Fact]
    public void ThePlayersHunt_IsAnActivity_WithRefusals()
    {
        var (w, t) = CombatTests.Field();
        Assert.Equal("No such herd here.", Sim.Refusal(w, new Hunt(P, new AgentId(999_999)))?.Reason);
        var herd = CombatTests.Herd(w, Species.Caribou, 4, t);
        Items.Pack(w, P).Gear.Add(Items.Make("wooden_spear"));
        Assert.Null(Sim.Refusal(w, new Hunt(P, herd)));
        var r = Sim.Step(w, new Hunt(P, herd), 1);
        var enc = Combat.EncounterOf(w, P);
        Assert.NotNull(enc);
        Assert.Equal("wooden_spear", Combat.UnitOf(enc!, P).Weapon!.Kind);
        Assert.Equal(Sim.InAnEncounter, Sim.Refusal(w, new Hunt(P, herd))?.Reason);
        Assert.Equal(1, r.MinutesElapsed);
    }

    /// An animal that falls in the player's action was on its way somewhere: with nobody of its herd
    /// left alive it walks no further, and the round ends with its carcass on the tile.
    [Fact]
    public void AnAnimalKilledMidWalk_InThePlayersRound_WalksNoFurther()
    {
        var (w, t) = CombatTests.Field();
        var caribou = CombatTests.Herd(w, Species.Caribou, 1, t);
        var m = Map.Of(w);
        var dir = new[] { Direction.North, Direction.East, Direction.South, Direction.West }
            .First(d => m.InBounds(Map.Step(t, d)) && Map.Blocked(m, t, Map.Step(t, d)) == null);
        Assert.Null(Sim.Start(w, new Travel(caribou, dir), Verbs.UntilDoneMinutes, [], out var walking));
        w.Agent(caribou).Doing = walking;
        Combat.Create(w, [new Fighter(P)], [new Fighter(caribou)], t, 10, Awareness.Engaged, Awareness.Unaware);
        w.Agent(caribou).Body.BloodPct = 0;
        var r = Sim.Step(w, new Fight(P, CombatAction.Wait), 1);
        Assert.Contains(r.Events, e => e is Killed k && k.Agent == caribou);
        Assert.False(w.Agents.ContainsKey(caribou));
        Assert.Null(Combat.EncounterOf(w, P));
        Assert.NotEmpty(w.Tiles[t].Carcasses!);
    }

    [Fact]
    public void TheChaseInstinct_FiresOnTheFirstTurnToRun_NotOnEveryWavering()
    {
        var (w, e) = PlayerVs(Species.Wolf, 10);
        double before = B(e).BoldnessModifier;
        Combat.Enter(w, e, Me(e), FighterState.Fleeing);
        Combat.Enter(w, e, Me(e), FighterState.GivingGround);
        Combat.Enter(w, e, Me(e), FighterState.Fleeing);
        Assert.Equal(before + 0.3, B(e).BoldnessModifier, 9);
    }

    [Fact]
    public void AnAmbushedPlayer_WhoActs_IsInTheFight_AndOneWhoKeepsStill_IsNot()
    {
        var (w, t) = CombatTests.Field();
        var wolf = CombatTests.Herd(w, Species.Wolf, 1, t);
        var e = Combat.Create(w, [new Fighter(P)], [new Fighter(wolf)], t, 20, Awareness.Unaware, Awareness.Engaged);
        B(e).BoldnessModifier = -1.2; // it stands off at 20 m, short of running
        Sim.Step(w, new Fight(P, CombatAction.Wait), 1);
        Assert.Equal(FighterState.Unaware, Me(e).State);
        Sim.Step(w, new Fight(P, CombatAction.Retreat), 1);
        Assert.Equal(FighterState.GivingGround, Me(e).State);
    }

    [Fact]
    public void AWaryAnimal_SometimesComesToLook()
    {
        var (w, e) = PlayerVs(Species.Bison, 20, Awareness.Alert);
        int looks = 0;
        for (int i = 0; i < 200; i++)
        {
            B(e).State = FighterState.Wary;
            if (Combat.ChooseAction(w, e, B(e)) is { Action: CombatAction.Move, To: Cell c } && c.DistanceTo(Me(e).Position) < B(e).Position.DistanceTo(Me(e).Position))
                looks++;
        }
        Assert.InRange(looks, 5, 80);
    }

    /// Giving ground with the enemy in reach, a fighter strikes while its resolve is at or above its kind's
    /// strikeBackResolve (decided 2026-11-21, tunable), and backs off below it.
    [Fact]
    public void GivingGround_StrikesInReach_AboveItsStrikeBackResolve_AndBacksOffBelow()
    {
        var (w, e) = PlayerVs(Species.Wolf, 1);
        var wolf = B(e);
        wolf.State = FighterState.GivingGround;
        var me = Combat.TargetOf(e, wolf)!;
        double line = SpeciesTable.Of(Species.Wolf).StrikeBackResolve;
        wolf.BoldnessModifier = line - Combat.Resolve(w, e, wolf, me) + 0.01;
        Assert.Equal(CombatAction.Attack, Combat.ChooseAction(w, e, wolf).Action);
        wolf.BoldnessModifier -= 0.02;
        Assert.Equal(CombatAction.Move, Combat.ChooseAction(w, e, wolf).Action);
    }

    /// A fighter asleep where it dropped from exhaustion does nothing but lie there until a blow wakes it.
    [Fact]
    public void AnAsleepFighter_Waits()
    {
        var (w, e) = PlayerVs(Species.Wolf, 1);
        var wolf = B(e);
        wolf.State = FighterState.GivingGround;
        var me = Combat.TargetOf(e, wolf)!;
        wolf.BoldnessModifier = SpeciesTable.Of(Species.Wolf).StrikeBackResolve - Combat.Resolve(w, e, wolf, me) + 0.01;
        Assert.Equal(CombatAction.Attack, Combat.ChooseAction(w, e, wolf).Action);
        Effects.Set(Combat.BodyOf(w, wolf), EffectKind.Asleep, 40 / 90.0);
        Assert.Equal(CombatAction.Wait, Combat.ChooseAction(w, e, wolf).Action);
    }
}
