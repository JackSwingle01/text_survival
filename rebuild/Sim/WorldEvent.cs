namespace Survival;

/// A fact the simulation emits. Log, notices, tensions and NPC memory all read the same stream.
public abstract record WorldEvent(long Minute);

public sealed record BecameTired(long Minute, AgentId Who) : WorldEvent(Minute);
public sealed record Exhausted(long Minute, AgentId Who) : WorldEvent(Minute);
public sealed record FullyRested(long Minute, AgentId Who) : WorldEvent(Minute);
