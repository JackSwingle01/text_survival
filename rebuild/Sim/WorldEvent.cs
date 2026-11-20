namespace Survival;

/// A fact the simulation emits. Log, notices, tensions and NPC memory all read the same stream.
public abstract record WorldEvent(long Minute);

public sealed record EffectStarted(long Minute, AgentId Who, EffectKind Kind) : WorldEvent(Minute);
public sealed record EffectEnded(long Minute, AgentId Who, EffectKind Kind) : WorldEvent(Minute);
public sealed record Died(long Minute, AgentId Who) : WorldEvent(Minute);
public sealed record Exhausted(long Minute, AgentId Who) : WorldEvent(Minute);
public sealed record FullyRested(long Minute, AgentId Who) : WorldEvent(Minute);

public sealed record FireLit(long Minute, FireId Fire, TileId Tile, AgentId By) : WorldEvent(Minute);
public sealed record FireStartFailed(long Minute, AgentId Who, TileId Tile, double ChancePct) : WorldEvent(Minute);
public sealed record FuelAdded(long Minute, FireId Fire, AgentId By, Fuel Fuel, double Kg) : WorldEvent(Minute);
public sealed record FireBurnedToEmbers(long Minute, FireId Fire, TileId Tile) : WorldEvent(Minute);
/// Embers gone cold, or a torch burned out.
public sealed record FireDied(long Minute, FireId Fire, TileId Tile) : WorldEvent(Minute);
