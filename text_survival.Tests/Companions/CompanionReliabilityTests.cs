using text_survival.Actors;
using text_survival.Actions;
using text_survival.Environments;

namespace text_survival.Tests.Companions;

public class CompanionReliabilityTests
{
    [Fact]
    public void ColdAtAnUnlitCampDoesNotTryToTravelToTheSameTile()
    {
        var world = new CompanionWorld();
        var npc = world.AddNpc("Cold", 0, 0);
        npc.Body.BodyTemperature = Body.BASE_BODY_TEMP - 5;
        npc.CurrentNeed = NeedType.Warmth;
        world.Advance(1);
        Assert.NotNull(npc.CurrentAction);
    }

    [Fact]
    public void SearchProgressAndPursuitActionSurviveCheckpoint()
    {
        var world = new CompanionWorld();
        var leader = world.AddLeader(LeaderKind.Npc);
        var npc = world.AddNpc("Follower");
        CompanionWorld.Follow(npc, leader);
        npc.Following!.SearchEffortMinutes = 42;
        npc.CurrentAction = new NPCRest(2) { IsFollowingPursuit = true };
        var loaded = System.Text.Json.JsonSerializer.Deserialize<GameContext>(
            System.Text.Json.JsonSerializer.Serialize(world.Game, text_survival.Persistence.SaveManager.Options),
            text_survival.Persistence.SaveManager.Options)!;
        Assert.Equal(42, loaded.NPCs[1].Following!.SearchEffortMinutes);
        Assert.True(loaded.NPCs[1].CurrentAction!.IsFollowingPursuit);
    }

    [Fact]
    public void LongSelfCareDoesNotSpendSearchEffortButEvidenceEventuallyExpires()
    {
        var world = new CompanionWorld(31, 3);
        var leader = world.AddNpc("Leader");
        var npc = world.AddNpc("Follower");
        CompanionWorld.Follow(npc, leader);
        Following.Observe(npc, 0);
        leader.CurrentLocation = world.Tile(30, 1);
        Following.Pursue(npc, 120);
        Assert.NotNull(npc.Following);
        Assert.Equal(0, npc.Following.SearchEffortMinutes);
        Following.Pursue(npc, Following.StaleEvidenceMinutes);
        Assert.Null(npc.Following);
        Assert.Equal("Search exhausted", npc.FollowEndReason);
    }

    [Fact]
    public void SearchAtAnEmptyLeadTerminatesThroughRealTicks()
    {
        var world = new CompanionWorld(31, 3);
        var leader = world.AddLeader(LeaderKind.Npc);
        var npc = world.AddNpc("Follower");
        CompanionWorld.Follow(npc, leader);
        Following.Observe(npc, 0);
        leader.CurrentLocation = world.Tile(30, 1);
        for (int i = 0; i < Following.SearchMinutes + 3 && npc.Following != null; i++)
        {
            CompanionWorld.SetComfortable(npc); // Isolate search effort from cold survival.
            world.Advance(1);
        }
        Assert.Null(npc.Following);
        Assert.Equal("Search exhausted", npc.FollowEndReason);
    }

    [Fact]
    public void MissingLeaderAtLastSighting_DoesNotStartOptionalForage()
    {
        var world = new CompanionWorld(31, 3);
        var leader = world.AddNpc("Leader");
        var npc = world.AddNpc("Follower");
        CompanionWorld.Follow(npc, leader);
        Following.Observe(npc, 0);
        leader.CurrentLocation = world.Tile(30, 1); // No new evidence.
        world.AddForage(npc.CurrentLocation);
        world.Advance(1);
        Assert.Equal(CompanionDecisionReason.Pursuit, npc.DecisionReason);
        Assert.IsNotType<NPCForage>(npc.CurrentAction);
    }

