using System.Text.Json;
using text_survival.Actors;
using text_survival.Environments.Grid;
using text_survival.Persistence;

namespace text_survival.Tests.Companions;

public class PursuitContractTests
{
    [Fact]
    public void FollowingRejectsCyclesWithoutChangingExistingAgreement()
    {
        var world = new CompanionWorld();
        var a = world.AddNpc("A");
        var b = world.AddNpc("B");
        var c = world.AddNpc("C");
        Assert.True(Following.TryBegin(a, b));
        Assert.True(Following.TryBegin(b, c));
        Assert.False(Following.TryBegin(c, a));
        Assert.Null(c.Following);
        Assert.Same(b, a.Following!.Target);
    }

    [Fact]
    public void LocalCrossingSupportsATurnButErodedEvidenceDoesNot()
    {
        var world = new CompanionWorld(31, 3);
        var leader = world.AddNpc("Leader", 15, 1);
        var follower = world.AddNpc("Follower", 15, 1);
        Assert.True(Following.TryBegin(follower, leader));
        Following.Observe(follower, 0);
        world.MoveActor(leader, 15, 2);
        leader.CurrentLocation = world.Tile(30, 2);
        var route = Following.Pursue(follower, 1);
        Assert.Equal(new GridPosition(15, 2), Assert.Single(route!.Steps));
        follower.Following!.LeadPosition = new(15, 1);
        follower.Following.LastPassage = 0;
        follower.Following.Investigated.Clear();
        world.Map.Tracks.Erosion += TrackRegistry.BaseLifespanUnits;
        Assert.Empty(Following.Pursue(follower, 2)!.Steps);
    }

    [Fact]
    public void HiddenAbsenceDoesNotImmediatelyEndFollowingButSearchEventuallyExpires()
    {
        var world = new CompanionWorld(31, 3);
        var leader = world.AddNpc("Leader", 15, 1);
        var follower = world.AddNpc("Follower", 15, 1);
        Following.TryBegin(follower, leader);
        Following.Observe(follower, 0);
        leader.CurrentLocation = world.Tile(30, 1);
        Following.Observe(follower, 1);
        Assert.NotNull(follower.Following);
        for (int i = 0; i < Following.SearchMinutes; i++) Following.SpendSearchMinute(follower);
        Following.Pursue(follower, Following.SearchMinutes);
        Assert.Null(follower.Following);
    }

    [Fact]
    public void EvidenceAndPhysicalPassagesSurviveSerialization()
    {
        var world = new CompanionWorld();
        var leader = world.AddNpc("Leader");
        var follower = world.AddNpc("Follower");
        Following.TryBegin(follower, leader);
        world.MoveActor(leader, 2, 1);
        Following.Observe(follower, 7);
        var loaded = JsonSerializer.Deserialize<text_survival.Actions.GameContext>(
            JsonSerializer.Serialize(world.Game, SaveManager.Options), SaveManager.Options)!;
        var intent = Assert.Single(loaded.NPCs, n => n.Following != null).Following!;
        Assert.Equal(new GridPosition(2, 1), intent.LastSeenPosition);
        Assert.Equal(7, intent.LastEvidenceMinute);
        Assert.Single(loaded.Map!.Tracks.ReadPassages(new(1, 1)));
    }
}
