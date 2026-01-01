using text_survival.Actions;
using text_survival.Actors.Animals;

namespace text_survival.Combat;

/// <summary>
/// Possible outcomes of a combat encounter.
/// </summary>
public enum CombatOutcome
{
    /// <summary>Player killed the animal.</summary>
    Victory,

    /// <summary>Player was killed.</summary>
    PlayerDied,

    /// <summary>Player successfully disengaged.</summary>
    PlayerDisengaged,

    /// <summary>Animal fled the combat.</summary>
    AnimalFled,

    /// <summary>Animal left after incapacitating player (mercy).</summary>
    AnimalDisengaged,

    /// <summary>Player dropped meat and animal took it.</summary>
    DistractedWithMeat
}

/// <summary>
/// Tracks all combat state for a single encounter.
/// Unifies what was previously split between EncounterRunner and CombatRunner.
/// </summary>
public class CombatState
{
    private static readonly Random _rng = new();

    // Base movement distances (meters) - modified by terrain and capacity
    private const double BasePlayerMove = 4.0;
    private const double BaseAnimalMove = 5.0;

    #region Core State

    /// <summary>The animal being fought.</summary>
    public Animal Animal { get; }

    /// <summary>Distance in meters between player and animal.</summary>
    public double DistanceMeters { get; private set; }

    /// <summary>Previous distance for UI animation.</summary>
    public double? PreviousDistanceMeters { get; private set; }

    /// <summary>Current distance zone (derived from DistanceMeters).</summary>
    public DistanceZone Zone => DistanceZoneHelper.GetZone(DistanceMeters);

    /// <summary>Animal behavior state machine.</summary>
    public AnimalCombatBehaviorManager Behavior { get; }

    /// <summary>Previous behavior state (for detecting transitions).</summary>
    public CombatBehavior? PreviousBehavior { get; private set; }

    /// <summary>Number of turns elapsed in this combat.</summary>
    public int TurnCount { get; private set; } = 0;

    /// <summary>Whether player has set a brace (spear ready for charge).</summary>
    public bool PlayerBraced { get; set; } = false;

    /// <summary>Whether the animal has attacked and dealt damage this combat.</summary>
    public bool AnimalHasAttacked { get; private set; } = false;

    /// <summary>Last action the player took (affects animal behavior).</summary>
    public CombatPlayerAction LastPlayerAction { get; private set; } = CombatPlayerAction.None;

    /// <summary>Messages accumulated during the current turn.</summary>
    public List<string> TurnMessages { get; } = new();

    #endregion

    #region Terrain & Environment

    /// <summary>
    /// Location hazard level (0-1). Affects dodge chances and movement speed for both combatants.
    /// </summary>
    public double TerrainHazard { get; private set; }

    /// <summary>
    /// Calculates terrain movement modifier. Higher hazard = slower movement.
    /// </summary>
    public double TerrainMovementFactor => 1.0 - (TerrainHazard * 0.2);

    /// <summary>
    /// Calculates terrain dodge modifier. Higher hazard = harder to dodge.
    /// </summary>
    public double TerrainDodgeFactor => 1.0 - (TerrainHazard * 0.3);

    #endregion

    #region Thrown Weapon Tracking

    /// <summary>
    /// Distance where thrown weapon landed (null if not thrown).
    /// </summary>
    public double? ThrownWeaponDistanceMeters { get; private set; }

    /// <summary>
    /// Whether player can retrieve their thrown weapon (within ~3m of weapon).
    /// </summary>
    public bool CanRetrieveWeapon =>
        ThrownWeaponDistanceMeters.HasValue &&
        Math.Abs(DistanceMeters - ThrownWeaponDistanceMeters.Value) < 3.0;

    /// <summary>
    /// Records a weapon throw, tracking where it landed.
    /// </summary>
    public void RecordWeaponThrow(double targetDistanceMeters)
    {
        ThrownWeaponDistanceMeters = targetDistanceMeters;
    }

    /// <summary>
    /// Marks the weapon as retrieved.
    /// </summary>
    public void WeaponRetrieved()
    {
        ThrownWeaponDistanceMeters = null;
    }

    #endregion

    #region Constructor

    public CombatState(Animal animal, double initialDistance, double initialBoldness, double terrainHazard = 0.0)
    {
        Animal = animal;
        DistanceMeters = Math.Clamp(initialDistance, 2.0, 25.0);
        Behavior = new AnimalCombatBehaviorManager(animal, initialBoldness);
        TerrainHazard = Math.Clamp(terrainHazard, 0.0, 1.0);
    }

    /// <summary>
    /// Creates combat state from an existing encounter setup.
    /// </summary>
    public static CombatState FromEncounter(Animal animal, double terrainHazard = 0.0)
    {
        return new CombatState(
            animal,
            animal.DistanceFromPlayer,
            animal.EncounterBoldness,
            terrainHazard
        );
    }

