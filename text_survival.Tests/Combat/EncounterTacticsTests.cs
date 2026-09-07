using text_survival.Actions;
using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Combat;
using text_survival.Environments.Grid;
using text_survival.Tests.Companions;

namespace text_survival.Tests.Combat;

public class EncounterTacticsTests
{
    private static (CompanionWorld world, CombatScenario scenario) Create(int wolves = 1, int allies = 0)
    {
        var world = new CompanionWorld();
        var team = new List<Actor> { world.Game.player };
        for (int i = 0; i < allies; i++) team.Add(world.AddNpc($"Ally {i}", 0, 0));
        var enemies = Enumerable.Range(0, wolves).Select(_ => (Actor)AnimalFactory.FromType(AnimalType.Wolf,
            world.Game.CurrentLocation, world.Map)!).ToList();
        return (world, CombatScenario.Create(team, enemies, world.Game.CurrentLocation, 20,
            AwarenessState.Engaged, AwarenessState.Engaged, world.Game.player));
    }

    [Theory]
    [InlineData(EncounterFormation.Cluster)]
    [InlineData(EncounterFormation.BroadFront)]
    [InlineData(EncounterFormation.Pincer)]
    [InlineData(EncounterFormation.Surround)]
    public void FormationsKeepPlayerCentralAlliesNearbyAndAnimalsOnPerimeter(EncounterFormation formation)
    {
        var (_, scenario) = Create(4, 5);
        for (int seed = 0; seed < 100; seed++)
        {
            EncounterPlacement.Apply(scenario, EncounterOpening.Approach, 20, new Random(seed), formation);
            var player = scenario.Player!;
            Assert.InRange(player.Position.X, 23, 27);
            Assert.InRange(player.Position.Y, 23, 27);
            Assert.Equal(scenario.Units.Count, scenario.Units.Select(u => u.Position).Distinct().Count());
            Assert.All(scenario.Team1, u => Assert.InRange(u.Position.DistanceTo(player.Position), 0, 4));
            Assert.All(scenario.Team2, u => Assert.InRange(CombatScenario.GetDistanceFromEdge(u.Position), 1, 6));
            Assert.All(scenario.Units, u => Assert.InRange(CombatScenario.GetDistanceFromEdge(u.Position), 1, 25));
            if (formation == EncounterFormation.Surround)
            {
                Assert.Contains(scenario.Team2, u => u.Position.X < player.Position.X);
                Assert.Contains(scenario.Team2, u => u.Position.X > player.Position.X);
                Assert.Contains(scenario.Team2, u => u.Position.Y < player.Position.Y);
                Assert.Contains(scenario.Team2, u => u.Position.Y > player.Position.Y);
            }
        }
    }

    [Fact]
    public void FormationEligibilityUsesSpeciesAndActualCount()
    {
        for (int seed = 0; seed < 500; seed++)
        {
            Assert.Equal(EncounterFormation.Cluster, EncounterPlacement.SelectFormation(AnimalType.Bear, 4, new(seed)));
            Assert.Equal(EncounterFormation.Cluster, EncounterPlacement.SelectFormation(AnimalType.Wolf, 1, new(seed)));
            Assert.NotEqual(EncounterFormation.Surround, EncounterPlacement.SelectFormation(AnimalType.Wolf, 2, new(seed)));
            Assert.Contains(EncounterPlacement.SelectFormation(AnimalType.Hyena, 4, new(seed)),
                new[] { EncounterFormation.Cluster, EncounterFormation.BroadFront });
        }
        Assert.Equal(4, Enumerable.Range(0, 500).Select(seed => EncounterPlacement.SelectFormation(AnimalType.Wolf, 4, new(seed))).Distinct().Count());
    }

    [Theory]
    [InlineData(EncounterOpening.CloseEncounter)]
    [InlineData(EncounterOpening.Ambush)]
    public void CloseOpeningsPreserveAuthoredDistance(EncounterOpening opening)
    {
        var (_, scenario) = Create();
        for (int seed = 0; seed < 50; seed++)
        {
            EncounterPlacement.Apply(scenario, opening, 5, new(seed));
            Assert.InRange(scenario.Player!.Position.DistanceTo(scenario.Team2[0].Position), 4.3, 5.7);
            Assert.Equal(EncounterFormation.Cluster, scenario.Formation);
        }
    }

