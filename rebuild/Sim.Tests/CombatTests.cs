namespace Survival.Tests;

/// Ported from text_survival.Tests/Combat (HeadlessCombatTests, PreyBehaviorTests, EncounterTacticsTests)
/// and the mechanical part of Companions/CompanionCombatTests, plus the agent-model rules the rebuild adds.
public class CombatTests
{
    internal static (World World, TileId Tile) Field(int seed = 1)
    {
        var world = World.New(seed);
        var map = Map.Of(world);
        // A clearing: visibility 1, the old tests' default Location, so cover changes nothing.
        var tile = Enumerable.Range(0, map.Width).SelectMany(x => Enumerable.Range(0, map.Height).Select(y => new TileId(x, y)))
            .First(t => map.TerrainAt(t) == Terrain.Clearing && map.CaveAt(t) == Cave.None && t != map.Camp);
        world.Agent(AgentId.Player).Tile = tile;
        world.Agent(AgentId.Player).Members[0] = BodyState.BaselineHuman(); // the reference man, not this seed's autumn draw
        return (world, tile);
    }

    internal static AgentId Herd(World world, Species species, int count, TileId tile) => Agents.Spawn(world, species, tile, count);

    /// Average-sized members, as the old goldens normalised SizeModifier away.
    internal static void Normalize(World world, AgentId id)
    {
        var agent = world.Agent(id);
        var row = SpeciesTable.Of(agent.Species);
        for (int i = 0; i < agent.Members.Count; i++)
            agent.Members[i] = row.IsAnimal ? BodyState.Create(row.Body, row.WeightKg, row.FatPct, row.MusclePct) : BodyState.BaselineHuman();
    }

    /// A spear in the player's pack, to wield: a fighter's weapon comes out of its own pack.
    internal static Gear Spear(World world)
    {
        var spear = Items.Make("wooden_spear");
        Items.Pack(world, AgentId.Player).Gear.Add(spear);
        return spear;
    }
    static Fighter Player(Gear? weapon = null) => new(AgentId.Player) { Weapon = weapon };
    static CombatUnit B(Encounter e, int i = 0) => e.Units.Where(u => u.Team == 1).ElementAt(i);
    static CombatUnit Me(Encounter e) => Combat.UnitOf(e, AgentId.Player);

    static void Cripple(BodyState body)
    {
        foreach (var leg in body.Regions.Where(r => r.Name.Contains("Leg")))
        {
            leg.Muscle.Condition = 0;
            leg.Bone.Condition = 0.2;
        }
    }

    static Encounter PlayerVs(World world, TileId tile, Species species, int distance, Awareness b = Awareness.Engaged, int count = 1, Gear? weapon = null)
    {
        var herd = Herd(world, species, count, tile);
        return Combat.Create(world, [Player(weapon)], [.. Enumerable.Range(0, count).Select(i => new Fighter(herd, i))], tile,
            distance, Awareness.Engaged, b);
    }

    // ---- setup (old HeadlessCombatTests) ----

    [Fact]
    public void Create_PutsTeamsAtRequestedDistance()
    {
        var (w, t) = Field();
        var wolf = Herd(w, Species.Wolf, 1, t);
        var caribou = Herd(w, Species.Caribou, 1, t);
        var e = Combat.Create(w, [new(wolf)], [new(caribou)], t, 12, Awareness.Engaged, Awareness.Unaware);
        Assert.Equal(12, e.Units[0].Position.DistanceTo(e.Units[1].Position), 2);
        Assert.Equal(Awareness.Engaged, e.Units[0].Awareness);
        Assert.Equal(Awareness.Unaware, e.Units[1].Awareness);
        Assert.Contains(e, w.Encounters);
    }

    [Fact]
    public void Create_RefusesBadRosters()
    {
        var (w, t) = Field();
        var wolf = Herd(w, Species.Wolf, 2, t);
        var elsewhere = Herd(w, Species.Wolf, 1, new TileId(t.X + 1, t.Y));
        Assert.Throws<ArgumentException>(() => Combat.Create(w, [new(wolf)], [Player()], t, 5, Awareness.Engaged, Awareness.Engaged));
        Assert.Throws<ArgumentException>(() => Combat.Create(w, [Player()], [new(wolf, 0), new(wolf, 0)], t, 5, Awareness.Engaged, Awareness.Engaged));
        Assert.Throws<ArgumentException>(() => Combat.Create(w, [Player()], [new(elsewhere)], t, 5, Awareness.Engaged, Awareness.Engaged));
        Assert.Throws<ArgumentOutOfRangeException>(() => Combat.Create(w, [Player()], [new(wolf, 2)], t, 5, Awareness.Engaged, Awareness.Engaged));
        Combat.Create(w, [Player()], [new(wolf, 0)], t, 5, Awareness.Engaged, Awareness.Engaged);
        Assert.Throws<InvalidOperationException>(() => Combat.Create(w, [new(wolf, 1)], [new(wolf, 0)], t, 5, Awareness.Engaged, Awareness.Engaged));
    }

    /// Units name members by index, and one fight's aftermath reorders its agents' members: a herd
    /// with members in two fights had the second fight's units pointing at the wrong bodies.
    [Fact]
    public void AnAgentFightsOneFightAtATime()
    {
        var (w, t) = Field();
        var wolves = Herd(w, Species.Wolf, 4, t);
        var caribou = Herd(w, Species.Caribou, 1, t);
        var hunt = Combat.Create(w, [new(caribou)], [new(wolves, 0)], t, 10, Awareness.Engaged, Awareness.Engaged);
        Assert.Throws<InvalidOperationException>(() => Combat.Create(w, [Player()], [new(wolves, 3)], t, 10, Awareness.Engaged, Awareness.Engaged));
        var other = Combat.Create(w, [Player()], [new(Herd(w, Species.Wolf, 1, t))], t, 10, Awareness.Engaged, Awareness.Engaged);
        Assert.Throws<InvalidOperationException>(() => Combat.Join(w, other, new Fighter(wolves, 2), 1));
        Combat.Join(w, hunt, new Fighter(wolves, 2), 1); // its own fight it may join
        Assert.Equal(2, w.Encounters.Count);
    }

