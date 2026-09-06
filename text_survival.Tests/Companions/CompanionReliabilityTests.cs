using text_survival.Actors;
using text_survival.Actions;
using text_survival.Environments;

namespace text_survival.Tests.Companions;

public class CompanionReliabilityTests
{
    [Fact]
    public void WaterDetourUsesCampSuppliesThenReunitesWithMovingTarget()
    {
        var world = new CompanionWorld();
        var leader = world.AddLeader(LeaderKind.Npc);
        var npc = world.AddNpc("Thirsty");
        CompanionWorld.Follow(npc, leader);
        npc.Body.Hydration = SurvivalProcessor.MAX_HYDRATION * 0.4;
        var fire = new text_survival.Environments.Features.HeatSourceFeature();
        Assert.True(fire.AddFuel(5, text_survival.Items.FuelType.Tinder));
        fire.IgniteAll();
        Assert.True(fire.IsActive);
        world.Camp.AddFeature(fire);
        var cache = world.Camp.GetFeature<text_survival.Environments.Features.CacheFeature>()!.Storage;
        cache.Add(Resource.Water, 2);
        world.Advance(1); // NPC recipient permits the detour through the ordinary request adapter.
        world.MoveActor(leader, 3, 1);
        bool visitedCamp = false, reunited = false;
        for (int minute = 0; minute < 100 && !reunited; minute++)
        {
            // Isolate hydration and movement from independent cold-weather experiments.
            npc.Body.BodyTemperature = Body.BASE_BODY_TEMP;
            leader.Body.BodyTemperature = Body.BASE_BODY_TEMP;
            world.Advance(1);
            visitedCamp |= npc.CurrentLocation == world.Camp;
            reunited = visitedCamp && npc.CurrentLocation == leader.CurrentLocation;
        }
        Assert.True(reunited, $"visited={visitedCamp}, at={world.Position(npc)}, target={world.Position(leader)}, need={npc.CurrentNeed}, action={npc.CurrentAction?.Name}, hydration={npc.Body.HydratedPct}, following={npc.Following?.Target.Name}, last={npc.Following?.LastSeenPosition}");
        Assert.True(npc.Body.HydratedPct > 0.85);
        Assert.True(cache.Weight(Resource.Water) < 2);
        Assert.Same(leader, npc.Following!.Target);
    }

    [Fact]
    public void VoluntaryRecruitmentLaterInTheGameStartsWithFreshEvidence()
    {
        var world = new CompanionWorld();
        world.Game.GameTime = GameContext.StartTime.AddHours(12);
        var leader = world.AddLeader(LeaderKind.Npc);
        var npc = world.AddNpc("Sociable");
        npc.Relationships.AddMemory(MemoryType.SavedMe, leader);
        npc.Relationships.AddMemory(MemoryType.FoughtTogether, leader);
        world.Advance(1);
        Assert.Same(leader, npc.Following!.Target);
        Assert.Equal(720, npc.Following.LastEvidenceMinute);
        Assert.Equal(world.Position(leader), npc.Following.LastSeenPosition);
    }

    [Fact]
    public void ExhaustedCriticalWarmthOptionsDoNotFallThroughToPursuit()
    {
        var world = new CompanionWorld();
        var leader = world.AddNpc("Leader", 2, 1);
        leader.CurrentAction = new NPCRest(100);
        var npc = world.AddNpc("Freezing");
        npc.Camp = npc.CurrentLocation;
        CompanionWorld.Follow(npc, leader);
        npc.Body.BodyTemperature = SurvivalProcessor.HypothermiaThreshold + 0.1;
        world.Advance(1);
        Assert.IsNotType<NPCMove>(npc.CurrentAction);
        Assert.Equal(CompanionDecisionReason.BlockedNeed, npc.DecisionReason);
    }

    [Fact]
    public void NewSightingAfterDoubleBackReplacesOldTravelLead()
    {
        var world = new CompanionWorld();
        var leader = world.AddLeader(LeaderKind.Npc);
        var npc = world.AddNpc("Follower");
        CompanionWorld.Follow(npc, leader);
        world.Advance(1);
        world.MoveActor(leader, 3, 1);
        world.Advance(1);
        world.MoveActor(leader, 0, 1);
        world.Advance(1);
        Assert.Equal(world.Position(leader), npc.Following!.LastSeenPosition);
        world.AdvanceUntil(() => npc.CurrentLocation == leader.CurrentLocation, 25);
    }

