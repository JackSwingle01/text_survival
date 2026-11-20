namespace Survival;

/// What an agent is doing. The player's UI and NPC AI both just pick one of these; Sim.Step runs it.
public abstract record Activity(AgentId Who);

public sealed record Wait(AgentId Who) : Activity(Who);
public sealed record Rest(AgentId Who) : Activity(Who);
