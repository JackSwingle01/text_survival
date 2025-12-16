using text_survival.Actors.NPCs;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;

namespace text_survival.Actors.Player;

/// <summary>
/// Manages ranged hunting mechanics including shooting, accuracy, and damage.
/// Integrates with AmmunitionManager and Body.Damage() system.
/// </summary>
public class HuntingManager
{
    private readonly Player _player;
    private readonly AmmunitionManager _ammunitionManager;

    public HuntingManager(Player player, AmmunitionManager ammunitionManager)
    {
        _player = player;
        _ammunitionManager = ammunitionManager;
    }

    /// <summary>
    /// Attempts to shoot a target animal with a ranged weapon.
    /// Handles accuracy calculation, damage, arrow consumption, and recovery.
    /// </summary>
    /// <param name="target">The animal to shoot</param>
    /// <param name="targetBodyPart">Optional body part to target (null for torso)</param>
    /// <returns>True if shot was successful (hit or miss), false if couldn't shoot</returns>
    public bool ShootTarget(Animal target, Location location, DateTime currentTime, string? targetBodyPart = null)
    {
        // Verify can shoot
        if (!_ammunitionManager.CanShoot(out string reason))
        {
            Output.WriteLine(reason);
            return false;
        }

        RangedWeapon bow = (RangedWeapon)_player.inventoryManager.Weapon;
        Item? arrow = _ammunitionManager.GetBestAvailableArrow();

        if (arrow == null)
        {
            Output.WriteLine("You have no arrows!");
            return false;
        }

        // Consume arrow
        if (!_ammunitionManager.ConsumeArrow(arrow))
        {
            return false;
        }

        Output.WriteLine($"You nock your {arrow.Name} and draw back the bowstring...");
        Thread.Sleep(500);

        // Calculate accuracy
        int huntingSkill = _player.Skills.GetSkill("Hunting").Level;
        bool isConcealed = _player.stealthManager.IsHunting && target.State != AnimalState.Detected;

        double hitChance = HuntingCalculator.CalculateRangedAccuracy(
            target.DistanceFromPlayer,
            bow.BaseAccuracy,
            huntingSkill,
            isConcealed
        );

        // Roll for hit
        double hitRoll = Utils.RandDouble(0, 1);
        bool hit = hitRoll < hitChance;

        if (!hit)
        {
            Output.WriteLine($"Your arrow flies wide, missing the {target.Name}!");
            Output.WriteLine($"Hit chance was {hitChance * 100:F0}% (rolled {hitRoll * 100:F0}%)");

            // Award small XP for attempt
            _player.Skills.GetSkill("Hunting").GainExperience(1);

            // Try to recover arrow
            _ammunitionManager.AttemptArrowRecovery(false, arrow, target.Name);

            // Animal detects player if missed
            if (target.State != AnimalState.Detected)
            {
                target.BecomeDetected();
                Output.WriteLine($"The {target.Name} is alerted by your miss!");

                // Handle detection response
                if (target.ShouldFlee(_player))
                {
                    Output.WriteLine($"The {target.Name} flees!");
                    location?.RemoveNpc(target);
                    _player.stealthManager.StopHunting($"The {target.Name} escaped.");
                }
                else
                {
                    Output.WriteLine($"The {target.Name} attacks!");
                    _player.IsEngaged = true;
                    target.IsEngaged = true;
                    _player.stealthManager.StopHunting($"Combat initiated with {target.Name}!");
                }
            }

            return true;
        }

        // HIT!
        Output.WriteLine($"Your arrow strikes true!");
        Output.WriteLine($"Hit chance was {hitChance * 100:F0}% (rolled {hitRoll * 100:F0}%)");

        // Calculate damage
        double baseDamage = bow.Damage;
        double arrowModifier = _ammunitionManager.GetArrowDamageModifier(arrow);
        double finalDamage = baseDamage * arrowModifier;

        // Determine hit location (default to torso if not specified)
        string hitLocation = targetBodyPart ?? "torso";

        // Apply damage through Body system
        var damageInfo = new DamageInfo
        {
            Amount = finalDamage,
            Type = DamageType.Pierce, // Arrows pierce
            TargetPartName = hitLocation,
            Source = $"{_player.Name}'s {arrow.Name}"
        };

        target.Body.Damage(damageInfo);

        // Award XP for successful hit
        int xpReward = target.IsAlive ? 3 : 5; // More XP for kill
        _player.Skills.GetSkill("Hunting").GainExperience(xpReward);

        if (!target.IsAlive)
        {
            Output.WriteLine($"The {target.Name} collapses, dead!");
            Output.WriteLine($"You gain {xpReward} Hunting XP.");

            // Try to recover arrow from corpse
            _ammunitionManager.AttemptArrowRecovery(true, arrow, target.Name);

            // End hunting session
            _player.stealthManager.StopHunting($"You successfully hunted the {target.Name}.");
        }
        else
        {
            Output.WriteLine($"The {target.Name} is wounded!");
            Output.WriteLine($"You gain {xpReward} Hunting XP.");

            // Try to recover arrow
            _ammunitionManager.AttemptArrowRecovery(true, arrow, target.Name);

            // Wounded animal response
            if (target.State != AnimalState.Detected)
            {
                target.BecomeDetected();
            }

            if (target.ShouldFlee(_player))
            {
                Output.WriteLine($"The wounded {target.Name} flees!");

                // Create blood trail (Phase 4)
                double woundSeverity = CalculateWoundSeverity(target, finalDamage);
                var currentLocation = location;
                var bloodTrail = new BloodTrail(target, currentLocation, woundSeverity, currentTime);
                currentLocation.BloodTrails.Add(bloodTrail);

                // Mark animal as bleeding for bleed-out tracking
                target.IsBleeding = true;
                target.WoundedTime = currentTime;
                target.CurrentWoundSeverity = woundSeverity;

                Output.WriteLine($"The {target.Name} leaves a blood trail behind...");
                Output.WriteLine(bloodTrail.GetSeverityDescription());

                location.RemoveNpc(target);
                _player.stealthManager.StopHunting($"The wounded {target.Name} escaped. You could try tracking it...");
            }
            else
            {
                Output.WriteLine($"The wounded {target.Name} attacks in desperation!");
                _player.IsEngaged = true;
                target.IsEngaged = true;
                _player.stealthManager.StopHunting($"Combat initiated with wounded {target.Name}!");
            }
        }

        return true;
    }

