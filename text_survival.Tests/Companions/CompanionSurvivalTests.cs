using text_survival.Actors;
using text_survival.Environments.Features;

namespace text_survival.Tests.Companions;

public class CompanionSurvivalTests
{
    [Fact]
    public void ThirstyCompanionUsesSharedCacheWithoutPlayerManagement()
    {
        var world = new CompanionWorld();
        var npc = world.AddNpc("Thirsty", 0, 0);
        CompanionWorld.Follow(npc, world.Game.player);
        npc.Body.Hydration = SurvivalProcessor.MAX_HYDRATION * 0.4;
        world.Camp.GetFeature<CacheFeature>()!.Storage.Add(Resource.Water, 1);
        world.Advance(5);
        Assert.True(npc.Body.HydratedPct > 0.4);
        Assert.True(world.Camp.GetFeature<CacheFeature>()!.Storage.Weight(Resource.Water) < 1);
    }

    [Fact]
    public void FamiliarSociableNpcCanChooseToFollowAnotherNpc()
    {
        var world = new CompanionWorld();
        var leader = world.AddNpc("Leader");
        leader.CurrentAction = new NPCRest(100);
        var npc = world.AddNpc("Sociable");
        npc.Relationships.AddMemory(MemoryType.SavedMe, leader);
        npc.Relationships.AddMemory(MemoryType.FoughtTogether, leader);
        world.Advance(1);
        Assert.Same(leader, npc.Following!.Target);
    }

    [Fact]
    public void CompanionCanRestAtASafeTemporaryStop()
    {
        var world = new CompanionWorld();
        var leader = world.AddLeader(LeaderKind.Npc);
        var npc = world.AddNpc("Tired");
        CompanionWorld.Follow(npc, leader);
        Assert.NotSame(world.Camp, npc.CurrentLocation);
        Assert.True(npc.CanSleep());
    }
}
