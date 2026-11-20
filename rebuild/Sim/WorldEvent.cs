namespace Survival;

/// A fact the simulation emits. Log, notices, tensions and NPC memory all read the same stream.
/// Minute is the instant it happened: an agent's action (lighting, adding fuel) happens at the
/// step's starting world.Minute, before that step's first minute is lived. Consumers should rely
/// on stream order, not on Minute being strictly greater than the previous step's last minute.
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

/// A crossing completed: the agent now stands on To. Tracks and trail wear will read this.
public sealed record Moved(long Minute, AgentId Who, TileId From, TileId To) : WorldEvent(Minute);
public sealed record Injured(long Minute, AgentId Who, TileId Tile, string Region, DamageType Type, double Amount) : WorldEvent(Minute);
/// Reached the far side of the mountain pass: the run is won.
public sealed record CrossedThePass(long Minute, AgentId Who) : WorldEvent(Minute);
