namespace Survival;

/// What an agent is doing. The player's UI and NPC AI both just pick one of these; Sim.Step runs it.
public abstract record Activity(AgentId Who);

public sealed record Wait(AgentId Who) : Activity(Who);
public sealed record Rest(AgentId Who) : Activity(Who);
public sealed record Sleep(AgentId Who) : Activity(Who);

// Fire activities act on the first minute; the agent stays by the fire for the rest. What goes
// on the fire, and the torch, come out of the agent's pack.
public sealed record StartFire(AgentId Who, ToolType Tool, Resource Tinder, double TinderKg, double KindlingKg, PitType Pit = PitType.Open) : Activity(Who);
public sealed record AddFuel(AgentId Who, Resource Fuel, double Kg) : Activity(Who);
public sealed record LightTorch(AgentId Who) : Activity(Who);
/// Takes the charcoal a dead fire on this tile left behind.
public sealed record GatherCharcoal(AgentId Who) : Activity(Who);

// Work (Work.cs). Timed work stops itself when done; the instant ones act on the first minute.

/// Works the Pool-th pool on the agent's tile (Pools.At): forages, chops, harvests or salvages,
/// whichever kind of pool it is. Focus only matters to foraging.
public sealed record Gather(AgentId Who, int Pool, ForageFocus Focus = ForageFocus.General) : Activity(Who);
/// Butchers a carcass on this tile. A carcass keeps the mode it was first butchered in.
public sealed record Butcher(AgentId Who, int Carcass, ButcherMode? Mode = null) : Activity(Who);
public sealed record Eat(AgentId Who, Resource Food, double Kg) : Activity(Who);
/// From the pack's water, or from a river or lake here when the pack has none.
public sealed record Drink(AgentId Who, double Liters = Work.MaxDrinkLiters) : Activity(Who);
/// Raw meat or fish to cooked, at a burning fire.
public sealed record Cook(AgentId Who, Resource Raw, double Kg) : Activity(Who);
public sealed record MeltSnow(AgentId Who) : Activity(Who);
/// Moves resources between the pack and this tile's stash.
public sealed record Stash(AgentId Who, Resource Resource, double Kg) : Activity(Who);
public sealed record Unstash(AgentId Who, Resource Resource, double Kg) : Activity(Who);
/// Puts on a garment from the pack; what that slot held goes back in the pack.
public sealed record Wear(AgentId Who, string Gear) : Activity(Who);

/// One round of the fight the agent is in: its action, then a minute lived, then everyone else's
/// turn (Combat). Target indexes Encounter.Units; To is the cell for a Move.
public sealed record Fight(AgentId Who, CombatAction Action, int? Target = null, Cell? To = null) : Activity(Who);

/// One tile north, east, south or west. Repeating it continues a crossing cut short; anything else abandons it.
public sealed record Travel(AgentId Who, Direction Dir, Pace Pace = Pace.Quick) : Activity(Who);
