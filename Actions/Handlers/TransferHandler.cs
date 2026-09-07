using text_survival.Items;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Pure game logic for inventory transfers between player and storage.
/// UI code calls these methods; the handler returns results for display.
/// </summary>
public static class TransferHandler
{
    /// <summary>
    /// Result of a transfer operation.
    /// </summary>
    public record TransferResult(bool Success, string Message);

    /// <summary>
    /// Transfer a resource stack from source to destination.
    /// </summary>
    public static TransferResult TransferResource(
        Inventory source, Inventory dest, Resource resource, string direction)
    {
        int count = source.Count(resource);
        if (count <= 0)
            return new TransferResult(false, $"No {resource} to transfer.");

        // Preserve individual unit weights instead of merging them into one oversized unit.
        for (int i = 0; i < count; i++)
            dest.Add(resource, source.Pop(resource));
        return new TransferResult(true, $"Moved {resource} x{count} {direction}");
    }

    /// <summary>
    /// Transfer a tool from source to destination.
    /// </summary>
    public static TransferResult TransferTool(
        Inventory source, Inventory dest, Gear tool, string direction)
    {
        if (!source.Tools.Remove(tool))
            return new TransferResult(false, $"Could not find {tool.Name} to transfer.");

        dest.Tools.Add(tool);
        return new TransferResult(true, $"Moved {tool.Name} {direction}");
    }

    /// <summary>
    /// Transfer equipment from source to destination.
    /// Finds the equipment slot and moves the item.
    /// </summary>
    public static TransferResult TransferEquipment(
        Inventory source, Inventory dest, Gear equipment, string direction)
    {
        // Find which slot this equipment is in
        var slot = source.Equipment.FirstOrDefault(kvp => kvp.Value == equipment).Key;
        if (!source.Equipment.Remove(slot))
            return new TransferResult(false, $"Could not find {equipment.Name} to transfer.");

        dest.Equipment[slot] = equipment;
        return new TransferResult(true, $"Moved {equipment.Name} {direction}");
    }

    /// <summary>
    /// Transfer an accessory from source to destination.
    /// </summary>
    public static TransferResult TransferAccessory(
        Inventory source, Inventory dest, Gear accessory, string direction)
    {
        if (!source.Accessories.Remove(accessory))
            return new TransferResult(false, $"Could not find {accessory.Name} to transfer.");

        dest.Accessories.Add(accessory);
        return new TransferResult(true, $"Moved {accessory.Name} {direction}");
    }
}