    /// A weapon is wielded out of the fighter's own pack, and goes back to it: a weapon from
    /// nowhere would be minted into the pack when the fight ended.
    [Fact]
    public void AWeaponComesOutOfTheWieldersPack()
    {
        var (w, t) = Field();
        var wolf = Herd(w, Species.Wolf, 1, t);
        Assert.Throws<ArgumentException>(() => Combat.Create(w, [Player(Items.Make("wooden_spear"))], [new(wolf)], t, 5, Awareness.Engaged, Awareness.Engaged));
        Assert.Throws<ArgumentException>(() => Combat.Create(w, [Player()], [new(wolf) { Weapon = Items.Make("wooden_spear") }], t, 5, Awareness.Engaged, Awareness.Engaged));
        var spear = Spear(w);
        Assert.Throws<ArgumentException>(() => Combat.Create(w, [Player(spear), new(Herd(w, Species.Human, 1, t)) { Weapon = spear }], [new(wolf)], t, 5, Awareness.Engaged, Awareness.Engaged));
        Assert.Empty(w.Encounters);
        var e = Combat.Create(w, [Player()], [new(wolf)], t, 5, Awareness.Engaged, Awareness.Engaged);
        var friend = Herd(w, Species.Human, 1, t);
        Assert.Throws<ArgumentException>(() => Combat.Join(w, e, new Fighter(friend) { Weapon = Items.Make("wooden_spear") }, 0));
        var striker = Items.Make("fire_striker");
        Items.Pack(w, AgentId.Player).Gear.Add(striker);
        Assert.Throws<ArgumentException>(() => Combat.Join(w, e, new Fighter(friend) { Weapon = striker }, 0)); // not a weapon
        Assert.Single(e.Units, u => u.Team == 0);
    }

    /// The unwatched bleed too: a round of a headless fight is a minute of wounds, as a round of the player's is.
    [Fact]
    public void AWoundedAnimalBleedsOutInAHeadlessFight()
    {
        var (w, t) = Field();
        var wolf = Herd(w, Species.Wolf, 1, t);
        var caribou = Herd(w, Species.Caribou, 1, t);
        var e = Combat.Create(w, [new(wolf)], [new(caribou)], t, 30, Awareness.Engaged, Awareness.Unaware);
        var body = Combat.BodyOf(w, B(e));
        body.BloodPct = 0.5005;
        Effects.Add(body, EffectKind.Bleeding, 1.0);
        var events = new List<WorldEvent>();
        Assert.Equal(CombatResult.Victory, Combat.ResolveHeadless(w, e, events));
        Assert.Null(events.OfType<Killed>().Single().By);
        Assert.Equal(1, e.Rounds);
    }

    [Fact]
    public void ResolveHeadless_RefusesThePlayer()
    {
        var (w, t) = Field();
        var e = PlayerVs(w, t, Species.Wolf, 5);
        Assert.Throws<InvalidOperationException>(() => Combat.ResolveHeadless(w, e, []));
    }

    [Fact]
    public void PackHunt_LeavesConsistentWorldWhateverHappened()
    {
        for (int seed = 1; seed <= 20; seed++)
        {
            var (w, t) = Field(seed);
            var wolves = Herd(w, Species.Wolf, 4, t);
            var caribou = Herd(w, Species.Caribou, 2, t);
            var events = new List<WorldEvent>();
            var e = Combat.Create(w, [.. Enumerable.Range(0, 4).Select(i => new Fighter(wolves, i))], [new(caribou, 0), new(caribou, 1)], t,
                5, Awareness.Engaged, Awareness.Unaware);
            var result = Combat.ResolveHeadless(w, e, events);

            int killed = events.OfType<Killed>().Count();
            Assert.Equal(killed, w.Tiles.TryGetValue(t, out var tile) ? tile.Carcasses?.Count ?? 0 : 0);
            Assert.All(w.Agents.Values, a => Assert.All(a.Members.Where(_ => SpeciesTable.Of(a.Species).IsAnimal), m => Assert.True(Body.IsAlive(m))));
            Assert.All(w.Agents.Values, a => Assert.NotEmpty(a.Members));
            Assert.Equal(6 - killed, (w.Agents.GetValueOrDefault(wolves)?.Members.Count ?? 0) + (w.Agents.GetValueOrDefault(caribou)?.Members.Count ?? 0));
            Assert.Empty(w.Encounters);
            var ended = events.OfType<FightEnded>().ToDictionary(f => f.Agent, f => f.Result);
            Assert.Equal(result, ended[wolves]);
            Assert.Equal(Combat.Mirror(result), ended[caribou]);
        }
    }

    // ---- prey (old PreyBehaviorTests) ----

    [Theory]
    [InlineData(Species.Rabbit)]
    [InlineData(Species.Fox)]
    public void SpottedSmallGameRunsInsteadOfFighting(Species species)
    {
        var (w, t) = Field();
        var e = PlayerVs(w, t, species, 4);
        var prey = B(e);
        double vitality = Body.Vitality(w.Agent(AgentId.Player).Body);
        double last = prey.Position.DistanceTo(Me(e).Position);
        for (int turn = 0; turn < 30 && prey.InField; turn++)
        {
            Combat.AiTurn(w, e, prey, []);
            if (!prey.InField) break;
            double now = prey.Position.DistanceTo(Me(e).Position);
            Assert.True(now >= last, $"{species} closed from {last:F1}m to {now:F1}m");
            last = now;
        }
        Assert.False(prey.InField); // off the map edge, alive
        Assert.Equal(vitality, Body.Vitality(w.Agent(AgentId.Player).Body));
    }

    [Fact]
    public void AnimalsNeverBlock()
    {
        var (w, t) = Field();
        var e = PlayerVs(w, t, Species.Wolf, 1);
        B(e).BoldnessModifier = -10; // won't attack, so it picks a defensive action
        for (int i = 0; i < 200; i++) Assert.NotEqual(CombatAction.Block, Combat.ChooseAction(w, e, B(e)));
    }

    [Fact]
    public void CrippledLegsCutMovement()
    {
        var (w, t) = Field();
        var e = PlayerVs(w, t, Species.Fox, 5, count: 2);
        Normalize(w, B(e).Agent);
        Cripple(Combat.BodyOf(w, B(e, 1)));
        Assert.Equal(3, Combat.Allowance(w, B(e, 0)));
        Assert.InRange(Combat.Allowance(w, B(e, 1)), 1, 2);
    }

