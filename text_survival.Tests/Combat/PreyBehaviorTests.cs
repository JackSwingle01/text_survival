using text_survival.Actions;
using text_survival.Actors.Animals;
using text_survival.Actors.Player;
using text_survival.Combat;
using text_survival.Environments;
using text_survival.Environments.Grid;

namespace text_survival.Tests.Combat;

/// <summary>
/// Hunting playtest regressions: prey that charged, blocked, outran you on three legs,
/// and noticed every stalk.
/// </summary>
public class PreyBehaviorTests
{
    private static (GameContext ctx, Location field) World()
    {
        var weather = new Weather(-10, GameContext.StartTime);
        var field = new Location("Field", "[field]", weather, 5);
        var map = new GameMap(1, 1) { Weather = weather };
        map.SetLocation(0, 0, field);
        map.CurrentPosition = new GridPosition(0, 0);
        var player = new Player { CurrentLocation = field, Map = map };
        return (new GameContext(player, field, weather) { Map = map }, field);
    }

    private static Animal Make(GameContext ctx, Location field, AnimalType type) =>
        AnimalFactory.FromType(type, field, ctx.Map!)!;

    private static void Cripple(Animal animal)
    {
        foreach (var leg in animal.Body.Parts.Where(p => p.Name.Contains("Leg")))
        {
            leg.Muscle.Condition = 0;
            leg.Bone.Condition = 0.2;
        }
    }

    [Theory]
    [InlineData(AnimalType.Rabbit)]
    [InlineData(AnimalType.Ptarmigan)]
    [InlineData(AnimalType.Fox)]
    public void SpottedSmallGameRunsInsteadOfFighting(AnimalType type)
    {
        var (ctx, field) = World();
        var scenario = CombatScenario.Create([ctx.player], [Make(ctx, field, type)], field, 4,
            AwarenessState.Engaged, AwarenessState.Engaged, ctx.player);
        var prey = scenario.Team2[0];
        double vitality = ctx.player.Vitality;
        double lastDistance = prey.Position.DistanceTo(scenario.Player!.Position);

        for (int turn = 0; turn < 30 && scenario.Units.Contains(prey); turn++)
        {
            scenario.ProcessSingleAITurn(prey);
            if (!scenario.Units.Contains(prey)) break;
            double distance = prey.Position.DistanceTo(scenario.Player.Position);
            Assert.True(distance >= lastDistance, $"{type} closed from {lastDistance:F1}m to {distance:F1}m");
            lastDistance = distance;
        }

        Assert.DoesNotContain(prey, scenario.Units); // off the map edge
        Assert.Equal(vitality, ctx.player.Vitality);
    }

    [Fact]
    public void AnimalsNeverBlock()
    {
        var (ctx, field) = World();
        var scenario = CombatScenario.Create([ctx.player], [Make(ctx, field, AnimalType.Wolf)], field, 1,
            AwarenessState.Engaged, AwarenessState.Engaged, ctx.player);
        var wolf = scenario.Team2[0];
        wolf.BoldnessModifier = -10; // won't attack, so it picks a defensive action

        for (int i = 0; i < 200; i++)
            Assert.NotEqual(CombatActions.Block, CombatAI.DetermineAction(wolf, scenario));
    }

    [Fact]
    public void CrippledLegsCutMovement()
    {
        var (ctx, field) = World();
        var healthy = new Unit(Make(ctx, field, AnimalType.Fox), new GridPosition(0, 0));
        var fox = Make(ctx, field, AnimalType.Fox);
        Cripple(fox);
        var crippled = new Unit(fox, new GridPosition(0, 0));

        Assert.Equal(3, CombatMovement.Allowance(healthy));
        Assert.True(CombatMovement.Allowance(crippled) < CombatMovement.Allowance(healthy));
        Assert.True(CombatMovement.Allowance(crippled) >= 1);
    }

    /// <summary>Fraction of stalks from 34m to 8m that end with the prey not yet engaged.</summary>
    private static double StalkSuccess(double detectionFactor)
    {
        var (ctx, field) = World();
        int successes = 0, trials = 2000;
        for (int t = 0; t < trials; t++)
        {
            var scenario = CombatScenario.Create([ctx.player], [Make(ctx, field, AnimalType.Rabbit)], field, 34,
                AwarenessState.Engaged, AwarenessState.Unaware, ctx.player);
            var prey = scenario.Team2[0];
            prey.DetectionFactor = detectionFactor;
            var hunter = scenario.Player!;
            while (hunter.Position.DistanceTo(prey.Position) > 8 && prey.Awareness != AwarenessState.Engaged)
            {
                hunter.Position = hunter.Position.MoveToward(prey.Position, 3);
                scenario.RunDetectionChecks(hunter, 0, CombatFormulas.StalkStepMeters);
            }
            if (prey.Awareness != AwarenessState.Engaged) successes++;
        }
        return successes / (double)trials;
    }

    [Fact]
    public void StalkingAFeedingAnimalUsuallyWorksAndAnAlertOneRarelyDoes()
    {
        double feeding = StalkSuccess(0.7);
        double alert = StalkSuccess(2.0);

        Assert.InRange(feeding, 0.5, 0.9);
        Assert.InRange(alert, 0.0, 0.25);
        Assert.True(feeding > alert + 0.3);
    }

    [Fact]
    public void StandingStillIsNeverNoticed()
    {
        var (ctx, field) = World();
        var scenario = CombatScenario.Create([ctx.player], [Make(ctx, field, AnimalType.Rabbit)], field, 5,
            AwarenessState.Engaged, AwarenessState.Unaware, ctx.player);
        for (int i = 0; i < 200; i++)
            scenario.RunDetectionChecks(scenario.Player!, 0, exposureMeters: 0);
        Assert.Equal(AwarenessState.Unaware, scenario.Team2[0].Awareness);
    }

    [Fact]
    public void HitVerbsMatchHowHardTheHitLanded()
    {
        for (int i = 0; i < 100; i++)
        {
            Assert.Contains(CombatNarrator.GetAttackVerb(text_survival.Bodies.DamageType.Blunt, 0.01), new[] { "grazes", "clips" });
            Assert.Contains(CombatNarrator.GetAttackVerb(text_survival.Bodies.DamageType.Blunt, 0.5), new[] { "smashes", "cracks" });
        }
    }
}
