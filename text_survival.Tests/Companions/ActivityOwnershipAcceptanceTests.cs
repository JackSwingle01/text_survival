using System.Text.Json;
using text_survival.Actions;
using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Environments.Features;
using text_survival.Persistence;

namespace text_survival.Tests.Companions;

[Trait("Suite", "CompanionAcceptance")]
public class ActivityOwnershipAcceptanceTests
{
    [Fact]
    [Trait("Scenario", "F16")]
    public void CombatParticipant_DoesNotAlsoCompleteOrdinaryForaging()
    {
        var world = new CompanionWorld();
        var npc = world.AddNpc("Ally", 0, 0);
        var forage = world.AddForage(npc.CurrentLocation);
        npc.CurrentAction = new NPCForage(1);
        StartCombat(world, npc);
        double initialHydration = npc.Body.Hydration;

        world.Advance(1);

        Assert.Equal(0, forage.NumberOfHoursForaged);
        Assert.True(npc.Body.Hydration < initialHydration, "Combat ownership must not stop physiology.");
        Assert.Equal(1, world.Game.TotalMinutesElapsed);
    }

    [Fact]
    [Trait("Scenario", "F16")]
    public void CombatElsewhere_DoesNotFreezeAnUninvolvedNpc()
    {
        var world = new CompanionWorld();
        var ally = world.AddNpc("Ally", 0, 0);
        ally.CurrentAction = new NPCRest(30);
        StartCombat(world, ally);
        var bystander = world.AddNpc("Bystander", 5, 1);
        var forage = world.AddForage(bystander.CurrentLocation);
        bystander.CurrentAction = new NPCForage(2);

        world.Advance(2);

        Assert.Equal(2.0 / 60, forage.NumberOfHoursForaged, 6);
        Assert.True(bystander.IsAlive);
    }

    [Fact]
    [Trait("Scenario", "F34")]
    public void InterruptingAnUnstartedCacheTransfer_DoesNotGrantItsCompletion()
    {
        var world = new CompanionWorld();
        var npc = world.AddNpc("Recipient", 0, 0);
        var cache = world.Camp.GetFeature<CacheFeature>()!;
        cache.Storage.Add(Resource.CookedMeat, 0.5);
        var transfer = new NPCTakeResourceFromCache(ResourceCategory.Food);

        transfer.Interrupt(npc);

        Assert.Equal(0.5, cache.Storage.Weight(Resource.CookedMeat), 6);
        Assert.Equal(0, npc.Inventory.Weight(Resource.CookedMeat));
    }

    [Fact]
    [Trait("Scenario", "F33")]
    public void SaveLoad_PreservesUnfinishedWorkAndItsEarnedProgress()
    {
        var world = new CompanionWorld();
        var npc = world.AddNpc("Worker");
        world.AddForage(npc.CurrentLocation);
        npc.CurrentAction = new NPCForage(10);
        world.Advance(3);

        var loaded = RoundTrip(world.Game);
        var loadedNpc = Assert.Single(loaded.NPCs);

        Assert.NotNull(loadedNpc.CurrentAction);
        Assert.Equal(3, loadedNpc.CurrentAction.MinutesSpent);
        Assert.Equal(10, loadedNpc.CurrentAction.DurationMinutes);
        Assert.Equal(0, loadedNpc.CurrentLocation.GetFeature<ForageFeature>()!.NumberOfHoursForaged);

        for (int i = 0; i < 7; i++) loaded.UpdateWithoutEvents(1, ActivityType.Resting);
        Assert.Equal(10.0 / 60, loadedNpc.CurrentLocation.GetFeature<ForageFeature>()!.NumberOfHoursForaged, 6);
    }

    [Fact]
    public void InterruptedCrossingNeverMovesBeforeItsFullDuration()
    {
        var world = new CompanionWorld();
        var npc = world.AddNpc("Walker");
        var origin = npc.CurrentLocation;
        var move = new NPCMove(world.Tile(2, 1), npc);
        move.MinutesSpent = move.DurationMinutes - 1;
        move.Interrupt(npc);
        Assert.Same(origin, npc.CurrentLocation);
        Assert.Empty(world.Map.Tracks.Marks);
    }

    [Fact]
    public void SaveLoadKeepsReservedFoodAndCancellationReturnsIt()
    {
        var world = new CompanionWorld();
        var npc = world.AddNpc("Eater");
        npc.CurrentAction = new NPCEat(Resource.CookedMeat, 0.5) { MinutesSpent = 2 };
        var loadedNpc = Assert.Single(RoundTrip(world.Game).NPCs);
        Assert.IsType<NPCEat>(loadedNpc.CurrentAction).Interrupt(loadedNpc);
        loadedNpc.CurrentAction!.Interrupt(loadedNpc);
        loadedNpc.CurrentAction.Complete(loadedNpc);
        Assert.Equal(0.5, loadedNpc.Inventory.Weight(Resource.CookedMeat), 6);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Scenario", "F33")]
    public void SaveLoad_PreservesNpcTargetIdentityRegardlessOfRosterOrder(bool followerFirst)
    {
        var world = new CompanionWorld();
        var leader = world.AddNpc("Same name");
        var follower = world.AddNpc("Same name");
        CompanionWorld.Follow(follower, leader);
        if (followerFirst)
            world.Game.NPCs.Reverse();

        var loaded = RoundTrip(world.Game);
        var loadedFollower = Assert.Single(loaded.NPCs, n => n.Following != null);
        var loadedLeader = Assert.Single(loaded.NPCs, n => n.Following == null);

        Assert.Same(loadedLeader, loadedFollower.Following!.Target);
        Assert.Same(loadedLeader, Assert.Single(loadedFollower.Relationships.MemoryEvents).Subject);
    }

    [Fact]
    [Trait("Scenario", "F33")]
    public void SaveLoad_PreservesPlayerTargetIdentity()
    {
        var world = new CompanionWorld();
        var follower = world.AddNpc("Follower");
        CompanionWorld.Follow(follower, world.Game.player);

        var loaded = RoundTrip(world.Game);

        Assert.Same(loaded.player, Assert.Single(loaded.NPCs).Following!.Target);
    }

    private static GameContext RoundTrip(GameContext game) =>
        JsonSerializer.Deserialize<GameContext>(JsonSerializer.Serialize(game, SaveManager.Options), SaveManager.Options)!;

    private static void StartCombat(CompanionWorld world, NPC ally)
    {
        var enemy = AnimalFactory.FromType(AnimalType.Caribou, world.Camp, world.Map)!;
        world.Game.ActiveCombat = CombatScenario.Create(
            [world.Game.player, ally], [enemy], world.Camp, 10,
            AwarenessState.Engaged, AwarenessState.Engaged, world.Game.player);
    }
}