    /// Fraction of stalks from 34 m to 8 m that end with the prey not yet engaged.
    static double StalkSuccess(double detectionFactor)
    {
        var (w, t) = Field();
        var rabbit = Herd(w, Species.Rabbit, 1, t);
        int successes = 0, trials = 2000;
        for (int i = 0; i < trials; i++)
        {
            var e = Combat.Create(w, [Player()], [new(rabbit) { DetectionFactor = detectionFactor }], t, 34, Awareness.Engaged, Awareness.Unaware);
            var prey = B(e);
            var hunter = Me(e);
            while (hunter.Position.DistanceTo(prey.Position) > 8 && prey.Awareness != Awareness.Engaged)
            {
                hunter.Position = hunter.Position.Toward(prey.Position, 3);
                Combat.RunDetection(w, e, hunter, 0, Combat.StalkStepMeters);
            }
            if (prey.Awareness != Awareness.Engaged) successes++;
            w.Encounters.Clear();
        }
        return successes / (double)trials;
    }

    [Fact]
    public void StalkingAFeedingAnimalUsuallyWorksAndAnAlertOneRarelyDoes()
    {
        double feeding = StalkSuccess(0.7), alert = StalkSuccess(2.0);
        Assert.InRange(feeding, 0.5, 0.9);
        Assert.InRange(alert, 0.0, 0.25);
        Assert.True(feeding > alert + 0.3);
    }

    [Fact]
    public void StandingStillIsNeverNoticed()
    {
        var (w, t) = Field();
        var e = PlayerVs(w, t, Species.Rabbit, 5, Awareness.Unaware);
        for (int i = 0; i < 200; i++) Combat.RunDetection(w, e, Me(e), 0, exposureMeters: 0);
        Assert.Equal(Awareness.Unaware, B(e).Awareness);
    }

    // ---- placement and pursuit (old EncounterTacticsTests) ----

    static (World W, Encounter E) Tactics(int wolves = 1, int allies = 0)
    {
        var (w, t) = Field();
        var pack = Herd(w, Species.Wolf, wolves, t);
        var friends = allies > 0 ? Herd(w, Species.Human, allies, t) : default;
        var team = new List<Fighter> { Player() };
        team.AddRange(Enumerable.Range(0, allies).Select(i => new Fighter(friends, i)));
        return (w, Combat.Create(w, team, [.. Enumerable.Range(0, wolves).Select(i => new Fighter(pack, i))], t, 20, Awareness.Engaged, Awareness.Engaged));
    }

    [Theory]
    [InlineData(Formation.Cluster)]
    [InlineData(Formation.BroadFront)]
    [InlineData(Formation.Pincer)]
    [InlineData(Formation.Surround)]
    public void FormationsKeepPlayerCentralAlliesNearbyAndAnimalsOnPerimeter(Formation formation)
    {
        var (w, e) = Tactics(4, 5);
        var rng = Rng.ForSystem(3, "test");
        for (int run = 0; run < 100; run++)
        {
            Combat.Place(w, e, Opening.Approach, 20, rng, formation);
            var me = Me(e);
            Assert.InRange(me.Position.X, 23, 27);
            Assert.InRange(me.Position.Y, 23, 27);
            Assert.Equal(e.Units.Count, e.Units.Select(u => u.Position).Distinct().Count());
            Assert.All(e.Units.Where(u => u.Team == 0), u => Assert.InRange(u.Position.DistanceTo(me.Position), 0, 4));
            Assert.All(e.Units.Where(u => u.Team == 1), u => Assert.InRange(Combat.DistanceFromEdge(u.Position), 1, 6));
            if (formation == Formation.Surround)
            {
                var pack = e.Units.Where(u => u.Team == 1).ToList();
                Assert.Contains(pack, u => u.Position.X < me.Position.X);
                Assert.Contains(pack, u => u.Position.X > me.Position.X);
                Assert.Contains(pack, u => u.Position.Y < me.Position.Y);
                Assert.Contains(pack, u => u.Position.Y > me.Position.Y);
            }
        }
    }

    [Fact]
    public void FormationEligibilityUsesSpeciesAndActualCount()
    {
        var rng = Rng.ForSystem(5, "test");
        for (int i = 0; i < 500; i++)
        {
            Assert.Equal(Formation.Cluster, Combat.SelectFormation(Species.Bear, 4, rng));
            Assert.Equal(Formation.Cluster, Combat.SelectFormation(Species.Wolf, 1, rng));
            Assert.NotEqual(Formation.Surround, Combat.SelectFormation(Species.Wolf, 2, rng));
            Assert.Contains(Combat.SelectFormation(Species.Hyena, 4, rng), new[] { Formation.Cluster, Formation.BroadFront });
        }
        Assert.Equal(4, Enumerable.Range(0, 500).Select(_ => Combat.SelectFormation(Species.Wolf, 4, rng)).Distinct().Count());
    }

    [Theory]
    [InlineData(Opening.CloseEncounter)]
    [InlineData(Opening.Ambush)]
    public void CloseOpeningsPreserveAuthoredDistance(Opening opening)
    {
        var (w, e) = Tactics();
        var rng = Rng.ForSystem(7, "test");
        for (int i = 0; i < 50; i++)
        {
            Combat.Place(w, e, opening, 5, rng);
            Assert.InRange(Me(e).Position.DistanceTo(B(e).Position), 4.3, 5.7);
            Assert.Equal(Formation.Cluster, e.Formation);
        }
    }

    [Fact]
    public void LateArrivalsUseFreeEdgeNearTheirSide()
    {
        var (w, e) = Tactics();
        Me(e).Position = new Cell(44, 24);
        var friends = Herd(w, Species.Human, 2, e.Tile);
        Combat.Join(w, e, new Fighter(friends, 0), 0);
        Combat.Join(w, e, new Fighter(friends, 1), 0);
        Assert.All(e.Units.Where(u => u.Agent == friends), u => Assert.Equal(48, u.Position.X));
        Assert.Equal(e.Units.Count, e.Units.Select(u => u.Position).Distinct().Count());
    }

    [Fact]
    public void PursuitClosesAtFullSpeedAndRespectsMorale()
    {
        var (w, e) = Tactics();
        Normalize(w, B(e).Agent);
        var wolf = B(e);
        Me(e).Position = new Cell(25, 25);
        wolf.Position = new Cell(25, 45);
        wolf.BoldnessModifier = 10;
        Assert.Equal(new Cell(25, 39), Combat.MovePosition(w, e, wolf));
        wolf.BoldnessModifier = -100;
        Assert.Null(Combat.PursuitTarget(w, e, wolf));
        Assert.True(Combat.MovePosition(w, e, wolf).Y > wolf.Position.Y);
    }

