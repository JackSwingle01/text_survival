using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Combat;

namespace text_survival.Tests.Companions;

public class CompanionCombatTests
{
    [Fact]
    public void CompanionCanHelpHuntWithoutConsideringPreyHostile()
    {
        var world = new CompanionWorld();
        var leader = world.AddNpc("Hunter");
        var follower = world.AddNpc("Companion");
        CompanionWorld.Follow(follower, leader);
        var prey = AnimalFactory.FromType(AnimalType.Caribou, leader.CurrentLocation, world.Map)!;
        Assert.False(follower.IsHostileTo(prey));
        Assert.True(CompanionCombat.WillAssist(follower, leader, prey, EncounterPurpose.Hunt));
    }

    [Fact]
    public void ArrivingCompanionJoinsExistingRosterOnlyOnce()
    {
        var world = new CompanionWorld();
        var leader = world.AddNpc("Hunter");
        var follower = world.AddNpc("Companion", 2, 1);
        CompanionWorld.Follow(follower, leader);
        var prey = AnimalFactory.FromType(AnimalType.Caribou, leader.CurrentLocation, world.Map)!;
        var battle = CombatScenario.Create([leader], [prey], leader.CurrentLocation, 20, AwarenessState.Engaged, AwarenessState.Engaged);
        battle.Purpose = EncounterPurpose.Hunt;
        world.Game.BackgroundCombats.Add(battle);
        CompanionCombat.JoinArrivals(world.Game);
        Assert.Single(battle.Team1);
        world.MoveActor(follower, 1, 1);
        follower.CurrentAction = new NPCRest(20);
        CompanionCombat.JoinArrivals(world.Game);
        CompanionCombat.JoinArrivals(world.Game);
        Assert.Equal(2, battle.Team1.Count);
        Assert.Single(battle.Team1, u => u.actor == follower);
        Assert.Null(follower.CurrentAction);
        Assert.True(CompanionCombat.Owns(world.Game, follower));
    }

    [Fact]
    public void RepeatedRetreatCallsCannotStackMoralePenalties()
    {
        var world = new CompanionWorld();
        var leader = world.AddNpc("Leader");
        var follower = world.AddNpc("Companion");
        var prey = AnimalFactory.FromType(AnimalType.Caribou, leader.CurrentLocation, world.Map)!;
        var battle = CombatScenario.Create([leader, follower], [prey], leader.CurrentLocation, 20, AwarenessState.Engaged, AwarenessState.Engaged);
        Assert.True(CompanionCombat.Signal(battle, leader, true, 0));
        var unit = battle.Team1.Single(u => u.actor == follower);
        double morale = unit.BoldnessModifier;
        Assert.False(CompanionCombat.Signal(battle, leader, true, 1));
        Assert.True(CompanionCombat.Signal(battle, leader, true, 5));
        Assert.Equal(morale, unit.BoldnessModifier);
    }

    [Fact]
    public void NpcDefenseIsScheduledWithoutAdvancingWorldRecursively()
    {
        var world = new CompanionWorld();
        var defender = world.AddNpc("Defender");
        var enemy = world.AddNpc("Enemy");
        CompanionCombat.StartDefense(world.Game, defender, [enemy]);
        Assert.Equal(0, world.Game.TotalMinutesElapsed);
        Assert.Single(world.Game.BackgroundCombats);
        Assert.True(CompanionCombat.Owns(world.Game, defender));
        world.Advance(1);
        Assert.Equal(1, world.Game.TotalMinutesElapsed);
    }
}
