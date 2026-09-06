using text_survival.Actors;
using text_survival.Environments.Grid;

namespace text_survival.Tests.Companions;

[Trait("Suite", "CompanionAcceptance")]
public class FollowingAcceptanceTests
{
    [Fact]
    [Trait("Scenario", "F01")]
    public void Departure_DoesNotCancelCurrentForaging()
    {
        var world = new CompanionWorld();
        var leader = world.AddLeader(LeaderKind.Npc);
        var follower = world.AddNpc("Follower");
        CompanionWorld.Follow(follower, leader);
        var origin = follower.CurrentLocation;
        var forage = world.AddForage(origin);
        var work = new NPCForage(10);
        follower.CurrentAction = work;

        world.Advance(1); // A chance to observe the leader before departure.
        world.MoveActor(leader, 3, 1);
        world.Advance(3);

        Assert.Same(work, follower.CurrentAction);
        Assert.Equal(4, work.MinutesSpent);
        Assert.Same(origin, follower.CurrentLocation);
        Assert.Equal(0, forage.NumberOfHoursForaged);
        Assert.Same(leader, follower.Following!.Target);
    }

    [Theory]
    [InlineData(LeaderKind.Npc)]
    [InlineData(LeaderKind.Player)]
    [InlineData(LeaderKind.Animal)]
    [Trait("Scenario", "F01")]
    [Trait("Scenario", "F36")]
    public void CompletedForage_IsFollowedByCatchingUpToTheAgreedActor(LeaderKind kind)
    {
        var world = new CompanionWorld();
        var leader = world.AddLeader(kind);
        var follower = world.AddNpc("Follower");
        CompanionWorld.Follow(follower, leader);
        var forage = world.AddForage(follower.CurrentLocation);
        follower.CurrentAction = new NPCForage(4);

        world.Advance(1);
        world.MoveActor(leader, 3, 1);
        world.Advance(3);
        Assert.Equal(4.0 / 60, forage.NumberOfHoursForaged, 6);

        world.AdvanceUntil(() => follower.CurrentLocation == leader.CurrentLocation, 20);

        Assert.Equal(4.0 / 60, forage.NumberOfHoursForaged, 6);
        Assert.Same(leader, follower.Following!.Target);
    }

    [Fact]
    [Trait("Scenario", "F05")]
    public void CatchingUp_TakesPriorityOverStartingAnotherOptionalForage()
    {
        var world = new CompanionWorld();
        var leader = world.AddLeader(LeaderKind.Npc);
        var follower = world.AddNpc("Follower");
        CompanionWorld.Follow(follower, leader);
        var forage = world.AddForage(follower.CurrentLocation);
        follower.CurrentAction = new NPCForage(2);

        world.Advance(1);
        world.MoveActor(leader, 2, 1);
        world.Advance(1);
        world.AdvanceUntil(() => follower.CurrentLocation == leader.CurrentLocation, 15);

        Assert.Equal(2.0 / 60, forage.NumberOfHoursForaged, 6);
    }

    [Fact]
    [Trait("Scenario", "F07")]
    public void Thirst_WithCarriedWater_IsHandledAutonomously()
    {
        var world = new CompanionWorld();
        var leader = world.AddLeader(LeaderKind.Npc);
        var follower = world.AddNpc("Follower");
        CompanionWorld.Follow(follower, leader);
        follower.Body.Hydration = SurvivalProcessor.MAX_HYDRATION * 0.4;
        follower.Inventory.Add(Resource.Water, 1);
        double initialHydration = follower.Body.Hydration;

        world.Advance(3);

        Assert.True(follower.Body.Hydration > initialHydration);
        Assert.True(follower.Inventory.Weight(Resource.Water) < 1);
        Assert.Same(leader, follower.Following!.Target);
        Assert.Same(leader.CurrentLocation, follower.CurrentLocation);
    }

    [Fact]
    [Trait("Scenario", "F10")]
    public void CriticalThirst_CanInterruptWorkWithoutEndingFollowing()
    {
        var world = new CompanionWorld();
        var leader = world.AddLeader(LeaderKind.Npc);
        var follower = world.AddNpc("Follower");
        CompanionWorld.Follow(follower, leader);
        world.AddForage(follower.CurrentLocation);
        follower.CurrentAction = new NPCForage(30);
        follower.Body.Hydration = SurvivalProcessor.MAX_HYDRATION * 0.15;
        follower.Inventory.Add(Resource.Water, 1);
        double initialHydration = follower.Body.Hydration;

        world.Advance(3);

        Assert.True(follower.Body.Hydration > initialHydration);
        Assert.True(follower.Inventory.Weight(Resource.Water) < 1);
        Assert.Same(leader, follower.Following!.Target);
    }

    [Fact]
    [Trait("Scenario", "F01")]
    public void NpcCrossing_TakesTimeAndLeavesOnePassageWithoutMovingThePlayer()
    {
        var world = new CompanionWorld();
        var npc = world.AddNpc("Walker");
        var from = world.Position(npc);
        var to = new GridPosition(2, 1);
        var playerPosition = world.Map.CurrentPosition;
        var move = new NPCMove(world.Tile(to.X, to.Y), npc);
        npc.CurrentAction = move;

        world.Advance(move.DurationMinutes - 1);
        Assert.Equal(from, world.Position(npc));
        Assert.Empty(world.Map.Tracks.At(from));

        world.Advance(1);

        Assert.Equal(to, world.Position(npc));
        Assert.Equal(playerPosition, world.Map.CurrentPosition);
        Assert.Equal(playerPosition, world.Position(world.Game.player));
        Assert.Equal(1, world.Map.Tracks.TrafficOf(from, TrackMaker.Human));
        Assert.Equal(1, world.Map.Tracks.TrafficOf(to, TrackMaker.Human));
    }
}
