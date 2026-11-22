using System.Text.Json.Serialization;

namespace Survival;

/// What an agent is doing. The player's UI and NPC AI both just pick one of these; Sim.Step runs it.
/// Saved while an NPC or herd is part-way through one (Agent.Doing): a new activity type needs its
/// line here, or saving a world where someone is doing it throws.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Wait), "wait")]
[JsonDerivedType(typeof(Rest), "rest")]
[JsonDerivedType(typeof(Sleep), "sleep")]
[JsonDerivedType(typeof(StartFire), "startfire")]
[JsonDerivedType(typeof(AddFuel), "addfuel")]
[JsonDerivedType(typeof(LightTorch), "lighttorch")]
[JsonDerivedType(typeof(GatherCharcoal), "gathercharcoal")]
[JsonDerivedType(typeof(Gather), "gather")]
[JsonDerivedType(typeof(Butcher), "butcher")]
[JsonDerivedType(typeof(Eat), "eat")]
[JsonDerivedType(typeof(Drink), "drink")]
[JsonDerivedType(typeof(Cook), "cook")]
[JsonDerivedType(typeof(MeltSnow), "meltsnow")]
[JsonDerivedType(typeof(Stash), "stash")]
[JsonDerivedType(typeof(Unstash), "unstash")]
[JsonDerivedType(typeof(Wear), "wear")]
[JsonDerivedType(typeof(TakeOff), "takeoff")]
[JsonDerivedType(typeof(Fight), "fight")]
[JsonDerivedType(typeof(Travel), "travel")]
[JsonDerivedType(typeof(Graze), "graze")]
[JsonDerivedType(typeof(Feed), "feed")]
[JsonDerivedType(typeof(Hunt), "hunt")]
[JsonDerivedType(typeof(Raid), "raid")]
[JsonDerivedType(typeof(Build), "build")]
[JsonDerivedType(typeof(ImproveShelter), "improveshelter")]
[JsonDerivedType(typeof(PackTent), "packtent")]
[JsonDerivedType(typeof(Make), "make")]
[JsonDerivedType(typeof(Abandon), "abandon")]
public abstract record Activity(AgentId Who);

public sealed record Wait(AgentId Who) : Activity(Who);
public sealed record Rest(AgentId Who) : Activity(Who);
public sealed record Sleep(AgentId Who) : Activity(Who);

// Fire activities act on the first minute; the agent stays by the fire for the rest. What goes
// on the fire, and the torch, come out of the agent's pack.
public sealed record StartFire(AgentId Who, ToolType Tool, Resource Tinder, double TinderKg, double KindlingKg, PitType Pit = PitType.Open) : Activity(Who);
public sealed record AddFuel(AgentId Who, Resource Fuel, double Kg) : Activity(Who);
public sealed record LightTorch(AgentId Who) : Activity(Who);
public sealed record GatherCharcoal(AgentId Who) : Activity(Who);

// Timed work stops itself when done; the instant ones act on the first minute.

/// Works the Pool-th pool on the agent's tile (Pools.At), whatever kind it is. Focus only matters to foraging.
public sealed record Gather(AgentId Who, int Pool, ForageFocus Focus = ForageFocus.General) : Activity(Who);
/// A carcass keeps the mode it was first butchered in.
public sealed record Butcher(AgentId Who, int Carcass, ButcherMode? Mode = null) : Activity(Who);
public sealed record Eat(AgentId Who, Resource Food, double Kg) : Activity(Who);
/// From the pack's water, or from a river or lake here when the pack has none.
public sealed record Drink(AgentId Who, double Liters = Work.MaxDrinkLiters) : Activity(Who);
public sealed record Cook(AgentId Who, Resource Raw, double Kg) : Activity(Who);
public sealed record MeltSnow(AgentId Who) : Activity(Who);
public sealed record Stash(AgentId Who, Resource Resource, double Kg) : Activity(Who);
public sealed record Unstash(AgentId Who, Resource Resource, double Kg) : Activity(Who);
/// What that slot held goes back in the pack.
public sealed record Wear(AgentId Who, string Gear) : Activity(Who);
public sealed record TakeOff(AgentId Who, string Gear) : Activity(Who);

/// One round: the agent's action, a minute lived, then everyone else's turn. Target indexes Encounter.Units;
/// To is the cell for a Move.
public sealed record Fight(AgentId Who, CombatAction Action, int? Target = null, Cell? To = null) : Activity(Who);

/// One tile north, east, south or west. Repeating it continues a crossing cut short; anything else abandons it.
public sealed record Travel(AgentId Who, Direction Dir, Pace Pace = Pace.Quick) : Activity(Who);

/// A new structure takes its materials at the start; work left off is picked up again. A tent comes from the pack.
public sealed record Build(AgentId Who, Structure What) : Activity(Who);
/// A tenth of a piece a minute, until stopped.
public sealed record ImproveShelter(AgentId Who, ShelterStat Stat, Resource Material) : Activity(Who);
public sealed record PackTent(AgentId Who) : Activity(Who);

/// Works on this recipe (Content/recipes.json): a new project takes its materials at the start; one in the
/// pack is picked up where it was left.
public sealed record Make(AgentId Who, string Recipe) : Activity(Who);
/// Gives up the unfinished one in the pack: most of the materials come back.
public sealed record Abandon(AgentId Who, string Recipe) : Activity(Who);
