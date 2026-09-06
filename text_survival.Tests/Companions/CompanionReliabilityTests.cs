using text_survival.Actors;
using text_survival.Actions;
using text_survival.Environments;

namespace text_survival.Tests.Companions;

public class CompanionReliabilityTests
{
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