    [Fact]
    public void LateArrivalsUseFreeEdgeNearTheirSide()
    {
        var (world, scenario) = Create();
        scenario.Player!.Position = new(44, 24);
        var first = world.AddNpc("First", 0, 0);
        var second = world.AddNpc("Second", 0, 0);
        Assert.True(scenario.AddParticipant(first, true));
        Assert.True(scenario.AddParticipant(second, true));
        Assert.All(scenario.Team1.Skip(1), u => Assert.Equal(48, u.Position.X));
        Assert.Equal(scenario.Units.Count, scenario.Units.Select(u => u.Position).Distinct().Count());
    }

    [Fact]
    public void PursuitClosesAtFullSpeedAndRespectsMorale()
    {
        var (_, scenario) = Create();
        var wolf = scenario.Team2[0];
        scenario.Player!.Position = new(25, 25);
        wolf.Position = new(25, 45);
        wolf.BoldnessModifier = 10;
        Assert.Equal(new GridPosition(25, 39), CombatAI.DetermineMovePosition(wolf, scenario));
        wolf.BoldnessModifier = -100;
        Assert.Null(CombatMovement.PursuitTarget(wolf, scenario));
        Assert.True(CombatAI.DetermineMovePosition(wolf, scenario).Y > wolf.Position.Y);
    }

    [Fact]
    public void PursuitCannotCrossAnOccupiedWall()
    {
        var (world, scenario) = Create();
        var wolf = scenario.Team2[0];
        scenario.Player!.Position = new(25, 25);
        wolf.Position = new(25, 30);
        for (int x = 0; x < CombatScenario.MAP_SIZE; x++)
            scenario.Units.Add(new Unit(world.AddNpc($"Blocker {x}", 0, 0), new(x, 28)));
        var destination = CombatMovement.Pursue(wolf, scenario.Player, scenario);
        Assert.True(destination.Y >= 29);
        Assert.DoesNotContain(scenario.Units.Where(u => u != wolf), u => u.Position == destination);
    }

    [Theory]
    [InlineData(0, -3)]
    [InlineData(-2, -2)]
    public void CommittedWolfAttemptsAttackBeforeContinuouslyRetreatingPlayerEscapes(int dx, int dy)
    {
        var (_, scenario) = Create();
        var player = scenario.Player!;
        var wolf = scenario.Team2[0];
        player.Position = new(25, 25);
        wolf.Position = new(25, 45);
        wolf.BoldnessModifier = 10;
        bool attacked = false;
        for (int round = 0; round < 12 && !CombatScenario.CanFlee(player.Position); round++)
        {
            scenario.Move(player, new(player.Position.X + dx, player.Position.Y + dy));
            string? narrative = scenario.ProcessSingleAITurn(wolf);
            // Attack can miss; movement + attack still produces two narrative sentences.
            if (narrative != null && narrative.StartsWith("The wolf advances.") && narrative.Length > "The wolf advances.".Length)
            {
                attacked = true;
                break;
            }
        }
        Assert.True(attacked);
        Assert.False(CombatScenario.CanFlee(player.Position));
    }
    [Theory]
    [InlineData(AnimalType.CaveBear, 4)]
    [InlineData(AnimalType.Bear, 5)]
    [InlineData(AnimalType.Hyena, 6)]
    [InlineData(AnimalType.Wolf, 6)]
    [InlineData(AnimalType.SaberTooth, 7)]
    public void SpeciesMovementAndDiagonalPursuitStayWithinBudget(AnimalType species, int expected)
    {
        var (world, scenario) = Create();
        var unit = scenario.Team2[0];
        unit.actor = AnimalFactory.FromType(species, world.Game.CurrentLocation, world.Map)!;
        unit.Position = new(40, 40);
        scenario.Player!.Position = new(25, 25);
        Assert.Equal(expected, CombatMovement.Allowance(unit));
        var destination = CombatMovement.Pursue(unit, scenario.Player, scenario);
        Assert.InRange(unit.Position.DistanceTo(destination), 1, expected);
        Assert.True(destination.DistanceTo(scenario.Player.Position) < unit.Position.DistanceTo(scenario.Player.Position));
    }

    [Theory]
    [InlineData(AwarenessState.Unaware)]
    [InlineData(AwarenessState.Alert)]
    public void UnawareAndAlertAnimalsCannotUsePursuitAttack(AwarenessState awareness)
    {
        var (_, scenario) = Create();
        var wolf = scenario.Team2[0];
        wolf.Awareness = awareness;
        wolf.BoldnessModifier = 10;
        wolf.Position = new(scenario.Player!.Position.X, scenario.Player.Position.Y + 4);
        Assert.Null(CombatMovement.PursuitTarget(wolf, scenario));
        string? narrative = scenario.ProcessSingleAITurn(wolf);
        Assert.DoesNotContain("!", narrative ?? "");
        Assert.False(wolf.JustDamaged);
    }

}
