using text_survival.Actions;
using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Combat;
using text_survival.Environments;

namespace text_survival.Tests.Companions;

public class CompanionCombatTests
{
    [Fact]
    public void EscapeDirectionUsesLocalThreatAndAvoidsBlockedExit()
    {
        var world = new CompanionWorld(5, 3);
        var npc = world.AddNpc("Fleeing", 2, 1);
        var enemy = world.AddNpc("Enemy", 2, 1);
        var scenario = CombatScenario.Create([npc], [enemy], npc.CurrentLocation, 5,
            AwarenessState.Engaged, AwarenessState.Engaged);
        scenario.Team1[0].Position = new(10, 10);
        scenario.Team2[0].Position = new(15, 10);
        Assert.Same(world.Tile(1, 1), CompanionCombat.EscapeDestination(scenario, npc));
        world.Tile(1, 1).Terrain = text_survival.Environments.Grid.TerrainType.Mountain;
        Assert.NotSame(world.Tile(1, 1), CompanionCombat.EscapeDestination(scenario, npc));
    }

    [Fact]
    public void EscapeTravelDoesNotWaitForTheRemainingBattle()
    {
        var world = new CompanionWorld();
        var player = world.Game.player;
        var ally = world.AddNpc("Ally", 0, 0);
        var enemy = world.AddNpc("Enemy", 0, 0);
        var scenario = CombatScenario.Create([player, ally], [enemy], player.CurrentLocation, 34,
            AwarenessState.Unaware, AwarenessState.Unaware, player);
        scenario.Player!.Position = new(0, 0);
        Assert.True(scenario.ExecuteFlee(scenario.Player));
        var destination = CompanionCombat.EscapeDestination(scenario, player)!;
        int crossing = TravelProcessor.GetTraversalMinutes(player.CurrentLocation, destination, player, player.Inventory, world.Map);
        Assert.Equal(CombatResult.Fled, CombatOrchestrator.CompletePlayerExit(world.Game, scenario));
        Assert.Equal(crossing, world.Game.TotalMinutesElapsed);
        Assert.Same(destination, player.CurrentLocation);
        Assert.DoesNotContain(scenario.Units, u => u.actor == player);
        Assert.True(scenario.IsOver || world.Game.BackgroundCombats.Contains(scenario));
        Assert.InRange(scenario.ElapsedRounds, 1, crossing);
    }

    [Fact]
    public async Task PlayerEscapeResolvesTheEncounterAndUsesATimedWorldCrossing()
    {
        var world = new CompanionWorld();
        var npc = world.AddNpc("Companion", 0, 0);
        CompanionWorld.Follow(npc, world.Game.player);
        var origin = world.Game.player.CurrentLocation;
        var prey = AnimalFactory.FromType(AnimalType.Caribou, origin, world.Map)!;
        var inputs = ((text_survival.Tests.Support.ScriptedUi)world.Game.Ui).CombatInputs;
        inputs.Enqueue(new text_survival.UI.CombatInput(CombatActions.Retreat, null));
        inputs.Enqueue(new text_survival.UI.CombatInput(CombatActions.Retreat, null));
        inputs.Enqueue(new text_survival.UI.CombatInput(CombatActions.Flee, null));
        var result = await CombatOrchestrator.RunHunt(world.Game, prey);
        Assert.Equal(CombatResult.Fled, result);
        Assert.Null(world.Game.ActiveCombat);
        Assert.NotSame(origin, world.Game.player.CurrentLocation);
        Assert.True(world.Game.TotalMinutesElapsed >= 5);
    }

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
