namespace Survival;

/// The NPC utility AI (phase 3d): picks an NPC's next activity, the same activities the player picks.
public static partial class Npc
{
    /// The next activity for this NPC and how many minutes to give it. Called by the scheduler when
    /// the NPC's previous activity ended or was interrupted.
    // ponytail: stub until phase 3d lands the utility AI.
    public static (Activity Activity, int Minutes) Decide(World world, Agent agent) => (new Rest(agent.Id), 60);
}
