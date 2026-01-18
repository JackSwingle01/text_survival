using text_survival.Combat;
using text_survival.Environments.Grid;

namespace text_survival.Desktop.Dto;

/// <summary>
/// Response from client to server via WebSocket.
/// Sent when player makes a selection, acknowledges a prompt, or clicks a tile.
/// </summary>
public record PlayerResponse(
    string? ChoiceId,         // String-based choice identity for reliable button matching
    int InputId,              // Must match the input request ID to prevent stale responses
    // Grid movement fields
    string? Type = null,      // "select", "move", "hazard_choice", "action", "examine", "travel_to", "transfer"
    int? TargetX = null,      // For "move"/"travel_to" type
    int? TargetY = null,      // For "move"/"travel_to" type
    bool? QuickTravel = null, // For "hazard_choice" type (true = quick, false = careful)
    string? Action = null,    // For "action" type: "inventory", "crafting"
    string? DetailId = null,  // For "examine" type: environmental detail ID
    // Combat action fields
    CombatActions? CombatAction = null,   // Typed combat action (Attack, Dodge, etc.)
    GridPosition? CombatMoveTarget = null, // Click-to-move target position
    // Transfer fields
    string? TransferItemId = null,  // For "transfer" type: item ID to transfer
    int? TransferCount = null,      // For "transfer" type: how many to transfer (1 or all)
    // Fire management fields
    string? FuelItemId = null,      // For "fire" type: fuel to add (tending mode)
    int? FuelCount = null,          // For "fire" type: how many units
    string? FireToolId = null,      // For "fire" type: selected tool (starting mode)
    string? TinderId = null,        // For "fire" type: selected tinder (starting mode)
    bool? CollectCharcoal = null,   // For "fire" type: collect charcoal from fire pit
    string? EmberCarrierId = null   // For "fire" type: light ember carrier (e.g., "ember_0")
);