    [Fact]
    public void PursuitCannotCrossAnOccupiedWall()
    {
        var (w, e) = Tactics();
        var wolf = B(e);
        Me(e).Position = new Cell(25, 25);
        wolf.Position = new Cell(25, 30);
        var wall = Herd(w, Species.Human, Combat.MapSize, e.Tile);
        for (int x = 0; x < Combat.MapSize; x++)
            e.Units.Add(new CombatUnit { Agent = wall, Member = x, Team = 0, Position = new Cell(x, 28) });
        var destination = Combat.Pursue(w, e, wolf, Me(e));
        Assert.True(destination.Y >= 29);
        Assert.DoesNotContain(e.Units.Where(u => u != wolf), u => u.Position == destination);
    }

    [Theory]
    [InlineData(0, -3)]
    [InlineData(-2, -2)]
    public void CommittedWolfAttemptsAttackBeforeContinuouslyRetreatingPlayerEscapes(int dx, int dy)
    {
        var (w, e) = Tactics();
        var me = Me(e);
        var wolf = B(e);
        me.Position = new Cell(25, 25);
        wolf.Position = new Cell(25, 45);
        wolf.BoldnessModifier = 10;
        bool struck = false;
        for (int round = 0; round < 12 && !Combat.CanFlee(me.Position); round++)
        {
            Combat.Move(w, e, me, new Cell(me.Position.X + dx, me.Position.Y + dy));
            var rngBefore = w.Rng("combat", wolf.Agent).State;
            var from = wolf.Position;
            Combat.AiTurn(w, e, wolf, []);
            // A pursuit strike is the only thing in a Move turn that rolls: it ends within 1.5 m.
            if (from != wolf.Position && wolf.Position.DistanceTo(me.Position) <= 1.5 && w.Rng("combat", wolf.Agent).State != rngBefore) { struck = true; break; }
        }
        Assert.True(struck);
        Assert.False(Combat.CanFlee(me.Position));
    }

    [Theory]
    [InlineData(Species.CaveBear, 4)]
    [InlineData(Species.Bear, 5)]
    [InlineData(Species.Hyena, 6)]
    [InlineData(Species.Wolf, 6)]
    [InlineData(Species.Homotherium, 7)]
    public void SpeciesMovementAndDiagonalPursuitStayWithinBudget(Species species, int expected)
    {
        var (w, t) = Field();
        var e = PlayerVs(w, t, species, 20);
        var unit = B(e);
        unit.Position = new Cell(40, 40);
        Me(e).Position = new Cell(25, 25);
        Assert.Equal(expected, Combat.Allowance(w, unit));
        var destination = Combat.Pursue(w, e, unit, Me(e));
        Assert.InRange(unit.Position.DistanceTo(destination), 1, expected);
        Assert.True(destination.DistanceTo(Me(e).Position) < unit.Position.DistanceTo(Me(e).Position));
    }

    [Theory]
    [InlineData(Awareness.Unaware)]
    [InlineData(Awareness.Alert)]
    public void UnawareAndAlertAnimalsCannotUsePursuitAttack(Awareness awareness)
    {
        var (w, e) = Tactics();
        var wolf = B(e);
        wolf.Awareness = awareness;
        wolf.BoldnessModifier = 10;
        wolf.Position = new Cell(Me(e).Position.X, Me(e).Position.Y + 4);
        Assert.Null(Combat.PursuitTarget(w, e, wolf));
        var events = new List<WorldEvent>();
        Combat.AiTurn(w, e, wolf, events);
        Assert.Empty(events.OfType<Injured>());
        Assert.False(wolf.JustDamaged);
    }

    // ---- companions (old CompanionCombatTests, the mechanical part) ----

    [Fact]
    public void RepeatedRetreatCallsCannotStackMoralePenalties()
    {
        var (w, e) = Tactics(1, 1);
        var me = Me(e);
        var friend = e.Units.Single(u => u.Team == 0 && u != me);
        Assert.Null(Combat.Apply(w, e, me, CombatAction.CallRetreat, []).Refused);
        double morale = friend.BoldnessModifier;
        Assert.Equal(-0.75, morale, 9);
        Assert.NotNull(Combat.Apply(w, e, me, CombatAction.CallRetreat, []).Refused);
        w.Minute += 5;
        Assert.Null(Combat.Apply(w, e, me, CombatAction.CallRetreat, []).Refused);
        Assert.Equal(morale, friend.BoldnessModifier, 9);
        for (int i = 0; i < 5; i++) Sim.Step(w, new Fight(AgentId.Player, CombatAction.Wait), 1);
        Assert.Equal(0, friend.SignalModifier); // the call lapses after five minutes
    }

    // ---- the player's rounds through Sim.Step ----

    [Fact]
    public void EachRoundIsOneMinute_RefusalsCostNothing()
    {
        var (w, t) = Field();
        Assert.Equal("You are not in a fight.", Sim.Step(w, new Fight(AgentId.Player, CombatAction.Wait), 1).InterruptedReason);
        var e = PlayerVs(w, t, Species.Wolf, 20);
        var refused = Sim.Step(w, new Fight(AgentId.Player, CombatAction.Attack), 1);
        Assert.Equal(0, refused.MinutesElapsed);
        Assert.Equal("Requires close or near range.", refused.InterruptedReason);
        Assert.Equal(0, w.Minute);
        Assert.Equal(0, e.Rounds);

        var round = Sim.Step(w, new Fight(AgentId.Player, CombatAction.Wait), 1);
        Assert.Equal(1, round.MinutesElapsed);
        Assert.Equal(1, w.Minute);
        Assert.Equal(1, e.Rounds);
        Assert.Equal("You are in a fight.", Sim.Step(w, new Travel(AgentId.Player, Direction.North), 1).InterruptedReason);
        Assert.Equal("You are in a fight.", Sim.Step(w, new Wait(B(e).Agent), 1).InterruptedReason);
        Assert.Equal(1, w.Minute);
        Assert.StartsWith("Only the player", Sim.Step(w, new Fight(B(e).Agent, CombatAction.Wait), 1).InterruptedReason);
    }

