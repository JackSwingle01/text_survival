namespace text_survival.Actors;

/// <summary>
/// Static routing class for relationship-relevant events.
/// Routes occurrences to NPCs who care, updating their RelationshipMemory.
/// Follows the Handler pattern (FireHandler, ConsumptionHandler, etc.).
/// </summary>
public static class RelationshipEvents
{
    /// <summary>
    /// Called after combat when team members survive together.
    /// All surviving team members record fighting alongside each other.
    /// </summary>
    public static void FoughtTogether(IEnumerable<Actor> team)
    {
        var survivors = team.Where(a => a.IsAlive).ToList();
        foreach (var actor in survivors)
        {
            if (actor is NPC npc)
            {
                foreach (var other in survivors)
                {
                    if (other != actor)
                        npc.Relationships.AddMemory(MemoryType.FoughtTogether, other);
                }
            }
        }
    }

    /// <summary>
    /// Called when an actor shares food with others present.
    /// Witnesses gain positive memory of the sharer.
    /// </summary>
    public static void SharedFood(Actor sharer, IEnumerable<NPC> witnesses)
    {
        foreach (var npc in witnesses)
        {
            if (npc != sharer)
                npc.Relationships.AddMemory(MemoryType.SharedFood, sharer);
        }
    }

    /// <summary>
    /// Called per-minute to track time spent together at the same location.
    /// Builds familiarity slowly over time.
    /// </summary>
    public static void TimeTogether(IEnumerable<Actor> actorsAtLocation)
    {
        var actors = actorsAtLocation.ToList();
        foreach (var actor in actors)
        {
            if (actor is NPC npc)
            {
                foreach (var other in actors)
                {
                    if (other != actor)
                        npc.Relationships.AddMemory(MemoryType.TimeTogether, other);
                }
            }
        }
    }

    /// <summary>
    /// Called when an NPC is saved from death/danger by another actor.
    /// </summary>
    public static void SavedMe(NPC npc, Actor savior)
    {
        npc.Relationships.AddMemory(MemoryType.SavedMe, savior);
    }

    /// <summary>
    /// Called when an NPC is abandoned in danger by another actor.
    /// </summary>
    public static void AbandonedMe(NPC npc, Actor abandoner)
    {
        npc.Relationships.AddMemory(MemoryType.AbandonedMe, abandoner);
    }
}
