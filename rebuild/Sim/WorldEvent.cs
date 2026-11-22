using System.Text.Json.Serialization;

namespace Survival;

/// A fact the simulation emits; log, notices, tile journals and memory all read the same stream.
/// Minute is when it really happened: an agent's action carries the step's starting minute, lazy
/// catch-up carries the minute it occurred, not when someone looked. Rely on stream order within a
/// minute, not on Minute exceeding the previous step's last minute. Every event has a Tile so it can be filed
/// in that tile's journal.
/// Saved inside tile journals: a new event type needs its line here, or filing it throws.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(EffectStarted), "effectstarted")]
[JsonDerivedType(typeof(EffectEnded), "effectended")]
[JsonDerivedType(typeof(Died), "died")]
[JsonDerivedType(typeof(Exhausted), "exhausted")]
[JsonDerivedType(typeof(FullyRested), "fullyrested")]
[JsonDerivedType(typeof(FireLit), "firelit")]
[JsonDerivedType(typeof(FireStartFailed), "firestartfailed")]
[JsonDerivedType(typeof(FuelAdded), "fueladded")]
[JsonDerivedType(typeof(FireBurnedToEmbers), "fireembers")]
[JsonDerivedType(typeof(FireDied), "firedied")]
[JsonDerivedType(typeof(Moved), "moved")]
[JsonDerivedType(typeof(Injured), "injured")]
[JsonDerivedType(typeof(CrossedThePass), "crossed")]
[JsonDerivedType(typeof(Gathered), "gathered")]
[JsonDerivedType(typeof(Dropped), "dropped")]
[JsonDerivedType(typeof(Stashed), "stashed")]
[JsonDerivedType(typeof(Unstashed), "unstashed")]
[JsonDerivedType(typeof(PoolDiscovered), "pooldiscovered")]
[JsonDerivedType(typeof(TreeFelled), "treefelled")]
[JsonDerivedType(typeof(Ate), "ate")]
[JsonDerivedType(typeof(Drank), "drank")]
[JsonDerivedType(typeof(Butchered), "butchered")]
[JsonDerivedType(typeof(Killed), "killed")]
[JsonDerivedType(typeof(EncounterEnded), "encounterended")]
[JsonDerivedType(typeof(WoundedEscaped), "woundedescaped")]
[JsonDerivedType(typeof(Built), "built")]
[JsonDerivedType(typeof(ShelterCollapsed), "sheltercollapsed")]
[JsonDerivedType(typeof(PlanRefused), "planrefused")]
[JsonDerivedType(typeof(SkillLeveledUp), "skillup")]
[JsonDerivedType(typeof(Noticed), "noticed")]
[JsonDerivedType(typeof(Made), "made")]
[JsonDerivedType(typeof(ProjectAbandoned), "projectabandoned")]
public abstract record WorldEvent(long Minute, TileId Tile);

public sealed record EffectStarted(long Minute, AgentId Who, TileId Tile, EffectKind Kind) : WorldEvent(Minute, Tile);
public sealed record EffectEnded(long Minute, AgentId Who, TileId Tile, EffectKind Kind) : WorldEvent(Minute, Tile);
public sealed record Died(long Minute, AgentId Who, TileId Tile) : WorldEvent(Minute, Tile);
public sealed record Exhausted(long Minute, AgentId Who, TileId Tile) : WorldEvent(Minute, Tile);
public sealed record FullyRested(long Minute, AgentId Who, TileId Tile) : WorldEvent(Minute, Tile);

public sealed record FireLit(long Minute, FireId Fire, TileId Tile, AgentId By) : WorldEvent(Minute, Tile);
public sealed record FireStartFailed(long Minute, AgentId Who, TileId Tile, double ChancePct) : WorldEvent(Minute, Tile);
public sealed record FuelAdded(long Minute, FireId Fire, TileId Tile, AgentId By, Resource Fuel, double Kg) : WorldEvent(Minute, Tile);
public sealed record FireBurnedToEmbers(long Minute, FireId Fire, TileId Tile) : WorldEvent(Minute, Tile);
/// Embers gone cold, or a torch burned out.
public sealed record FireDied(long Minute, FireId Fire, TileId Tile) : WorldEvent(Minute, Tile);

/// A crossing completed: the agent now stands on To, which is the event's Tile.
public sealed record Moved(long Minute, AgentId Who, TileId From, TileId To) : WorldEvent(Minute, To);
public sealed record Injured(long Minute, AgentId Who, TileId Tile, string Region, DamageType Type, double Amount) : WorldEvent(Minute, Tile);
public sealed record CrossedThePass(long Minute, AgentId Who, TileId Tile) : WorldEvent(Minute, Tile);

public sealed record Gathered(long Minute, AgentId Who, TileId Tile, string What) : WorldEvent(Minute, Tile);
/// More than the pack could hold: the rest went on the tile's stash.
public sealed record Dropped(long Minute, AgentId Who, TileId Tile, string What) : WorldEvent(Minute, Tile);
public sealed record Stashed(long Minute, AgentId Who, TileId Tile, Resource Resource, double Kg) : WorldEvent(Minute, Tile);
/// Taken: by someone whose camp this was not, as it stood when they took it (Journal.IsNotable).
public sealed record Unstashed(long Minute, AgentId Who, TileId Tile, Resource Resource, double Kg, bool Taken) : WorldEvent(Minute, Tile);
/// A hidden pool found by searching. Only this agent now knows it.
public sealed record PoolDiscovered(long Minute, AgentId Who, PoolId Pool, string Name) : WorldEvent(Minute, Pool.Tile);
public sealed record TreeFelled(long Minute, AgentId Who, PoolId Pool) : WorldEvent(Minute, Pool.Tile);
public sealed record Ate(long Minute, AgentId Who, TileId Tile, Resource Food, double Kg, double Kcal) : WorldEvent(Minute, Tile);
public sealed record Drank(long Minute, AgentId Who, TileId Tile, double Liters) : WorldEvent(Minute, Tile);
public sealed record Butchered(long Minute, AgentId Who, TileId Tile, int Carcass, bool Finished) : WorldEvent(Minute, Tile);

/// An animal member died on Tile: a carcass of this species and mass. By struck the blow; null if it bled out.
public sealed record Killed(long Minute, AgentId Agent, Species Species, double MassKg, TileId Tile, AgentId? By) : WorldEvent(Minute, Tile);
/// Result is from this agent's side; Rounds is minutes lasted.
public sealed record EncounterEnded(long Minute, AgentId Agent, TileId Tile, CombatResult Result, int Rounds) : WorldEvent(Minute, Tile);
/// A wounded animal left its herd as Agent: a trail to follow. Heading is the way it ran; null if nowhere in particular.
public sealed record WoundedEscaped(long Minute, AgentId Agent, Species Species, double Severity, TileId Tile,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Direction? Heading = null)
    : WorldEvent(Minute, Tile);

public sealed record SkillLeveledUp(long Minute, AgentId Who, TileId Tile, Skill Skill, int Level) : WorldEvent(Minute, Tile);
/// What changed on a remembered tile since the player last saw it (Memories.Receipt).
/// Never filed in a journal: it is a reading of one.
public sealed record Noticed(long Minute, AgentId Who, TileId Tile, Receipt Receipt) : WorldEvent(Minute, Tile);