    [Fact]
    public void AFightEndsInTheStepThatEndsIt()
    {
        var (w, t) = Field();
        var e = PlayerVs(w, t, Species.Rabbit, 2, weapon: Spear(w));
        var rabbit = B(e).Agent;
        StepResult r = null!;
        for (int i = 0; i < 40 && w.Encounters.Count > 0; i++)
            r = Sim.Step(w, new Fight(AgentId.Player, Combat.CanFlee(Me(e).Position) ? CombatAction.Flee : CombatAction.Retreat), 1);
        Assert.Empty(w.Encounters);
        Assert.Equal("fight over", r.InterruptedReason);
        Assert.Contains(r.Events, ev => ev is FightEnded f && f.Agent == AgentId.Player);
        Assert.True(w.Agents.ContainsKey(rabbit) || r.Events.OfType<Killed>().Any());
        Assert.Contains("wooden_spear", Items.Pack(w, AgentId.Player).Gear.Select(g => g.Kind)); // the wielded spear went back in the pack
    }

    [Fact]
    public void ThePlayerDyingEndsTheFightAsDefeat()
    {
        var (w, t) = Field();
        var e = PlayerVs(w, t, Species.CaveBear, 1, count: 1);
        w.Agent(AgentId.Player).Body.BloodPct = 0.51;
        Effects.Add(w.Agent(AgentId.Player).Body, EffectKind.Bleeding, 1.0);
        StepResult r = null!;
        for (int i = 0; i < 60 && w.Encounters.Count > 0; i++) r = Sim.Step(w, new Fight(AgentId.Player, CombatAction.Wait), 1);
        Assert.Equal("died", r.InterruptedReason);
        Assert.Single(r.Events.OfType<Died>(), d => d.Who == AgentId.Player);
        Assert.Equal(CombatResult.Defeat, r.Events.OfType<FightEnded>().Single(f => f.Agent == AgentId.Player).Result);
        Assert.True(w.Agents.ContainsKey(AgentId.Player)); // a dead person stays where they fell
    }

    [Theory]
    [InlineData(1, new[] { CombatAction.Wait, CombatAction.Advance, CombatAction.Retreat, CombatAction.Attack, CombatAction.Block, CombatAction.Shove, CombatAction.CallHelp, CombatAction.CallRetreat })]
    [InlineData(2, new[] { CombatAction.Wait, CombatAction.Advance, CombatAction.Retreat, CombatAction.Attack, CombatAction.Dodge, CombatAction.Block, CombatAction.CallHelp, CombatAction.CallRetreat })]
    [InlineData(10, new[] { CombatAction.Wait, CombatAction.Advance, CombatAction.Retreat, CombatAction.Throw, CombatAction.ThrowStone, CombatAction.Intimidate, CombatAction.CallHelp, CombatAction.CallRetreat })]
    [InlineData(22, new[] { CombatAction.Wait, CombatAction.Advance, CombatAction.Retreat, CombatAction.Intimidate, CombatAction.CallHelp, CombatAction.CallRetreat })]
    public void ActionsDependOnTheZone(int distance, CombatAction[] expected)
    {
        var (w, t) = Field();
        Items.Add(Items.Pack(w, AgentId.Player), Resource.Stone, 1.0);
        var e = PlayerVs(w, t, Species.Wolf, distance, weapon: Spear(w));
        Assert.Equal(expected.Order(), Combat.Actions(w, e, Me(e)).Order());
        Assert.Equal("Move nearer an edge to flee.", Combat.Refusal(w, e, Me(e), CombatAction.Flee));
        Assert.Equal("That's too far to move in one action.", Combat.Refusal(w, e, Me(e), CombatAction.Move, to: new Cell(Me(e).Position.X + 4, Me(e).Position.Y)));
        Assert.Null(Combat.Refusal(w, e, Me(e), CombatAction.Move, to: new Cell(Me(e).Position.X + 3, Me(e).Position.Y)));
    }

    [Fact]
    public void ThrownSpearsLandOnTheGround_StonesComeOutOfThePack()
    {
        var (w, t) = Field();
        var pack = Items.Pack(w, AgentId.Player);
        Items.Add(pack, Resource.Stone, 0.5);
        var spear = Spear(w);
        var e = PlayerVs(w, t, Species.Bison, 10, weapon: spear);
        Assert.DoesNotContain(spear, pack.Gear); // wielded, not packed
        Assert.Null(Combat.Apply(w, e, Me(e), CombatAction.Throw, []).Refused);
        Assert.Null(Me(e).Weapon);
        Assert.Contains(spear, Items.StashAt(w, t).Gear);
        Assert.Equal("No weapon to throw.", Combat.Refusal(w, e, Me(e), CombatAction.Throw));
        Assert.Null(Combat.Apply(w, e, Me(e), CombatAction.ThrowStone, []).Refused);
        Assert.Equal(0.2, Items.Have(pack, Resource.Stone), 9);
        Assert.Equal("No stones to throw.", Combat.Refusal(w, e, Me(e), CombatAction.ThrowStone));
    }

    [Fact]
    public void FleeingNeedsAnEdgeAndCostsBreath()
    {
        var (w, t) = Field();
        var e = PlayerVs(w, t, Species.Wolf, 20);
        Me(e).Position = new Cell(2, 25);
        var body = w.Agent(AgentId.Player).Body;
        double energy = body.EnergyMinutes, kcal = body.CaloriesKcal;
        Combat.Apply(w, e, Me(e), CombatAction.Flee, []);
        Assert.False(Me(e).InField);
        // Running for it empties the sprint reserve, which costs a little sleep and the run's calories.
        // Old: a flat twenty minutes of sleep, and nothing else.
        Assert.Equal(0, Body.SprintReservePct(body), 9);
        Assert.Equal(energy - Body.SpentReserveEnergyMinutes, body.EnergyMinutes, 9);
        Assert.True(body.CaloriesKcal < kcal - 3, $"the run cost {kcal - body.CaloriesKcal:0.0} kcal");
        Assert.Equal(CombatResult.Fled, Combat.Result(w, e));
    }

