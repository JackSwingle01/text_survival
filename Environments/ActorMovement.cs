using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Environments.Grid;

namespace text_survival.Environments;

/// <summary>Commits physical movement without changing the player's map cursor or discovery.</summary>
public static class ActorMovement
{
    public static bool CompleteCrossing(Actor actor, Location destination)
    {
        if (actor.CurrentLocation == destination) return true;
        var map = actor.Map;
        if (!map.GetTravelOptionsFrom(actor.CurrentLocation).Contains(destination)) return false;
        var origin = actor.CurrentLocation;
        map.RecordMove(map.GetPosition(origin), map.GetPosition(destination),
            actor is Animal animal ? animal.AnimalType.Tracks() : TrackMaker.Human);
        actor.CurrentLocation = destination;
        if (actor is NPC npc)
        {
            npc.ResourceMemory.RememberLocation(origin);
            npc.ResourceMemory.RememberLocation(destination);
        }
        return true;
    }
}
