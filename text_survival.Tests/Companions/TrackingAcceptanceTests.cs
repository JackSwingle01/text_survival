using text_survival.Actors;
using text_survival.Environments.Grid;

namespace text_survival.Tests.Companions;

[Trait("Suite", "CompanionAcceptance")]
public class TrackingAcceptanceTests
{
    [Fact]
    [Trait("Scenario", "F02")]
    public void MovingAnUnseenTargetWithoutNewEvidence_DoesNotChangeTheFollowersActions()
    {
        // Paired worlds differ only in an out-of-sight target location. Opposite
        // directions ensure that an omniscient next-step query would diverge immediately.
        // Combined with positive reunion tests, this rejects omniscient chasing without
        // specifying how the future tracking module represents its memories.
        var westHiddenTarget = CapturePursuit(hiddenTargetX: 1);
        var eastHiddenTarget = CapturePursuit(hiddenTargetX: 29);

        Assert.Equal(westHiddenTarget, eastHiddenTarget);
    }

    [Fact]
    [Trait("Scenario", "F02")]
    public void LostSight_FollowerInvestigatesTheLastSeenPosition()
    {
        var trace = CapturePursuit(hiddenTargetX: 29, minutes: 12);

        Assert.Contains(trace, sample => sample.Position == new GridPosition(16, 1));
    }

    [Fact]
    [Trait("Scenario", "F25")]
    public void FollowingBeyondPlayerSight_DoesNotRevealThePlayersMap()
    {
        var world = new CompanionWorld(31, 3);
        var leader = world.AddNpc("Leader", 20, 1);
        leader.CurrentAction = new NPCRest(100);
        var follower = world.AddNpc("Follower", 19, 1);
        follower.CurrentAction = new NPCRest(2);
        CompanionWorld.Follow(follower, leader);
        Assert.Equal(TileVisibility.Unexplored, world.Tile(20, 1).Visibility);

        world.Advance(4);

        Assert.Equal(TileVisibility.Unexplored, world.Tile(20, 1).Visibility);
        Assert.Equal(new GridPosition(0, 0), world.Map.CurrentPosition);
    }

    private static IReadOnlyList<PursuitSample> CapturePursuit(int hiddenTargetX, int minutes = 3)
    {
        var world = new CompanionWorld(31, 3);
        var leader = world.AddNpc("Leader", 15, 1);
        leader.CurrentAction = new NPCRest(1_000);
        var follower = world.AddNpc("Follower", 15, 1);
        world.AddForage(follower.CurrentLocation);
        follower.CurrentAction = new NPCRest(2);
        CompanionWorld.Follow(follower, leader);
        world.Advance(1);
        world.MoveActor(leader, 16, 1);
        world.Advance(1);

        // Deliberately alter hidden world truth without creating any physical evidence.
        // A perception leak would make these two otherwise identical runs diverge.
        leader.CurrentLocation = world.Tile(hiddenTargetX, 1);
        Assert.True(Math.Abs(hiddenTargetX - 15) > GameMap.GetSightRange(follower.CurrentLocation));

        var trace = new List<PursuitSample>();
        for (int i = 0; i < minutes; i++)
        {
            world.Advance(1);
            trace.Add(new PursuitSample(world.Position(follower), follower.CurrentAction?.Name, follower.CurrentNeed));
        }
        return trace;
    }

    private sealed record PursuitSample(GridPosition Position, string? Action, NeedType? Need);
}
