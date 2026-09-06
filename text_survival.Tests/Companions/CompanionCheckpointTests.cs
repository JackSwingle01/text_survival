using System.Text.Json;
using text_survival.Actions;
using text_survival.Actors;
using text_survival.Combat;
using text_survival.Environments.Navigation;
using text_survival.Environments.Grid;
using text_survival.Persistence;

namespace text_survival.Tests.Companions;

public class CompanionCheckpointTests
{
    [Fact]
    public void BackgroundFightRetainsRosterOwnershipAndMoraleAfterLoad()
    {
        var world = new CompanionWorld();
        var defender = world.AddNpc("Defender");
        var enemy = world.AddNpc("Enemy");
        CompanionCombat.StartDefense(world.Game, defender, [enemy]);
        var before = Assert.Single(world.Game.BackgroundCombats);
        before.Team1[0].BoldnessModifier = -0.4;
        before.ElapsedRounds = 7;
        var loaded = RoundTrip(world.Game);
        var battle = Assert.Single(loaded.BackgroundCombats);
        Assert.Equal(7, battle.ElapsedRounds);
        Assert.Equal(-0.4, battle.Team1[0].BoldnessModifier);
        Assert.Same(loaded.NPCs[0], battle.Team1[0].actor);
        Assert.True(CompanionCombat.Owns(loaded, loaded.NPCs[0]));
        loaded.UpdateWithoutEvents(1, ActivityType.Resting);
        Assert.Equal(1, loaded.TotalMinutesElapsed);
    }

    [Fact]
    public void PendingRequestAndCooldownSurviveLoad()
    {
        var world = new CompanionWorld();
        var leader = world.AddNpc("Leader");
        var follower = world.AddNpc("Follower");
        CompanionWorld.Follow(follower, leader);
        follower.Social.NextNeedRequestMinute = 60;
        follower.Social.PendingNeed = new CompanionNeedRequest { Recipient = leader, Need = NeedType.Water, ExpiresAtMinute = 10 };
        var loaded = RoundTrip(world.Game);
        var restored = loaded.NPCs[1];
        Assert.Same(loaded.NPCs[0], restored.Social.PendingNeed!.Recipient);
        Assert.Equal(60, restored.Social.NextNeedRequestMinute);
        CompanionInteractions.Reply(restored, loaded.NPCs[0], NeedReply.LetGo, 1);
        Assert.Null(restored.Social.PendingNeed);
    }

    [Fact]
    public void VisibleButUnreachableTargetDoesNotCauseEndlessRouteRetries()
    {
        var world = new CompanionWorld(3, 1);
        var leader = world.AddNpc("Leader", 2, 0);
        var follower = world.AddNpc("Follower", 0, 0);
        world.Tile(1, 0).Terrain = TerrainType.Mountain;
        follower.Following = new FollowIntent(leader) { LeadPosition = new(2, 0), LastSeenPosition = new(2, 0) };
        Assert.Equal(PathStatus.NoRoute, Following.Pursue(follower, 0)!.Status);
        Following.Pursue(follower, 5);
        Following.Pursue(follower, 10);
        Assert.Null(follower.Following);
    }

    [Fact]
    public void NonHumanFollowerUsesTheSameEvidenceAndNavigationContract()
    {
        var world = new CompanionWorld();
        var animal = world.AddLeader(LeaderKind.Animal);
        var leader = world.AddNpc("Leader");
        Assert.True(Following.TryBegin(animal, leader));
        world.MoveActor(leader, 2, 1);
        Following.Observe(animal, 0);
        Assert.Equal(PathStatus.Found, Following.Pursue(animal, 1)!.Status);
    }

    [Fact]
    public void NavigationUsesTheConfiguredReplacementAlgorithm()
    {
        var world = new CompanionWorld();
        var replacement = new RecordingPathfinder();
        world.Map.Pathfinder = replacement;
        Navigation.FindRoute(world.Map, new(1, 1), new(4, 1));
        Assert.True(replacement.Called);
    }

    private sealed class RecordingPathfinder : IPathfinder
    {
        public bool Called;
        public PathResult FindPath(PathRequest request, NavigationView view)
        {
            Called = true;
            return new DijkstraPathfinder().FindPath(request, view);
        }
    }

    private static GameContext RoundTrip(GameContext ctx) => JsonSerializer.Deserialize<GameContext>(
        JsonSerializer.Serialize(ctx, SaveManager.Options), SaveManager.Options)!;
}