    #endregion

    #region Distance Management

    /// <summary>
    /// Calculates actual movement distance using the formula:
    /// distance = terrainHazard * movingCapacity * baseMove * random(0.8-1.2)
    /// </summary>
    private double CalculateMovement(double baseMove, double movingCapacity)
    {
        double randomVariance = 0.8 + _rng.NextDouble() * 0.4; // 0.8 to 1.2
        return TerrainMovementFactor * movingCapacity * baseMove * randomVariance;
    }

    /// <summary>
    /// Player moves closer to the animal. Actual distance depends on terrain and player condition.
    /// </summary>
    public double PlayerClosesDistance(double playerMovingCapacity)
    {
        PreviousDistanceMeters = DistanceMeters;
        double actualMove = CalculateMovement(BasePlayerMove, playerMovingCapacity);
        DistanceMeters = Math.Max(DistanceZoneHelper.MeleeCenter, DistanceMeters - actualMove);
        return actualMove;
    }

    /// <summary>
    /// Player moves away from the animal. Actual distance depends on terrain and player condition.
    /// </summary>
    public double PlayerIncreasesDistance(double playerMovingCapacity)
    {
        PreviousDistanceMeters = DistanceMeters;
        double actualMove = CalculateMovement(BasePlayerMove, playerMovingCapacity);
        DistanceMeters = Math.Min(DistanceZoneHelper.FarMax + 5, DistanceMeters + actualMove);
        return actualMove;
    }

    /// <summary>
    /// Animal closes distance to player. Uses animal's speed and terrain.
    /// </summary>
    public double AnimalClosesDistance()
    {
        PreviousDistanceMeters = DistanceMeters;
        double animalSpeed = Animal.SpeedMps / 2.0; // Normalize to reasonable combat movement
        double actualMove = CalculateMovement(BaseAnimalMove, Math.Min(1.0, animalSpeed / 5.0));
        DistanceMeters = Math.Max(DistanceZoneHelper.MeleeCenter, DistanceMeters - actualMove);
        return actualMove;
    }

    /// <summary>
    /// Animal increases distance from player (retreating).
    /// </summary>
    public double AnimalIncreasesDistance()
    {
        PreviousDistanceMeters = DistanceMeters;
        double animalSpeed = Animal.SpeedMps / 2.0;
        double actualMove = CalculateMovement(BaseAnimalMove, Math.Min(1.0, animalSpeed / 5.0));
        DistanceMeters = Math.Min(DistanceZoneHelper.FarMax + 5, DistanceMeters + actualMove);
        return actualMove;
    }

    /// <summary>
    /// Moves the player closer by a fixed amount (for special actions like shove).
    /// </summary>
    public void CloseDistance(double meters)
    {
        PreviousDistanceMeters = DistanceMeters;
        DistanceMeters = Math.Max(DistanceZoneHelper.MeleeCenter, DistanceMeters - meters);
    }

    /// <summary>
    /// Moves the player away by a fixed amount (for special actions).
    /// </summary>
    public void IncreaseDistance(double meters)
    {
        PreviousDistanceMeters = DistanceMeters;
        DistanceMeters = Math.Min(DistanceZoneHelper.FarMax + 5, DistanceMeters + meters);
    }

    /// <summary>
    /// Sets distance to the center of a specific zone.
    /// </summary>
    public void SetToZone(DistanceZone zone)
    {
        PreviousDistanceMeters = DistanceMeters;
        DistanceMeters = DistanceZoneHelper.GetZoneCenter(zone);
    }

