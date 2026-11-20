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
public sealed record FuelAdded(long Minute, FireId Fire, AgentId By, Resource Fuel, double Kg) : WorldEvent(Minute);
public sealed record FireBurnedToEmbers(long Minute, FireId Fire, TileId Tile) : WorldEvent(Minute);
/// Embers gone cold, or a torch burned out.
public sealed record FireDied(long Minute, FireId Fire, TileId Tile) : WorldEvent(Minute);

/// A crossing completed: the agent now stands on To. Tracks and trail wear will read this.
public sealed record Moved(long Minute, AgentId Who, TileId From, TileId To) : WorldEvent(Minute);
public sealed record Injured(long Minute, AgentId Who, TileId Tile, string Region, DamageType Type, double Amount) : WorldEvent(Minute);
/// Reached the far side of the mountain pass: the run is won.
public sealed record CrossedThePass(long Minute, AgentId Who) : WorldEvent(Minute);

/// What one step of work brought in, all of it, summarised (Items.Describe).
public sealed record Gathered(long Minute, AgentId Who, TileId Tile, string What) : WorldEvent(Minute);
/// More than the pack could hold: the rest went on the tile's stash.
public sealed record Dropped(long Minute, AgentId Who, TileId Tile, string What) : WorldEvent(Minute);
/// A hidden pool found by searching. Only this agent now knows it.
public sealed record PoolDiscovered(long Minute, AgentId Who, PoolId Pool, string Name) : WorldEvent(Minute);
public sealed record TreeFelled(long Minute, AgentId Who, PoolId Pool) : WorldEvent(Minute);
public sealed record Ate(long Minute, AgentId Who, Resource Food, double Kg, double Kcal) : WorldEvent(Minute);
public sealed record Drank(long Minute, AgentId Who, double Liters) : WorldEvent(Minute);
public sealed record Butchered(long Minute, AgentId Who, int Carcass, bool Finished) : WorldEvent(Minute);

/// An animal member died on Tile: a carcass of this species and mass. By struck the blow; null if it bled out.
public sealed record Killed(long Minute, AgentId Agent, Species Species, double MassKg, TileId Tile, AgentId? By) : WorldEvent(Minute);
/// A fight is over. Result is from this agent's side; Rounds is how many minutes it lasted.
public sealed record FightEnded(long Minute, AgentId Agent, TileId Tile, CombatResult Result, int Rounds) : WorldEvent(Minute);
/// A wounded animal got away from the player and left its herd as Agent: a trail to follow.
public sealed record WoundedEscaped(long Minute, AgentId Agent, Species Species, double Severity, TileId Tile) : WorldEvent(Minute);
