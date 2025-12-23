namespace text_survival.Web.Dto;

/// <summary>
/// Response from client to server via WebSocket.
/// Sent when player makes a selection or acknowledges a prompt.
/// </summary>
public record PlayerResponse(int? ChoiceIndex);
