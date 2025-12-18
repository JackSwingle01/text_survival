using text_survival.Actors.NPCs;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actors.Player;

/// <summary>
/// Manages ranged hunting mechanics including shooting, accuracy, and damage.
/// </summary>
public class HuntingManager
{
    private readonly Player _player;

    public HuntingManager(Player player)
    {
        _player = player;
    }

    /// <summary>
    /// Attempts to shoot a target animal with a ranged weapon.
    /// Currently stubbed - ranged hunting not yet implemented with new inventory.
    /// </summary>
    public bool ShootTarget(Animal target, Location location, DateTime currentTime, Inventory inventory, string? targetBodyPart = null)
    {
        // Check for ranged weapon in inventory
        var weapon = inventory.Weapon;
        if (weapon == null || !weapon.IsWeapon)
        {
            GameDisplay.AddNarrative("You need a weapon to hunt.");
            return false;
        }

        // For now, only melee hunting is supported
        // TODO: Implement ranged hunting with arrows stored in Inventory.Special
        GameDisplay.AddNarrative("Ranged hunting not yet implemented. Use stealth approach for melee.");
        return false;
    }

    /// <summary>
    /// Gets info about player's current weapon setup.
    /// </summary>
    public string GetWeaponInfo(Inventory inventory)
    {
        var weapon = inventory.Weapon;
        if (weapon == null)
        {
            return "No weapon equipped.";
        }

        return $"Weapon: {weapon.Name} (Damage: {weapon.Damage ?? 0}, Accuracy: {weapon.Accuracy ?? 0:F1})";
    }
}
