namespace Survival;

/// What an agent is doing. The player's UI and NPC AI both just pick one of these; Sim.Step runs it.
public abstract record Activity(AgentId Who);

public sealed record Wait(AgentId Who) : Activity(Who);
public sealed record Rest(AgentId Who) : Activity(Who);
public sealed record Sleep(AgentId Who) : Activity(Who);

// Fire activities act on the first minute; the agent stays by the fire for the rest.
public sealed record StartFire(AgentId Who, FireStarter Tool, Fuel Tinder, double TinderKg, double KindlingKg, PitType Pit = PitType.Open) : Activity(Who);
public sealed record AddFuel(AgentId Who, Fuel Fuel, double Kg) : Activity(Who);
public sealed record LightTorch(AgentId Who) : Activity(Who);

/// One tile north, east, south or west. Repeating it continues a crossing cut short; anything else abandons it.
public sealed record Travel(AgentId Who, Direction Dir, Pace Pace = Pace.Quick) : Activity(Who);
