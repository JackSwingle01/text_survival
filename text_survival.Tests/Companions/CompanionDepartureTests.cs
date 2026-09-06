using text_survival.Actors;

namespace text_survival.Tests.Companions;

public class CompanionDepartureTests
{
    [Fact]
    public void OldDepartureCannotEndANewAgreement()
    {
        var world = new CompanionWorld();
        var oldTarget = world.AddNpc("Old target");
        var newTarget = world.AddNpc("New target");
        var npc = world.AddNpc("Follower");
        npc.Social.PendingNeed = new() { IsDeparture = true, Recipient = oldTarget, ExpiresAtMinute = 10 };
        CompanionWorld.Follow(npc, newTarget);
        CompanionInteractions.UpdateAgreement(npc, 1);
        Assert.Null(npc.Social.PendingNeed);
        Assert.Same(newTarget, npc.Following!.Target);
    }

    [Fact]
    public void ResolvedStayRequestCannotBeRerolledAfterSave()
    {
        var world = new CompanionWorld();
        var npc = world.AddNpc("Follower", 0, 0);
        CompanionWorld.Follow(npc, world.Game.player);
        npc.CurrentNeed = NeedType.Water;
        npc.Social.PendingNeed = new() { Id = 1, Recipient = world.Game.player, Need = NeedType.Water, ExpiresAtMinute = 10 };
        CompanionInteractions.Reply(npc, world.Game.player, NeedReply.AskToStay, 1, 1);
        int pressure = npc.Relationships.MemoryEvents.Where(m => m.Type == MemoryType.PressuredMe).Sum(m => m.Count);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<text_survival.Actions.GameContext>(
            System.Text.Json.JsonSerializer.Serialize(world.Game, text_survival.Persistence.SaveManager.Options),
            text_survival.Persistence.SaveManager.Options)!;
        var restored = loaded.NPCs[0];
        double until = restored.Social.StayUntilMinute;
        CompanionInteractions.Reply(restored, loaded.player, NeedReply.AskToStay, 2, 1);
        Assert.Equal(until, restored.Social.StayUntilMinute);
        Assert.Equal(pressure, restored.Relationships.MemoryEvents.Where(m => m.Type == MemoryType.PressuredMe).Sum(m => m.Count));
        Assert.Null(restored.Social.PendingNeed);
    }

    [Fact]
    public void DeterioratingOpinionWarnsNearbyTargetAndDepartureHasCooldown()
    {
        var world = new CompanionWorld();
        var npc = world.AddNpc("Follower", 0, 0);
        CompanionWorld.Follow(npc, world.Game.player);
        npc.Relationships.AddMemory(MemoryType.AbandonedMe, world.Game.player);
        world.Advance(1);
        var request = Assert.IsType<CompanionNeedRequest>(npc.Social.PendingNeed);
        Assert.True(request.IsDeparture);
        CompanionInteractions.Reply(npc, world.Game.player, NeedReply.LetGo, 1, request.Id);
        Assert.Null(npc.Following);
        Assert.True(npc.NextSocialDecisionMinute > 1);
        Assert.Equal("Chose to leave", npc.FollowEndReason);
    }

    [Fact]
    public void OutOfCommunicationDepartureDoesNotSendRemoteRequest()
    {
        var world = new CompanionWorld(31, 3);
        var npc = world.AddNpc("Follower");
        CompanionWorld.Follow(npc, world.Game.player);
        npc.Relationships.AddMemory(MemoryType.AbandonedMe, world.Game.player);
        world.Advance(1);
        Assert.Null(npc.Following);
        Assert.Null(npc.Social.PendingNeed);
    }

    [Fact]
    public void StaleResponseCannotResolveAnotherIncident()
    {
        var world = new CompanionWorld();
        var npc = world.AddNpc("Follower", 0, 0);
        CompanionWorld.Follow(npc, world.Game.player);
        npc.CurrentNeed = NeedType.Water;
        npc.Social.PendingNeed = new() { Id = 2, Recipient = world.Game.player, Need = NeedType.Water, ExpiresAtMinute = 10 };
        CompanionInteractions.Reply(npc, world.Game.player, NeedReply.AskToStay, 1, 1);
        Assert.Equal(2, npc.Social.PendingNeed!.Id);
        Assert.DoesNotContain(npc.Relationships.MemoryEvents, m => m.Type == MemoryType.PressuredMe);
    }

    [Fact]
    public void ReadyFoodOtherThanMeatCanResolveHunger()
    {
        var world = new CompanionWorld();
        world.Game.player.Inventory.Add(Resource.Berries, 1);
        Assert.Equal(Resource.Berries, CompanionInteractions.UsefulResource(world.Game.player, NeedType.Food));
    }
}
