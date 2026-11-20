using System.Text.Json.Serialization;

namespace Survival;

public enum Species { Human } // quadrupeds arrive with their herd minds in phase 3

/// Who decides an agent's next activity. Kind-specific state lives here, never as optional fields on Agent.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(PlayerMind), "player")]
public abstract record Mind;

/// The UI picks the activities.
public sealed record PlayerMind : Mind;

/// One or more bodies that move and decide as a unit: the player, an NPC, a herd (plan decision 2).
public sealed class Agent
{
    public required AgentId Id { get; init; }
    public required TileId Tile { get; set; }
    public Species Species { get; init; } = Species.Human;
    public List<BodyState> Members { get; init; } = [BodyState.BaselineHuman()];
    public Mind Mind { get; init; } = new PlayerMind();
    /// The one body of a one-body agent. Throws for a herd, which has no single body.
    [JsonIgnore] public BodyState Body => Members.Single();
    /// A tile crossing under way; the agent stays on Tile until it completes. Owned by Travel.
    public Crossing? Crossing { get; set; }
    /// Whole-body clothing insulation and its weight. ponytail: two numbers until items exist; then derive from worn gear.
    public double ClothingClo { get; set; } = 2.0;
    public double ClothingWeightKg { get; set; } = 3.0;
}
