namespace text_survival.Web.Dto;

/// <summary>
/// Response from client to server via WebSocket.
/// Sent when player makes a selection, acknowledges a prompt, or clicks a tile.
/// </summary>
public record PlayerResponse(
    int? ChoiceIndex,
    // Grid movement fields
    string? Type = null,      // "select", "move", "hazard_choice", "action"
    int? TargetX = null,      // For "move" type
    int? TargetY = null,      // For "move" type
    bool? QuickTravel = null, // For "hazard_choice" type (true = quick, false = careful)
    string? Action = null     // For "action" type: "inventory", "crafting"
);
