using text_survival.Actors;
using text_survival.Actors.Animals;

namespace text_survival.Combat;

/// <summary>
/// Which side an actor is on in combat.
/// </summary>
public enum CombatTeam
{
    Player,
    Enemy,
    Ally
}

/// <summary>
/// Wraps an actor for combat with its own behavior state.
/// Does NOT know about grid positions - that's CombatMap's job.
/// </summary>
public class CombatActor
{
    private static int _nextId = 0;

    /// <summary>Unique ID for CombatMap lookups.</summary>
    public int Id { get; }

    /// <summary>The underlying actor (Animal, Player, or NPC).</summary>
    public Actor Actor { get; }

    /// <summary>Which side this actor is on.</summary>
    public CombatTeam Team { get; }

    /// <summary>
    /// Behavior state machine for this actor.
    /// Null for player (player makes own decisions).
    /// </summary>
    public AnimalCombatBehaviorManager? Behavior { get; }

    /// <summary>Current boldness level (0-1).</summary>
    public double Boldness { get; set; }

    /// <summary>Whether this actor is still engaged in combat.</summary>
    public bool IsEngaged { get; set; } = true;

    /// <summary>Whether this actor has fled the combat.</summary>
    public bool HasFled { get; set; } = false;

    /// <summary>Display name for UI.</summary>
    public string Name => Actor.Name;

    /// <summary>Is this actor still alive and in the fight?</summary>
    public bool IsActive => Actor.IsAlive && IsEngaged && !HasFled;

    /// <summary>Current vitality (0-1) for AI decisions.</summary>
    public double Vitality => Actor.Vitality;

    /// <summary>Is this the player?</summary>
    public bool IsPlayer => Team == CombatTeam.Player;

    /// <summary>Is this an enemy?</summary>
    public bool IsEnemy => Team == CombatTeam.Enemy;

    /// <summary>Is this an ally?</summary>
    public bool IsAlly => Team == CombatTeam.Ally;

    /// <summary>
    /// Create a combat actor for an animal enemy.
    /// </summary>
    public static CombatActor CreateEnemy(Animal animal, double initialBoldness)
    {
        return new CombatActor(
            animal,
            CombatTeam.Enemy,
            new AnimalCombatBehaviorManager(animal, initialBoldness),
            initialBoldness
        );
    }

    /// <summary>
    /// Create a combat actor for the player.
    /// </summary>
    public static CombatActor CreatePlayer(Actor player)
    {
        return new CombatActor(player, CombatTeam.Player, null, 0);
    }

    /// <summary>
    /// Create a combat actor for an ally NPC.
    /// For now, allies use animal behavior. May change later.
    /// </summary>
    public static CombatActor CreateAlly(Actor ally, double initialBoldness)
    {
        var behavior = ally is Animal animal
            ? new AnimalCombatBehaviorManager(animal, initialBoldness)
            : null;

        return new CombatActor(ally, CombatTeam.Ally, behavior, initialBoldness);
    }

    private CombatActor(Actor actor, CombatTeam team, AnimalCombatBehaviorManager? behavior, double boldness)
    {
        Id = _nextId++;
        Actor = actor;
        Team = team;
        Behavior = behavior;
        Boldness = boldness;
    }

    /// <summary>
    /// Get the current behavior state (for enemies/allies with AI).
    /// </summary>
    public CombatBehavior? CurrentBehavior => Behavior?.CurrentBehavior;

    /// <summary>
    /// Update behavior based on distance zone and player action.
    /// Returns movement intent (positive = toward player, negative = away).
    /// </summary>
    public double UpdateBehavior(DistanceZone zone, CombatPlayerAction lastPlayerAction)
    {
        if (Behavior == null) return 0;

        Behavior.UpdateBehavior(lastPlayerAction, zone, Vitality);

        // Return movement intent based on behavior
        return CurrentBehavior switch
        {
            CombatBehavior.Approaching => 3.0,  // Move toward
            CombatBehavior.Attacking => 5.0,    // Close fast
            CombatBehavior.Retreating => -3.0,  // Move away
            CombatBehavior.Disengaging => -5.0, // Flee fast
            CombatBehavior.Circling => 0,       // Lateral (handled separately)
            _ => 0
        };
    }

    /// <summary>
    /// Mark this actor as fled from combat.
    /// </summary>
    public void Flee()
    {
        HasFled = true;
        IsEngaged = false;
    }

    /// <summary>
    /// Get description of current behavior for narrative.
    /// </summary>
    public string GetBehaviorDescription()
    {
        return CurrentBehavior switch
        {
            CombatBehavior.Circling => $"The {Name} circles, watching you.",
            CombatBehavior.Approaching => $"The {Name} moves closer.",
            CombatBehavior.Threatening => $"The {Name} snarls, hackles raised.",
            CombatBehavior.Attacking => $"The {Name} lunges!",
            CombatBehavior.Recovering => $"The {Name} is off-balance.",
            CombatBehavior.Retreating => $"The {Name} backs away.",
            CombatBehavior.Disengaging => $"The {Name} tries to break away.",
            _ => $"The {Name} watches you."
        };
    }
}
