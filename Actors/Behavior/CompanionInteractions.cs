using text_survival.Items;

namespace text_survival.Actors;

public sealed class CompanionSocialState
{
    public double NextInvitationMinute { get; set; }
    public double NextItemRequestMinute { get; set; }
    public double NextNeedRequestMinute { get; set; }
    public double NextGiftMemoryMinute { get; set; }
    public double StayUntilMinute { get; set; }
    public CompanionNeedRequest? PendingNeed { get; set; }
}

public sealed class CompanionNeedRequest
{
    public Actor Recipient { get; set; } = null!;
    public NeedType Need { get; set; }
    public double ExpiresAtMinute { get; set; }
}

public enum NeedReply { LetGo, GiveResource, AskToStay }

/// <summary>Social consent and atomic exchanges. No UI, player identity, or party membership.</summary>
public static class CompanionInteractions
{
    public static bool CanTalk(Actor a, Actor b) => a != b && a.IsAlive && b.IsAlive &&
        a.Map == b.Map && a.CurrentLocation == b.CurrentLocation;

    public static string Invite(Actor inviter, NPC invited, double minute)
    {
        if (!CanTalk(inviter, invited)) return "They are not here to answer.";
        if (minute < invited.Social.NextInvitationMinute) return "They have already answered for now.";
        invited.Social.NextInvitationMinute = minute + 60;
        if (invited.GetRelationship(inviter) < 0.1 + 0.2 * invited.Personality.Selfishness)
            return "They do not know you well enough to come along.";
        return Following.TryBegin(invited, inviter) ? "They agree to come along." : "That following arrangement cannot work.";
    }

    public static string RequestResource(Actor requester, NPC owner, Resource resource, double amount, double minute)
    {
        if (!CanTalk(requester, owner)) return "They are not here to answer.";
        if (minute < owner.Social.NextItemRequestMinute) return "They do not want another request just yet.";
        owner.Social.NextItemRequestMinute = minute + 30;
        bool water = resource == Resource.Water;
        bool food = resource.GetCategory() == ResourceCategory.Food;
        if ((water && (owner.Body.HydratedPct < 0.6 || owner.Inventory.Weight(resource) - amount < 0.5)) ||
            (food && (owner.Body.FullPct < 0.5 || owner.Inventory.GetWeight(ResourceCategory.Food) - amount < 0.3)))
            return "They need to keep that for themselves.";
        if (owner.GetRelationship(requester) < owner.Personality.Selfishness * 0.5)
            return "They are not willing to share that.";
        return Give(owner, requester, resource, amount, minute) ? "They share what you asked for." : "That resource is no longer available or cannot be carried.";
    }

    public static bool Give(Actor giver, Actor recipient, Resource resource, double amount, double minute)
    {
        if (!CanTalk(giver, recipient) || giver.Inventory == null || recipient.Inventory == null ||
            !double.IsFinite(amount) || amount <= 0 || giver.Inventory.Weight(resource) < amount || !recipient.Inventory.CanCarry(amount)) return false;
        bool useful = resource == Resource.Water ? recipient.Body.HydratedPct < 0.5 :
            resource.GetCategory() == ResourceCategory.Food && recipient.Body.FullPct < 0.3;
        var transferred = giver.Inventory.ConsumeByWeight(resource, amount);
        recipient.Inventory.Add(resource, transferred);
        if (useful && transferred >= 0.25 && recipient is NPC npc && minute >= npc.Social.NextGiftMemoryMinute)
        {
            npc.Relationships.AddMemory(MemoryType.SharedFood, giver);
            npc.Social.NextGiftMemoryMinute = minute + 360;
        }
        return transferred > 0;
    }

    public static NPCAction ConsiderNeed(NPC npc, NPCAction action, double minute)
    {
        var state = npc.Social;
        if (state.PendingNeed is { } pending)
        {
            if (action is NPCEat or NPCDrinkWater)
            {
                state.PendingNeed = null;
                return action;
            }
            if (npc.CurrentNeed != pending.Need || !CanTalk(npc, pending.Recipient) || minute >= pending.ExpiresAtMinute || npc.Following?.Target != pending.Recipient)
                state.PendingNeed = null;
            else
            {
                action.Interrupt(npc);
                npc.DecisionReason = CompanionDecisionReason.RequestWait;
                return new NPCRest(1);
            }
        }
        if (npc.Following is not { } intent || npc.CurrentNeed is not { } need || !CanTalk(npc, intent.Target)) return action;
        if (action is NPCMove && minute < state.StayUntilMinute && !Emergency(npc))
        {
            npc.DecisionReason = CompanionDecisionReason.AgreedWait;
            return new NPCRest(1);
        }
        if (minute < state.NextNeedRequestMinute || action is not NPCMove) return action;
        state.NextNeedRequestMinute = minute + 60;
        npc.DecisionReason = CompanionDecisionReason.RequestWait;
        state.PendingNeed = new CompanionNeedRequest { Recipient = intent.Target, Need = need, ExpiresAtMinute = minute + (Emergency(npc) ? 2 : 10) };
        return new NPCRest(1);
    }

    public static Resource? UsefulResource(Actor giver, NeedType need)
    {
        if (giver.Inventory == null) return null;
        if (need == NeedType.Water && giver.Inventory.Weight(Resource.Water) >= 0.5) return Resource.Water;
        if (need == NeedType.Food && giver.Inventory.Weight(Resource.CookedMeat) >= 0.5) return Resource.CookedMeat;
        return null;
    }

    public static string Reply(NPC npc, Actor recipient, NeedReply reply, double minute)
    {
        var pending = npc.Social.PendingNeed;
        if (pending == null || pending.Recipient != recipient || !CanTalk(npc, recipient) || minute >= pending.ExpiresAtMinute)
            return "The situation has changed.";
        if (reply == NeedReply.GiveResource)
        {
            var resource = UsefulResource(recipient, pending.Need);
            if (resource == null || !Give(recipient, npc, resource.Value, 0.5, minute)) return "There is no useful resource available to give.";
        }
        npc.Social.PendingNeed = null;
        if (reply == NeedReply.AskToStay)
        {
            bool agrees = !Emergency(npc) && Utils.DetermineSuccess(Math.Clamp(npc.GetRelationship(recipient) + 0.35 - npc.Personality.Selfishness * 0.2, 0.05, 0.9));
            npc.Relationships.AddMemory(MemoryType.PressuredMe, recipient);
            if (!agrees) return "They refuse; they need to take care of themselves.";
            npc.Social.StayUntilMinute = minute + 10;
            return "They agree to wait a little longer.";
        }
        return reply == NeedReply.GiveResource ? "They take the supplies and will use them when needed." : "They will take care of their need and try to catch up.";
    }

    private static bool Emergency(NPC npc) => npc.Body.HydratedPct < 0.15 || npc.Body.WarmPct < 0.15 || npc.Body.EnergyPct < 0.05;
}
