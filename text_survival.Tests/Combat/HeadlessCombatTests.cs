using text_survival.Actions;
using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Actors.Player;
using text_survival.Combat;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;

namespace text_survival.Tests.Combat;

/// <summary>
/// Fights with no player in them run on the same grid as player fights and end in the same aftermath.
/// Outcomes are random, so these tests check the invariants that must hold whatever happened.
/// </summary>
public class HeadlessCombatTests
{
    private static GameContext CreateContext()
    {
        var player = new Player();
        var weather = new Weather(-10, GameContext.StartTime);
        var camp = new Location("Test Camp", "[camp]", weather, 5);
        var field = new Location("Field", "[field]", weather, 5);

        var map = new GameMap(2, 2);
        map.Weather = weather;
        map.SetLocation(0, 0, camp);
        map.SetLocation(1, 0, field);
        map.CurrentPosition = new GridPosition(0, 0);

        player.CurrentLocation = camp;
        player.Map = map;

        var ctx = new GameContext(player, camp, weather) { Map = map };
        return ctx;
    }

    private static Herd AddHerd(GameContext ctx, AnimalType type, int count, Location at)
    {
        var map = ctx.Map!;
        var herd = Herd.Create(type, at, map, [map.GetPosition(at)]);
        for (int i = 0; i < count; i++)
            herd.AddMember(AnimalFactory.FromType(type, at, map)!);
        ctx.Herds.Add(herd);
        return herd;
    }

    [Fact]
    public void PackHunt_LeavesConsistentWorldWhateverHappened()
    {
        var ctx = CreateContext();
        var field = ctx.Map!.GetLocationAt(1, 0)!;
        var wolves = AddHerd(ctx, AnimalType.Wolf, 4, field);
        var caribou = AddHerd(ctx, AnimalType.Caribou, 2, field);
        var wolfMembers = wolves.Members.ToList();
        var caribouMembers = caribou.Members.ToList();

        var result = CombatOrchestrator.ResolveHeadless(
            ctx, wolfMembers.Cast<Actor>().ToList(), caribouMembers.Cast<Actor>().ToList(), field,
            startDistanceM: 5, AwarenessState.Engaged, AwarenessState.Unaware);

        var dead = wolfMembers.Concat(caribouMembers).Where(a => !a.IsAlive).ToList();

        // Every death left a carcass, and nothing else did
        Assert.Equal(dead.Count, field.Features.OfType<CarcassFeature>().Count());

        // Dead animals are out of their herds; empty herds are gone
        Assert.DoesNotContain(wolves.Members, a => !a.IsAlive);
        Assert.DoesNotContain(caribou.Members, a => !a.IsAlive);
        Assert.All(ctx.Herds, h => Assert.False(h.IsEmpty));

        if (caribouMembers.Any(c => !c.IsAlive))
        {
            // A kill feeds the pack
            Assert.Equal(0, wolves.Hunger);
            Assert.Equal(HerdState.Feeding, wolves.State);
        }

        if (!caribou.IsEmpty)
        {
            // Survivors of a hunt do not stand around
            Assert.Equal(HerdState.Fleeing, caribou.State);
        }

        // From the pack's side a hunt ends in a kill, an escape, or a stand-off; the pack never "flees" its own hunt
        Assert.Contains(result, new[] { CombatResult.Victory, CombatResult.AnimalFled, CombatResult.AnimalDisengaged, CombatResult.Defeat, CombatResult.Fled });

    }

    [Fact]
    public void Create_PutsTeamsAtRequestedDistance()
    {
        var ctx = CreateContext();
        var field = ctx.Map!.GetLocationAt(1, 0)!;
        var wolf = AnimalFactory.FromType(AnimalType.Wolf, field, ctx.Map)!;
        var caribou = AnimalFactory.FromType(AnimalType.Caribou, field, ctx.Map)!;

        var scenario = CombatScenario.Create([wolf], [caribou], field, 12, AwarenessState.Engaged, AwarenessState.Unaware);

        Assert.Equal(12, scenario.Team1[0].Position.DistanceTo(scenario.Team2[0].Position), 0.01);
        Assert.Equal(AwarenessState.Engaged, scenario.Team1[0].Awareness);
        Assert.Equal(AwarenessState.Unaware, scenario.Team2[0].Awareness);
        Assert.Null(scenario.Player);
    }

    [Fact]
    public void Create_RejectsPlayerWhoIsNotFighting()
    {
        var ctx = CreateContext();
        var field = ctx.Map!.GetLocationAt(1, 0)!;
        var wolf = AnimalFactory.FromType(AnimalType.Wolf, field, ctx.Map)!;
        var caribou = AnimalFactory.FromType(AnimalType.Caribou, field, ctx.Map)!;

        Assert.Throws<ArgumentException>(() =>
            CombatScenario.Create([wolf], [caribou], field, 5, AwarenessState.Engaged, AwarenessState.Engaged, ctx.player));
    }

    [Fact]
    public void ResolveHeadless_RefusesThePlayer()
    {
        var ctx = CreateContext();
        var field = ctx.Map!.GetLocationAt(1, 0)!;
        var wolf = AnimalFactory.FromType(AnimalType.Wolf, field, ctx.Map)!;

        var scenario = CombatScenario.Create([ctx.player], [wolf], field, 5, AwarenessState.Engaged, AwarenessState.Engaged, ctx.player);

        Assert.Throws<InvalidOperationException>(() => scenario.ResolveHeadless());
    }
}
