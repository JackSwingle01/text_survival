using text_survival.Crafting;
using text_survival.IO;
using text_survival.Items;

namespace text_survival.Actors.Player;

/// <summary>
/// Manages ammunition inventory for ranged weapons.
/// Tracks arrows, consumes on shot, and handles arrow recovery from kills.
/// </summary>
public class AmmunitionManager
{
    private readonly InventoryManager _inventoryManager;

    public AmmunitionManager(InventoryManager inventoryManager)
    {
        _inventoryManager = inventoryManager;
    }

    /// <summary>
    /// Gets count of ammunition items of specified type in inventory.
    /// </summary>
    public int GetAmmunitionCount(string ammunitionType = "Arrow")
    {
        return _inventoryManager.Items
            .Where(stack => stack.FirstItem.CraftingProperties.Contains(ItemProperty.Ammunition))
            .Where(stack => stack.FirstItem.Name.Contains(ammunitionType))
            .Sum(stack => stack.Count);
    }

    /// <summary>
    /// Gets the best available arrow from inventory (highest tier).
    /// Priority: Obsidian > Bone > Flint > Stone
    /// </summary>
    public Item? GetBestAvailableArrow()
    {
        var arrows = _inventoryManager.Items
            .Where(stack => stack.FirstItem.CraftingProperties.Contains(ItemProperty.Ammunition))
            .Where(stack => stack.FirstItem.Name.Contains("Arrow"))
            .Select(stack => stack.FirstItem)
            .ToList();

        if (!arrows.Any())
            return null;

        // Priority order: Obsidian > Bone > Flint > Stone
        var obsidian = arrows.FirstOrDefault(a => a.Name.Contains("Obsidian"));
        if (obsidian != null) return obsidian;

        var bone = arrows.FirstOrDefault(a => a.Name.Contains("Bone"));
        if (bone != null) return bone;

        var flint = arrows.FirstOrDefault(a => a.Name.Contains("Flint"));
        if (flint != null) return flint;

        return arrows.FirstOrDefault(a => a.Name.Contains("Stone"));
    }

    /// <summary>
    /// Consumes one arrow from inventory for shooting.
    /// </summary>
    /// <returns>True if arrow was consumed, false if no arrows available</returns>
    public bool ConsumeArrow(Item arrow)
    {
        var hasArrow = _inventoryManager.Items
            .Any(stack => stack.FirstItem == arrow && stack.Count > 0);

        if (!hasArrow)
        {
            Output.WriteLine("You have no arrows!");
            return false;
        }

        // Remove one arrow from inventory
        _inventoryManager.Items
            .First(stack => stack.FirstItem == arrow)
            .Pop();

        return true;
    }

    /// <summary>
    /// Attempts to recover arrows from a killed animal's corpse.
    /// Recovery chance depends on shot quality and arrow type.
    /// </summary>
    /// <param name="shotHit">Whether the shot hit the target</param>
    /// <param name="arrow">The arrow that was fired</param>
    /// <param name="targetName">Name of the target (for output messages)</param>
    /// <returns>Number of arrows recovered</returns>
    public int AttemptArrowRecovery(bool shotHit, Item arrow, string targetName)
    {
        if (!shotHit)
        {
            // Missed shot - 30% chance to find arrow
            if (Utils.DetermineSuccess(0.30))
            {
                Output.WriteLine($"You recover your {arrow.Name} from the ground.");
                _inventoryManager.AddToInventory(arrow);
                return 1;
            }
            else
            {
                Output.WriteLine($"Your {arrow.Name} is lost in the undergrowth.");
                return 0;
            }
        }
        else
        {
            // Hit target - 60% chance to recover from corpse
            if (Utils.DetermineSuccess(0.60))
            {
                Output.WriteLine($"You recover your {arrow.Name} from the {targetName}'s corpse.");
                _inventoryManager.AddToInventory(arrow);
                return 1;
            }
            else
            {
                Output.WriteLine($"Your {arrow.Name} broke on impact and cannot be recovered.");
                return 0;
            }
        }
    }

    /// <summary>
    /// Gets arrow damage modifier based on arrow type.
    /// Better arrows deal more damage.
    /// </summary>
    public double GetArrowDamageModifier(Item arrow)
    {
        if (arrow.Name.Contains("Obsidian"))
            return 1.4; // +40% damage
        else if (arrow.Name.Contains("Bone"))
            return 1.2; // +20% damage
        else if (arrow.Name.Contains("Flint"))
            return 1.1; // +10% damage
        else
            return 1.0; // Stone arrows - base damage
    }

    /// <summary>
    /// Checks if player has a ranged weapon equipped and ammunition available.
    /// </summary>
    public bool CanShoot(out string reason)
    {
        reason = "";

        // Check if weapon is ranged
        if (_inventoryManager.Weapon is not RangedWeapon rangedWeapon)
        {
            reason = "You need a ranged weapon equipped (bow).";
            return false;
        }

        // Check if ammunition is available
        int arrowCount = GetAmmunitionCount("Arrow");
        if (arrowCount == 0)
        {
            reason = "You have no arrows!";
            return false;
        }

        return true;
    }
}