    [Fact]
    public void EmergencyDuringPendingRequest_DoesNotWaitForTheAnswer()
    {
        var world = new CompanionWorld();
        var leader = world.AddNpc("Leader");
        var npc = world.AddNpc("Follower");
        CompanionWorld.Follow(npc, leader);
        npc.CurrentNeed = NeedType.Water;
        npc.Social.PendingNeed = new() { Recipient = leader, Need = NeedType.Water, ExpiresAtMinute = 10 };
        npc.Social.NextNeedRequestMinute = 60;
        npc.Body.Hydration = SurvivalProcessor.MAX_HYDRATION * 0.1;
        var move = new NPCMove(world.Tile(2, 1), npc);
        Assert.Same(move, CompanionInteractions.ConsiderNeed(npc, move, 1));
        Assert.Null(npc.Social.PendingNeed);
    }

    [Theory]
    [InlineData(LeaderKind.Npc, 1)]
    [InlineData(LeaderKind.Player, 1)]
    [InlineData(LeaderKind.Npc, 4)]
    [InlineData(LeaderKind.Player, 4)]
    public void TimedLeaderTravel_ReunitesAfterCommittedWork(LeaderKind kind, int count)
    {
        var world = new CompanionWorld();
        var leader = world.AddLeader(kind);
        var followers = Enumerable.Range(0, count).Select(i => world.AddNpc($"Follower {i}")).ToList();
        world.AddForage(leader.CurrentLocation);
        foreach (var npc in followers)
        {
            CompanionWorld.Follow(npc, leader);
            npc.CurrentAction = new NPCForage(10);
        }
        world.Advance(1);
        for (int x = 2; x <= 4; x++)
        {
            var destination = world.Tile(x, 1);
            int duration = TravelProcessor.GetTraversalMinutes(leader.CurrentLocation, destination, leader, leader.Inventory, world.Map);
            if (leader is NPC npc)
            {
                npc.CurrentAction = new NPCMove(destination, npc);
                world.Advance(duration);
                npc.CurrentAction = new NPCRest(1000);
            }
            else
            {
                for (int m = 0; m < duration; m++) world.Game.UpdateWithoutEvents(1, ActivityType.Traveling);
                world.Map.MoveTo(destination, leader);
            }
            Assert.Same(destination, leader.CurrentLocation);
        }
        int bound = followers.Max(n => Math.Max(0, (n.CurrentAction?.DurationMinutes ?? 0) - (n.CurrentAction?.MinutesSpent ?? 0)) +
            (int)text_survival.Environments.Navigation.Navigation.FindRoute(world.Map, world.Position(n), world.Position(leader), n).CostMinutes + 4);
        world.AdvanceUntil(() => followers.All(n => n.CurrentLocation == leader.CurrentLocation), bound);
        Assert.All(followers, n => Assert.Same(leader, n.Following!.Target));
    }

    [Fact]
    public void DiagnosticSampling_DoesNotChangeDecisionsOrRandomness()
    {
        static string Run(bool capture)
        {
            var world = new CompanionWorld();
            var leader = world.AddLeader(LeaderKind.Npc);
            var npc = world.AddNpc("Follower");
            CompanionWorld.Follow(npc, leader);
            world.AddForage(npc.CurrentLocation);
            var diagnostics = new CompanionDiagnostics();
            world.Advance(1);
            world.MoveActor(leader, 4, 1);
            for (int i = 0; i < 30; i++)
            {
                world.Advance(1);
                if (capture) diagnostics.Sample(npc, world.Game.TotalMinutesElapsed, true);
            }
            return $"{world.Position(npc)}|{npc.Body.Hydration:R}|{npc.Body.Energy:R}|{npc.Body.CalorieStore:R}|{npc.Inventory.CurrentWeightKg:R}|{npc.CurrentAction?.Name}|{npc.CurrentAction?.MinutesSpent}|{npc.Following?.LeadPosition}|{text_survival.Utils.Rng.Next()}";
        }
        Assert.Equal(Run(false), Run(true));
    }
}
