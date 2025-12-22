using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.UI;

namespace text_survival.Actors.Player;

/// <summary>
/// Manages stealth hunting mechanics including animal targeting, approach, and detection.
/// Player component for tracking distance and stealth state during hunting.
/// </summary>
public class StealthManager
{
    private readonly Player _player;

    /// <summary>
    /// Currently targeted animal for hunting.
    /// </summary>
    public Animal? TargetAnimal { get; private set; }

    /// <summary>
    /// Whether player is currently in stealth/hunting mode.
    /// </summary>
    public bool IsHunting => TargetAnimal != null;

    public StealthManager(Player player)
    {
        _player = player;
    }

    #region Target Management

    /// <summary>
    /// Begin hunting a specific animal. Sets it as the target.
    /// </summary>
    public void StartHunting(Animal animal)
    {
        TargetAnimal = animal;
        GameDisplay.AddNarrative($"You begin stalking the {animal.Name}...");
        GameDisplay.AddNarrative($"Distance: {animal.DistanceFromPlayer:F0}m");
        GameDisplay.AddNarrative($"Animal state: {animal.State}");
    }

    /// <summary>
    /// Stop hunting the current target (animal fled, killed, or player cancels).
    /// </summary>
    public void StopHunting(string reason = "")
    {
        if (TargetAnimal != null)
        {
            if (!string.IsNullOrEmpty(reason))
            {
                GameDisplay.AddNarrative(reason);
            }
            TargetAnimal = null;
        }
    }

    /// <summary>
    /// Check if target animal is still valid (alive, in location).
    /// </summary>
    public bool IsTargetValid()
    {
        if (TargetAnimal == null)
            return false;

        if (!TargetAnimal.IsAlive)
        {
            StopHunting($"The {TargetAnimal.Name} is dead.");
            return false;
        }

        return true;
    }

    #endregion

    #region Stealth Approach

    /// <summary>
    /// Attempt to approach the target animal, reducing distance and checking for detection.
    /// </summary>
    /// <returns>True if approach successful (not detected), false if detected</returns>
    public bool AttemptApproach(Location location)
    {
        if (!IsTargetValid())
            return false;

        Animal animal = TargetAnimal!;

        // Calculate approach distance
        double approachDistance = HuntingCalculator.CalculateApproachDistance();
        double newDistance = Math.Max(0, animal.DistanceFromPlayer - approachDistance);

        GameDisplay.AddNarrative($"You carefully move {approachDistance:F0}m closer...");

        // Check for detection (uses traits + activity)
        int huntingSkill = _player.Skills.GetSkill("Hunting").Level;
        double impairedMultiplier = 1.0;
        if (AbilityCalculator.IsConsciousnessImpaired(_player.GetCapacities().Consciousness))
            impairedMultiplier *= 1.3;  // +30% detection when mentally impaired
        if (AbilityCalculator.IsPerceptionImpaired(
            AbilityCalculator.CalculatePerception(_player.Body, _player.EffectRegistry.GetCapacityModifiers())))
            impairedMultiplier *= 1.3;  // +30% detection when senses are foggy
        double detectionChance = HuntingCalculator.CalculateDetectionChanceWithTraits(
            newDistance,
            animal,
            huntingSkill,
            animal.FailedStealthChecks,
            impairedMultiplier
        );

        double detectionRoll = Utils.RandDouble(0, 1);
        bool detected = detectionRoll < detectionChance;
        bool becameAlert = !detected && HuntingCalculator.ShouldBecomeAlert(detectionRoll, detectionChance);

        // Update distance and activity (time passed during approach)
        animal.DistanceFromPlayer = newDistance;
        if (animal.CheckActivityChange(7, out var newActivity) && newActivity.HasValue)
        {
            GameDisplay.AddNarrative($"The {animal.Name} shifts—now {animal.GetActivityDescription()}.");
        }

        // Handle detection results
        if (detected)
        {
            animal.BecomeDetected();
            GameDisplay.AddNarrative($"The {animal.Name} spots you!");
            HandleAnimalDetection(animal, location);
            return false;
        }
        else if (becameAlert)
        {
            animal.BecomeAlert();
            animal.FailedStealthChecks++;
            GameDisplay.AddNarrative($"The {animal.Name} becomes alert - it senses something nearby.");
            GameDisplay.AddNarrative($"New distance: {animal.DistanceFromPlayer:F0}m | State: {animal.State}");
            return true;
        }
        else
        {
            GameDisplay.AddNarrative($"You remain undetected.");
            GameDisplay.AddNarrative($"New distance: {animal.DistanceFromPlayer:F0}m | State: {animal.State}");
            return true;
        }
    }

    /// <summary>
    /// Assess the target animal - view its status without approaching.
    /// </summary>
    public void AssessTarget()
    {
        if (!IsTargetValid())
            return;

        Animal animal = TargetAnimal!;

        GameDisplay.AddNarrative($"\n=== {animal.GetTraitDescription()} ===");
        GameDisplay.AddNarrative($"Distance: {animal.DistanceFromPlayer:F0}m");
        GameDisplay.AddNarrative($"Activity: {animal.GetActivityDescription()}");
        GameDisplay.AddNarrative($"Vitality: {animal.Vitality * 100:F0}%");

        // Show detection chance for next approach (uses traits + activity)
        int huntingSkill = _player.Skills.GetSkill("Hunting").Level;
        double nextApproachDistance = animal.DistanceFromPlayer - 25; // Average approach
        double impairedMultiplier = 1.0;
        if (AbilityCalculator.IsConsciousnessImpaired(_player.GetCapacities().Consciousness))
            impairedMultiplier *= 1.3;
        if (AbilityCalculator.IsPerceptionImpaired(
            AbilityCalculator.CalculatePerception(_player.Body, _player.EffectRegistry.GetCapacityModifiers())))
            impairedMultiplier *= 1.3;
        double detectionChance = HuntingCalculator.CalculateDetectionChanceWithTraits(
            nextApproachDistance,
            animal,
            huntingSkill,
            animal.FailedStealthChecks,
            impairedMultiplier
        );

        GameDisplay.AddNarrative($"\nNext approach detection risk: {detectionChance * 100:F0}%");

        // Hint about activity affecting detection
        if (animal.CurrentActivity == AnimalActivity.Grazing)
            GameDisplay.AddNarrative("It's distracted—a good time to approach.");
        else if (animal.CurrentActivity == AnimalActivity.Alert)
            GameDisplay.AddNarrative("It's very alert—approaching now is risky.");

        if (animal.State == AnimalState.Alert)
        {
            GameDisplay.AddNarrative("! Animal is alert - detection chance increased!");
        }
    }

    #endregion

    #region Detection Response

    /// <summary>
    /// Handle what happens when animal detects the player.
    /// Animal response depends on behavior type.
    /// </summary>
    private void HandleAnimalDetection(Animal animal, Location location)
    {
        // Check if animal should flee
        if (animal.ShouldFlee(_player))
        {
            GameDisplay.AddNarrative($"The {animal.Name} flees!");
            StopHunting($"The {animal.Name} escaped.");
        }
        else
        {
            // Predator or cornered animal - attacks!
            GameDisplay.AddNarrative($"The {animal.Name} attacks!");
            _player.IsEngaged = true;
            animal.IsEngaged = true;
            // Combat will be handled by existing combat system
            StopHunting($"Combat initiated with {animal.Name}!");
        }
    }

    #endregion
}