    [Fact]
    public void CoverHidesTheStalker()
    {
        var (w, t) = Field();
        var map = Map.Of(w);
        var forest = Enumerable.Range(0, map.Width).SelectMany(x => Enumerable.Range(0, map.Height).Select(y => new TileId(x, y)))
            .First(p => map.TerrainAt(p) == Terrain.Forest && map.CaveAt(p) == Cave.None && p != map.Camp);
        var open = PlayerVs(w, t, Species.Caribou, 30, Awareness.Unaware);
        w.Agent(AgentId.Player).Tile = forest;
        var hidden = PlayerVsAt(w, forest);
        double inOpen = Combat.DetectionRisk(w, open, Me(open), B(open)), inForest = Combat.DetectionRisk(w, hidden, Me(hidden), B(hidden));
        Assert.Equal(Combat.StepDetectionChance(Combat.DetectionChance(30, Awareness.Unaware) * 0.85, 3), inOpen, 9);
        Assert.Equal(Combat.StepDetectionChance(Combat.DetectionChance(30, Awareness.Unaware) * (1 - 0.75 * 0.3), 3), inForest, 9);
    }

    static Encounter PlayerVsAt(World w, TileId tile)
    {
        w.Encounters.Clear();
        var herd = Herd(w, Species.Caribou, 1, tile);
        return Combat.Create(w, [Player()], [new(herd)], tile, 30, Awareness.Engaged, Awareness.Unaware);
    }

    [Fact]
    public void AHerdSendsItsLeadAndUpToThreeOthers()
    {
        var (w, t) = Field();
        var pack = Herd(w, Species.Wolf, 8, t);
        var sizes = new HashSet<int>();
        for (int i = 0; i < 200; i++)
        {
            var sent = Combat.SendMembers(w, pack, 5);
            Assert.Equal(new Fighter(pack, 5), sent[0]);
            Assert.Equal(sent.Count, sent.Distinct().Count());
            sizes.Add(sent.Count);
        }
        Assert.Equal([1, 2, 3, 4], sizes.Order());
        Assert.Single(Combat.SendMembers(w, Herd(w, Species.Bear, 1, t), 0));
    }

    [Fact]
    public void OpeningMoraleAndTheFearAFightLeaves()
    {
        var (w, t) = Field();
        var wolf = Herd(w, Species.Wolf, 1, t);
        var e = Combat.Create(w, [Player()], [new(wolf) { BoldnessDelta = 0.25 - 0.5 }], t, 20, Awareness.Engaged, Awareness.Engaged);
        Assert.Equal(-0.25, B(e).BoldnessModifier, 9);
        Assert.Equal([0.9, 0.7, 0.2, 0.0, 0.4],
            new[] { CombatResult.Defeat, CombatResult.Fled, CombatResult.AnimalFled, CombatResult.Victory, CombatResult.AnimalDisengaged }.Select(Combat.FearAfter));
        Assert.Equal(2.0, Combat.AwarenessDamageFactor(Awareness.Unaware));
        Assert.Equal(1.0, Combat.AwarenessDamageFactor(Awareness.Alert));
    }

    // ---- the agent model ----

    [Fact]
    public void AHerdSendsThreeOfItsEightAndKeepsTheRest()
    {
        var (w, t) = Field();
        var pack = Herd(w, Species.Wolf, 8, t);
        var stayed = w.Agent(pack).Members.Skip(3).ToList();
        var e = Combat.Create(w, [Player(Spear(w))], [new(pack, 0), new(pack, 1), new(pack, 2)], t, 5, Awareness.Engaged, Awareness.Engaged);
        Assert.Equal(3, e.Units.Count(u => u.Agent == pack));
        // Two of the three fall; the fight ends when the last runs.
        Combat.BodyOf(w, B(e, 0)).BloodPct = 0;
        Combat.BodyOf(w, B(e, 2)).BloodPct = 0;
        var events = new List<WorldEvent>();
        Combat.Apply(w, e, B(e, 1), CombatAction.Wait, events); // any action sweeps the field
        Assert.Equal(2, events.OfType<Killed>().Count(k => k.Agent == pack && k.Species == Species.Wolf));
        Combat.BodyOf(w, B(e, 1)).BloodPct = 0;
        Sim.Step(w, new Fight(AgentId.Player, CombatAction.Wait), 1);

        Assert.Equal(5, w.Agent(pack).Members.Count);
        Assert.Equal(stayed, w.Agent(pack).Members); // the five that stayed are the same bodies
        Assert.Equal(3, w.Tiles[t].Carcasses!.Count);
    }

    [Fact]
    public void AHerdWithNobodyLeftIsGone()
    {
        var (w, t) = Field();
        var e = PlayerVs(w, t, Species.Wolf, 1, count: 1);
        var wolf = B(e).Agent;
        Combat.BodyOf(w, B(e)).BloodPct = 0;
        var r = Sim.Step(w, new Fight(AgentId.Player, CombatAction.Wait), 1);
        Assert.Equal("fight over", r.InterruptedReason);
        Assert.False(w.Agents.ContainsKey(wolf));
        Assert.Equal(CombatResult.Victory, r.Events.OfType<FightEnded>().Single(f => f.Agent == AgentId.Player).Result);
    }

    [Fact]
    public void AWoundedAnimalThatGetsAwaySplitsIntoItsOwnAgent()
    {
        var (w, t) = Field();
        var herd = Herd(w, Species.Caribou, 5, t);
        var e = Combat.Create(w, [Player()], [new(herd, 1)], t, 5, Awareness.Engaged, Awareness.Engaged);
        var wounded = Combat.BodyOf(w, B(e));
        wounded.Regions[2].Muscle.Condition = 0.6;
        B(e).Position = new Cell(B(e).Position.X, Combat.MapSize - 1);
        var events = new List<WorldEvent>();
        Combat.Move(w, e, B(e), new Cell(B(e).Position.X, Combat.MapSize)); // off the edge
        var r = Sim.Step(w, new Fight(AgentId.Player, CombatAction.Wait), 1);

        var escaped = r.Events.OfType<WoundedEscaped>().Single();
        Assert.Equal(4, w.Agent(herd).Members.Count);
        Assert.Same(wounded, w.Agent(escaped.Agent).Body);
        Assert.Equal(Species.Caribou, w.Agent(escaped.Agent).Species);
        // It lived the round's minute before it got away: one minute of mending off the wound.
        Assert.Equal(0.3 + (0.4 - Fauna.MendPctPerMinute) * 0.5, escaped.Severity, 9);
        Assert.Equal(CombatResult.AnimalFled, r.Events.OfType<FightEnded>().Single(f => f.Agent == AgentId.Player).Result);
    }