    /// <summary>
    /// Calculates wound severity based on damage dealt vs target's max health.
    /// Returns value 0.0 to 1.0 for blood trail intensity.
    /// </summary>
    private double CalculateWoundSeverity(Animal target, double damageDealt)
    {
        // Severity based on percentage of max health lost
        double healthPercentageLost = damageDealt / target.Body.MaxHealth;

        // Also factor in current health status
        double currentHealthPercent = target.Body.Health / target.Body.MaxHealth;

        // Severe wounds = high damage relative to max health
        // Critical wounds = low remaining health
        double severity = (healthPercentageLost * 0.6) + ((1.0 - currentHealthPercent) * 0.4);

        return Math.Clamp(severity, 0.1, 1.0); // Minimum 0.1 (always some blood)
    }

    /// <summary>
    /// Gets info about player's current ranged weapon setup.
    /// </summary>
    public string GetRangedWeaponInfo()
    {
        if (_player.inventoryManager.Weapon is not RangedWeapon bow)
        {
            return "No ranged weapon equipped.";
        }

        int arrowCount = _ammunitionManager.GetAmmunitionCount("Arrow");
        Item? bestArrow = _ammunitionManager.GetBestAvailableArrow();

        string info = $"Weapon: {bow.Name}\n";
        info += $"Effective Range: {bow.EffectiveRange:F0}m\n";
        info += $"Max Range: {bow.MaxRange:F0}m\n";
        info += $"Arrows: {arrowCount}";

        if (bestArrow != null)
        {
            info += $" (Best: {bestArrow.Name})";
        }

        return info;
    }
}
