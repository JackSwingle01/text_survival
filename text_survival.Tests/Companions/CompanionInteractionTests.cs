using text_survival.Actions;
using text_survival.Actors;

namespace text_survival.Tests.Companions;

public class CompanionInteractionTests
{
    [Fact]
    public void NewSuppliesResolveAPendingNeedWithoutDiscardingReservedFood()
    {
        var world = new CompanionWorld();
        var leader = world.AddNpc("Leader");
        var npc = world.AddNpc("Follower");
        CompanionWorld.Follow(npc, leader);
        npc.CurrentNeed = NeedType.Food;
        npc.Social.PendingNeed = new CompanionNeedRequest { Recipient = leader, Need = NeedType.Food, ExpiresAtMinute = 10 };
        var eating = new NPCEat(Resource.CookedMeat, 0.5);
        Assert.Same(eating, CompanionInteractions.ConsiderNeed(npc, eating, 1));
        Assert.Null(npc.Social.PendingNeed);
        Assert.False(eating.Settled);
    }

    [Fact]
    public async Task PendingRequestIsDeliveredWithoutARandomEventRoll()
    {
        var world = new CompanionWorld();
        var npc = world.AddNpc("Follower", 0, 0);
        CompanionWorld.Follow(npc, world.Game.player);
        npc.CurrentNeed = NeedType.Water;
        npc.CurrentAction = new NPCRest(5);
        npc.Social.PendingNeed = new CompanionNeedRequest
        {
            Recipient = world.Game.player, Need = NeedType.Water, ExpiresAtMinute = 10
        };
        ((text_survival.Tests.Support.ScriptedUi)world.Game.Ui).Choices.Enqueue("go");
        await world.Game.Update(1, ActivityType.Eating);
        Assert.Null(npc.Social.PendingNeed);
        Assert.Same(world.Game.player, npc.Following!.Target);
    }

    [Fact]
    public void InvitationUsesOpinionAndCannotBeRerolledImmediately()
    {
        var world = new CompanionWorld();
        var leader = world.AddNpc("Leader");
        var npc = world.AddNpc("Stranger");
        CompanionInteractions.Invite(leader, npc, 0);
        Assert.Null(npc.Following);
        npc.Relationships.AddMemory(MemoryType.SavedMe, leader);
        CompanionInteractions.Invite(leader, npc, 1);
        Assert.Null(npc.Following);
        CompanionInteractions.Invite(leader, npc, 60);
        Assert.Same(leader, npc.Following!.Target);
    }

    [Fact]
    public void RepeatedTinyGiftsCannotFarmOpinion()
    {
        var world = new CompanionWorld();
        var giver = world.AddNpc("Giver");
        var npc = world.AddNpc("Thirsty");
        giver.Inventory.Add(Resource.Water, 2);
        npc.Body.Hydration = SurvivalProcessor.MAX_HYDRATION * 0.3;
        for (int i = 0; i < 5; i++) CompanionInteractions.Give(giver, npc, Resource.Water, 0.01, i);
        Assert.Empty(npc.Relationships.MemoryEvents);
        CompanionInteractions.Give(giver, npc, Resource.Water, 0.5, 5);
        CompanionInteractions.Give(giver, npc, Resource.Water, 0.5, 6);
        Assert.Equal(1, Assert.Single(npc.Relationships.MemoryEvents).Count);
    }

    [Fact]
    public void SharedResourceIsRevalidatedForEachRecipient()
    {
        var world = new CompanionWorld();
        var giver = world.AddNpc("Giver");
        var a = world.AddNpc("A");
        var b = world.AddNpc("B");
        giver.Inventory.Add(Resource.Water, 0.5);
        Assert.True(CompanionInteractions.Give(giver, a, Resource.Water, 0.5, 0));
        Assert.False(CompanionInteractions.Give(giver, b, Resource.Water, 0.5, 0));
        Assert.Equal(0.5, a.Inventory.Weight(Resource.Water));
        Assert.Equal(0, b.Inventory.Weight(Resource.Water));
    }

    [Fact]
    public void NpcCanRefuseToGiveAwayItsLastWater()
    {
        var world = new CompanionWorld();
        var owner = world.AddNpc("Owner");
        var requester = world.AddNpc("Requester");
        owner.Relationships.AddMemory(MemoryType.SavedMe, requester);
        owner.Inventory.Add(Resource.Water, 0.5);
        CompanionInteractions.RequestResource(requester, owner, Resource.Water, 0.5, 0);
        Assert.Equal(0.5, owner.Inventory.Weight(Resource.Water));
        Assert.Equal(0, requester.Inventory.Weight(Resource.Water));
    }

    [Fact]
    public void NeedRequestHasBoundedWaitAndLetGoPreservesReunionIntent()
    {
        var world = new CompanionWorld();
        var leader = world.AddNpc("Leader");
        var npc = world.AddNpc("Follower");
        CompanionWorld.Follow(npc, leader);
        npc.CurrentNeed = NeedType.Water;
        var action = new NPCMove(world.Tile(2, 1), npc);
        Assert.IsType<NPCRest>(CompanionInteractions.ConsiderNeed(npc, action, 0));
        CompanionInteractions.Reply(npc, leader, NeedReply.LetGo, 1);
        Assert.Null(npc.Social.PendingNeed);
        Assert.Same(leader, npc.Following!.Target);
        Assert.Same(action, CompanionInteractions.ConsiderNeed(npc, action, 2));
    }

    [Fact]
    public void PassiveFamiliarityCannotEraseSeriousAbandonment()
    {
        var world = new CompanionWorld();
        var npc = world.AddNpc("Companion");
        npc.Relationships.MemoryEvents.Add(new MemoryEvent(MemoryType.TimeTogether, world.Game.player) { Count = 100000 });
        npc.Relationships.AddMemory(MemoryType.AbandonedMe, world.Game.player);
        Assert.True(npc.GetRelationship(world.Game.player) < 0);
    }
}