    [Fact]
    public void SplitOff_MovesOneMemberOut_AndRefusesMidFight()
    {
        var (w, t) = Field();
        var herd = Herd(w, Species.Wolf, 3, t);
        var second = w.Agent(herd).Members[1];
        var split = Agents.SplitOff(w, herd, 1);
        Assert.NotEqual(herd, split);
        Assert.Equal(2, w.Agent(herd).Members.Count);
        Assert.Same(second, w.Agent(split).Body);
        Assert.Equal(t, w.Agent(split).Tile);
        Assert.Equal(split, Agents.SplitOff(w, split, 0)); // already alone
        Combat.Create(w, [Player()], [new(herd, 0)], t, 5, Awareness.Engaged, Awareness.Engaged);
        Assert.Throws<InvalidOperationException>(() => Agents.SplitOff(w, herd, 1));
    }

    [Fact]
    public void HeadlessFightsAreDeterministic()
    {
        string Run()
        {
            var (w, t) = Field(9);
            var wolves = Herd(w, Species.Wolf, 4, t);
            var caribou = Herd(w, Species.Caribou, 3, t);
            var events = new List<WorldEvent>();
            var e = Combat.Create(w, [.. Enumerable.Range(0, 4).Select(i => new Fighter(wolves, i))], [new(caribou, 0), new(caribou, 2)], t,
                15, Awareness.Engaged, Awareness.Alert);
            Combat.ResolveHeadless(w, e, events);
            return Save.ToJson(w) + string.Join("|", events);
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void SaveAndLoadMidFightResumesIt()
    {
        var (w, t) = Field(4);
        PlayerVs(w, t, Species.Wolf, 6, count: 2, weapon: Spear(w));
        Sim.Step(w, new Fight(AgentId.Player, CombatAction.Wait), 1);
        Assert.Single(w.Encounters);
        var loaded = Save.FromJson(Save.ToJson(w));
        Assert.Equal(Save.ToJson(w), Save.ToJson(loaded));
        foreach (var world in new[] { w, loaded })
            for (int i = 0; i < 3 && world.Encounters.Count > 0 && Body.IsAlive(world.Agent(AgentId.Player).Body); i++)
                Sim.Step(world, new Fight(AgentId.Player, Combat.Actions(world, world.Encounters[0], Me(world.Encounters[0])).Contains(CombatAction.Attack) ? CombatAction.Attack : CombatAction.Advance), 1);
        Assert.Equal(Save.ToJson(w), Save.ToJson(loaded));
    }

    // ---- old bugs, fixed ----

    [Fact]
    public void AUnitIsNotItsOwnAlly()
    {
        // Old game: a lone wolf counted itself as an ally (2.51), and its own wound made it bolder.
        var (w, t) = Field();
        var e = PlayerVs(w, t, Species.Wolf, 1);
        Normalize(w, B(e).Agent);
        Assert.Equal(1.3 + 1.0, Combat.UnitBoldness(w, e, B(e)), 9);
        Me(e).BlockSet = false;
        Combat.Apply(w, e, Me(e), CombatAction.Attack, []);
        if (Combat.BodyOf(w, B(e)).Regions.Any(r => r.Condition < 1)) Assert.Equal(-0.1, B(e).BoldnessModifier, 9);
        var pack = PlayerVsPack(3);
        Assert.Equal(1.3 + 1.0 + 0.15 * 2 * (0.7 * 2), Combat.UnitBoldness(pack.W, pack.E, B(pack.E)), 9);
    }

    static (World W, Encounter E) PlayerVsPack(int count)
    {
        var (w, t) = Field();
        var e = PlayerVs(w, t, Species.Wolf, 5, count: count);
        Normalize(w, B(e).Agent);
        return (w, e);
    }

    [Fact]
    public void APlayersGuardLastsOneRound()
    {
        // Old game never cleared the player's stances: one Block guarded every later blow.
        var (w, t) = Field();
        var e = PlayerVs(w, t, Species.Wolf, 1);
        Assert.Null(Combat.Apply(w, e, Me(e), CombatAction.Block, []).Refused);
        Assert.True(Me(e).BlockSet);
        Combat.Apply(w, e, Me(e), CombatAction.Wait, []);
        Assert.False(Me(e).BlockSet);
    }

    [Fact]
    public void AnEnemyThatBleedsOutBetweenBlowsEndsTheFight()
    {
        // Old game: a fighter that died of its wounds stayed on the field, so the fight never ended.
        var (w, t) = Field();
        var e = PlayerVs(w, t, Species.Wolf, 20);
        var body = Combat.BodyOf(w, B(e));
        body.BloodPct = 0.5005;
        Effects.Add(body, EffectKind.Bleeding, 1.0);
        var r = Sim.Step(w, new Fight(AgentId.Player, CombatAction.Wait), 1);
        Assert.Equal("fight over", r.InterruptedReason);
        Assert.Null(r.Events.OfType<Killed>().Single().By);
        Assert.Equal(CombatResult.Victory, r.Events.OfType<FightEnded>().Single(f => f.Agent == AgentId.Player).Result);
    }

    [Fact]
    public void EveryoneStillActsWhenSomeoneLeavesMidRound()
    {
        // Old game indexed into a list that shrank as a unit fled, so the next one lost its turn.
        var (w, t) = Field();
        var caribou = Herd(w, Species.Caribou, 1, t);
        var wolf = Herd(w, Species.Wolf, 1, t);
        var e = Combat.Create(w, [Player()], [new(caribou), new(wolf)], t, 20, Awareness.Engaged, Awareness.Engaged);
        var runner = e.Units.Single(u => u.Agent == caribou);
        var hunter = e.Units.Single(u => u.Agent == wolf);
        Me(e).Position = new Cell(25, 10);
        runner.Position = new Cell(25, Combat.MapSize - 1); // one step from leaving
        hunter.Position = new Cell(25, 40);
        hunter.BoldnessModifier = 10;
        Sim.Step(w, new Fight(AgentId.Player, CombatAction.Wait), 1);
        Assert.False(runner.InField);
        Assert.NotEqual(new Cell(25, 40), hunter.Position);
    }

    // ---- review 2: fled fighters, and fights that outlive their player ----

    /// Review 2 J1: a fight holds only who is in its field (Sim.Held), so an NPC that fled the
    /// player's fight lives on its own mind; when that mind hunts, the hunt admits it and releases it
    /// from the fight it ran from (old: "already in another fight" thrown from the scheduler).
    [Fact]
    public void AFledNpc_CanHuntElsewhere_AndBothFightsResolve()
    {
        var w = World.New(17);
        Sim.Advance(w, 60);
        w.Agent(AgentId.Player).Members[0] = BodyState.BaselineHuman();
        var tile = w.Agent(AgentId.Player).Tile;
        var npc = Npc.Spawn(w, tile);
        Items.Pack(w, npc).Gear.Add(Items.Make("wooden_spear"));
        var wolves = Agents.Spawn(w, Species.Wolf, tile, 2);
        var enc = Combat.Create(w, [new Fighter(AgentId.Player), new Fighter(npc)], [new Fighter(wolves, 0)], tile, 20, Awareness.Engaged, Awareness.Engaged);
        var fled = enc.Units.Single(u => u.Agent == npc);
        fled.InField = false; // it fled; the player fights on
        var bison = Agents.Spawn(w, Species.Bison, tile, 3);
        var mind = (NpcMind)w.Agent(npc).Mind;
        var b = w.Agent(npc).Body;
        b.FatKg = 0.025 * b.WeightKg; b.CaloriesKcal = 0; b.CoreTempF = 98.6; // starving: a bison is worth the risk (Npc.WouldHunt)
        Items.Add(Items.StashAt(w, tile), Resource.Birch, 60);

        var events = new List<WorldEvent>();
        for (int i = 0; i < 30; i++) events.AddRange(Sim.Advance(w, 1));
        Assert.Contains(events, e => e is FightEnded f && f.Agent == bison); // the NPC's hunt was fought out
        Assert.True(fled.Released);
        // The player's fight goes on (or ended) without reading the NPC again: it hears nothing of it.
        for (int i = 0; i < 600 && w.Encounters.Contains(enc); i++) events.AddRange(Sim.Advance(w, 1));
        Assert.DoesNotContain(w.Encounters, e => e == enc);
        Assert.Single(events.OfType<FightEnded>(), f => f.Agent == AgentId.Player);
    }

    /// Releasing a fled fighter settles it out of the old fight: its weapon comes back to its pack,
    /// and the old fight's aftermath leaves its body alone (it may be dead, or reordered, by then).
    [Fact]
    public void AdmittingAFledFighter_ReleasesItFromTheFightItRanFrom()
    {
        var (w, tile) = Field();
        var npc = Npc.Spawn(w, tile);
        var spear = Items.Make("wooden_spear");
        Items.Pack(w, npc).Gear.Add(spear);
        var wolves = Herd(w, Species.Wolf, 2, tile);
        var old = Combat.Create(w, [new Fighter(AgentId.Player), new Fighter(npc) { Weapon = spear }], [new Fighter(wolves, 0)], tile, 20, Awareness.Engaged, Awareness.Engaged);
        var fled = old.Units.Single(u => u.Agent == npc);
        // Still in the field: held, and refused elsewhere.
        var bear = Herd(w, Species.Bear, 1, tile);
        Assert.Throws<InvalidOperationException>(() => Combat.Create(w, [new Fighter(npc)], [new Fighter(bear, 0)], tile, 20, Awareness.Engaged, Awareness.Engaged));
        fled.InField = false;
        var other = Combat.Create(w, [new Fighter(npc) { Weapon = spear }], [new Fighter(bear, 0)], tile, 20, Awareness.Engaged, Awareness.Engaged);
        Assert.True(fled.Released);
        Assert.Null(fled.Weapon);
        Assert.Same(other, Combat.EncounterOf(w, npc));
        w.Agent(npc).Body.BloodPct = 0; // dies in the new fight
        Combat.ResolveHeadless(w, other, []);
        // The old fight ends without the NPC: no carcass, no fall, no report of it.
        var events = new List<WorldEvent>();
        Combat.Finish(w, old, events);
        Assert.DoesNotContain(events, e => e is FightEnded f && f.Agent == npc);
        Assert.Contains(events, e => e is FightEnded f && f.Agent == wolves);
        Assert.Empty(w.Encounters);
    }

    /// Review 2 F1: Advance while the player is in a fight waits it out round by round, so the
    /// fight goes on: a bleeding player dies in it and the wolves are free again (old: no round ran,
    /// the fight never ended and held the wolves for good).
    [Fact]
    public void AdvanceWhileInAFight_FightsWaitingRounds_UntilItEnds()
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var wolves = Agents.Spawn(w, Species.Wolf, camp, 3);
        var enc = Combat.Create(w, [new Fighter(AgentId.Player)], [new Fighter(wolves, 0)], camp, 8, Awareness.Engaged, Awareness.Engaged);
        Effects.Add(w.Agent(AgentId.Player).Body, EffectKind.Bleeding, 1.0);
        var events = new List<WorldEvent>();
        for (int i = 0; i < 1440 && w.Encounters.Count > 0; i++) events.AddRange(Sim.Advance(w, 1));
        Assert.Empty(w.Encounters);
        Assert.Contains(events, e => e is FightEnded f && f.Agent == AgentId.Player);
        Assert.True(enc.Rounds > 0);
        Assert.Contains(events, e => e is Died d && d.Who == AgentId.Player);
        Assert.False(Sim.Held(w, wolves));
        // Their mind decides again: a kill with no carcass to it feeds nobody, so they lie up.
        Assert.Equal(HerdState.Feeding, Fauna.MindOf(w.Agent(wolves)).State);
        Sim.Advance(w, 5);
        Assert.NotEqual(HerdState.Feeding, Fauna.MindOf(w.Agent(wolves)).State);
    }

    /// However the player dies outside a round, the next minute ends the fight and frees its sides.
    [Fact]
    public void AFightWhosePlayerDiedOutsideARound_EndsOnTheNextMinute()
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var wolves = Agents.Spawn(w, Species.Wolf, camp, 3);
        Combat.Create(w, [new Fighter(AgentId.Player)], [new Fighter(wolves, 0)], camp, 8, Awareness.Engaged, Awareness.Engaged);
        w.Agent(AgentId.Player).Body.BloodPct = 0;
        Assert.False(Body.IsAlive(w.Agent(AgentId.Player).Body));
        var events = Sim.Advance(w, 1);
        Assert.Empty(w.Encounters);
        Assert.Contains(events, e => e is FightEnded f && f.Agent == wolves && f.Result == CombatResult.Victory);
        Assert.False(Sim.Held(w, wolves));
    }
}