    [Fact]
    public void FullPackDoesNotCauseAnUnrelatedCampReturnWhileCatchingUp()
    {
        var world = new CompanionWorld();
        var leader = world.AddLeader(LeaderKind.Npc);
        var npc = world.AddNpc("Follower");
        npc.Inventory.Add(Resource.Stick, npc.Inventory.MaxWeightKg - npc.Inventory.CurrentWeightKg - 0.1);
        CompanionWorld.Follow(npc, leader);
        npc.CurrentAction = new NPCRest(2);
        world.Advance(1);
        world.MoveActor(leader, 3, 1);
        for (int i = 0; i < 30 && npc.CurrentLocation != leader.CurrentLocation; i++)
        {
            world.Advance(1);
            Assert.NotSame(world.Camp, npc.CurrentLocation);
        }
        Assert.Same(leader.CurrentLocation, npc.CurrentLocation);
    }

    [Fact]
    public void OneFollowersThirstDoesNotFreezeAnotherFollowersWork()
    {
        var world = new CompanionWorld();
        var leader = world.AddLeader(LeaderKind.Npc);
        var thirsty = world.AddNpc("Thirsty");
        var worker = world.AddNpc("Worker");
        CompanionWorld.Follow(thirsty, leader);
        CompanionWorld.Follow(worker, leader);
        thirsty.Body.Hydration = SurvivalProcessor.MAX_HYDRATION * 0.4;
        thirsty.Inventory.Add(Resource.Water, 1);
        var work = new NPCForage(10);
        world.AddForage(worker.CurrentLocation);
        worker.CurrentAction = work;
        world.Advance(3);
        Assert.Equal(3, work.MinutesSpent);
        Assert.True(thirsty.Body.HydratedPct > 0.4);
        Assert.NotNull(worker.Following);
    }

    [Fact]
    public void BudgetExhaustionRetriesWithMoreWorkAndNeverClaimsNoRoute()
    {
        var world = new CompanionWorld();
        var leader = world.AddNpc("Leader", 2, 1);
        var npc = world.AddNpc("Follower");
        CompanionWorld.Follow(npc, leader);
        Following.Observe(npc, 0);
        var algorithm = new ExhaustedPathfinder();
        world.Map.Pathfinder = algorithm;
        Following.Pursue(npc, 0);
        Assert.Equal(PursuitStatus.BudgetExceeded, npc.Following!.Status);
        Assert.Null(Following.Pursue(npc, 1));
        Assert.Equal(PursuitStatus.RetryDelay, npc.Following.Status);
        Assert.Equal(0, npc.Following.RouteFailures);
        Following.Pursue(npc, 5);
        Assert.Equal(new[] { 10000, 20000 }, algorithm.Budgets);
        for (int minute = 10; minute <= 25; minute += 5) Following.Pursue(npc, minute);
        Assert.Null(npc.Following);
        Assert.Equal("Route search budget exhausted", npc.FollowEndReason);
    }

    private sealed class ExhaustedPathfinder : text_survival.Environments.Navigation.IPathfinder
    {
        public List<int> Budgets { get; } = [];
        public text_survival.Environments.Navigation.PathResult FindPath(
            text_survival.Environments.Navigation.PathRequest request, text_survival.Environments.Navigation.NavigationView view)
        {
            Budgets.Add(request.MaxExpandedNodes);
            return new(text_survival.Environments.Navigation.PathStatus.BudgetExceeded, []);
        }
    }

    [Fact]
    public void ChainFollowerStaysWithDirectTargetWhileItFinishesWork()
    {
        var world = new CompanionWorld();
        var c = world.AddLeader(LeaderKind.Npc);
        var b = world.AddNpc("B");
        var a = world.AddNpc("A");
        CompanionWorld.Follow(b, c);
        CompanionWorld.Follow(a, b);
        world.AddForage(b.CurrentLocation);
        b.CurrentAction = new NPCForage(30);
        world.Advance(1);
        world.MoveActor(c, 4, 1);
        world.Advance(10);
        Assert.Same(b, a.Following!.Target);
        Assert.Same(b.CurrentLocation, a.CurrentLocation);
        Assert.NotSame(c.CurrentLocation, a.CurrentLocation);
    }

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
