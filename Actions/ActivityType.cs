namespace text_survival.Actions;

public enum ActivityType
{
    // No events
    Idle,           // Menu, thinking - no events
    Fighting,       // Combat - no events
    Encounter,      // Predator standoff - no events

    // Camp activities (near fire, moderate events)
    Sleeping,       // Rare events, low activity
    Resting,        // Occasional events, waiting by fire
    Incapacitated,  // Occasional events, unable to move
    TendingFire,    // Moderate events
    Eating,         // Moderate events
    Cooking,        // Moderate events
    Crafting,       // Moderate events

    // Expedition activities (away from fire, full events)
    Traveling,      // Full events, moving between locations
    Foraging,       // Full events, searching for resources
    Hunting,        // Full events, tracking game
    Exploring,      // Full events, scouting new areas (away from fire)
    Chopping,       // Full events, felling trees (strenuous)
    Tracking,       // Full events, following animal signs while foraging
    Butchering,     // Full events, processing a carcass
    Fishing,        // Full events, fishing at water feature

}