    /// <summary>
    /// Moves one zone closer.
    /// </summary>
    public bool MoveCloserZone()
    {
        var newZone = DistanceZoneHelper.GetCloserZone(Zone);
        if (newZone.HasValue)
        {
            SetToZone(newZone.Value);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Moves one zone farther.
    /// </summary>
    public bool MoveFartherZone()
    {
        var newZone = DistanceZoneHelper.GetFartherZone(Zone);
        if (newZone.HasValue)
        {
            SetToZone(newZone.Value);
            return true;
        }
        return false;
    }

    #endregion

    #region Turn Management

    /// <summary>
    /// Called at the start of each turn. Clears messages, increments counter.
    /// </summary>
    public void StartTurn()
    {
        TurnCount++;
        TurnMessages.Clear();
    }

    /// <summary>
    /// Records the player's action for this turn (affects animal behavior).
    /// </summary>
    public void RecordPlayerAction(CombatPlayerAction action)
    {
        LastPlayerAction = action;

        // Clear brace if player does something other than hold
        if (action != CombatPlayerAction.HoldGround && action != CombatPlayerAction.Brace)
        {
            PlayerBraced = false;
        }
    }

    /// <summary>
    /// Marks that the animal has attacked the player this combat.
    /// </summary>
    public void RecordAnimalAttack()
    {
        AnimalHasAttacked = true;
    }

    /// <summary>
    /// Called at the end of each turn to update animal behavior.
    /// </summary>
    public void EndTurn()
    {
        // Capture previous behavior before update (for transition messaging)
        PreviousBehavior = Behavior.CurrentBehavior;

        Behavior.UpdateBehavior(LastPlayerAction, Zone, Animal.Vitality);

        // Sync distance back to animal for other systems
        Animal.DistanceFromPlayer = DistanceMeters;
    }

    /// <summary>
    /// Adds a message to the turn narrative.
    /// </summary>
    public void AddMessage(string message)
    {
        TurnMessages.Add(message);
    }

    /// <summary>
    /// Gets all turn messages as a single string.
    /// </summary>
    public string GetTurnNarrative()
    {
        return string.Join(" ", TurnMessages);
    }

    #endregion

    #region Combat Resolution Checks

    /// <summary>
    /// Checks if combat should end and returns the outcome.
    /// Returns null if combat continues.
    /// </summary>
    public CombatOutcome? CheckForEnd(GameContext ctx)
    {
        // Player death
        if (!ctx.player.IsAlive)
            return CombatOutcome.PlayerDied;

        // Animal death
        if (!Animal.IsAlive)
            return CombatOutcome.Victory;

        // Animal fleeing at far range
        if (Behavior.IsTryingToFlee() && Zone == DistanceZone.Far)
            return CombatOutcome.AnimalFled;

        // Animal disengages after incapacitating player
        if (ShouldAnimalDisengage(ctx))
            return CombatOutcome.AnimalDisengaged;

        return null;
    }

    /// <summary>
    /// Check if animal should disengage from combat.
    /// Animals may leave after severely injuring the player.
    /// </summary>
    private bool ShouldAnimalDisengage(GameContext ctx)
    {
        // Only disengage after the animal has attacked (mauled the player)
        if (!AnimalHasAttacked) return false;

        var capacities = ctx.player.GetCapacities();

        // Only consider disengage if player is incapacitated
        bool playerIncapacitated = capacities.Moving < 0.3 || capacities.Consciousness < 0.4;
        if (!playerIncapacitated) return false;

        // Calculate disengage chance
        double chance = Animal.DisengageAfterMaul;

        // Higher chance if player is severely incapacitated
        if (capacities.Moving < 0.2) chance += 0.15;
        if (capacities.Consciousness < 0.3) chance += 0.2;

        // Lower chance if player is still a threat
        if (ctx.Inventory.Weapon != null && capacities.Manipulation > 0.3)
            chance -= 0.1;

        chance = Math.Clamp(chance, 0, 0.85);

        return _rng.NextDouble() < chance;
    }

    /// <summary>
    /// Whether the player can attempt to disengage (Far zone only).
    /// </summary>
    public bool CanDisengage(GameContext ctx)
    {
        if (Zone != DistanceZone.Far) return false;

        // Need reasonable movement capacity
        var capacities = ctx.player.GetCapacities();
        return capacities.Moving > 0.3;
    }

    /// <summary>
    /// Attempt to disengage from combat.
    /// At Far range with movement capacity, you can always successfully disengage.
    /// </summary>
    public bool AttemptDisengage(GameContext ctx)
    {
        // At Far range with movement capacity, you can always disengage successfully
        return CanDisengage(ctx);
    }

    #endregion

    #region Descriptive Health (No HP Bars)

    /// <summary>
    /// Gets a descriptive health status for the animal (no numbers).
    /// </summary>
    public string GetAnimalHealthDescription()
    {
        double vitality = Animal.Vitality;

        if (vitality > 0.9)
            return "uninjured";
        if (vitality > 0.7)
            return "lightly wounded";
        if (vitality > 0.5)
            return "wounded";
        if (vitality > 0.3)
            return "badly hurt";
        if (vitality > 0.15)
            return "staggering";
        return "near death";
    }

    /// <summary>
    /// Gets a detailed health narrative for the animal.
    /// </summary>
    public string GetAnimalConditionNarrative()
    {
        double vitality = Animal.Vitality;
        string name = Animal.Name;

        if (vitality > 0.9)
            return $"The {name} appears unhurt.";
        if (vitality > 0.7)
            return $"The {name} shows signs of injury.";
        if (vitality > 0.5)
            return $"Blood mats the {name}'s fur.";
        if (vitality > 0.3)
            return $"The {name} favors its wounds, moving slowly.";
        if (vitality > 0.15)
            return $"The {name} staggers, barely standing.";
        return $"The {name} is on the verge of collapse.";
    }

    #endregion
}
