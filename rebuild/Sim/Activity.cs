using System.Text.Json.Serialization;

namespace Survival;

/// Saved while an NPC or herd is part-way through one: a new activity type needs its line here, or saving it throws.
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
[JsonDerivedType(typeof(Wash), "wash")]
[JsonDerivedType(typeof(WalkAbout), "walkabout")]
[JsonDerivedType(typeof(PutDown), "putdown")]
[JsonDerivedType(typeof(PickUp), "pickup")]
[JsonDerivedType(typeof(Bury), "bury")]
[JsonDerivedType(typeof(RepairCache), "repaircache")]
[JsonDerivedType(typeof(Wear), "wear")]
[JsonDerivedType(typeof(TakeOff), "takeoff")]
[JsonDerivedType(typeof(Fight), "fight")]
[JsonDerivedType(typeof(Travel), "travel")]
[JsonDerivedType(typeof(Graze), "graze")]
[JsonDerivedType(typeof(Feed), "feed")]
[JsonDerivedType(typeof(Watering), "watering")]
[JsonDerivedType(typeof(Hunt), "hunt")]
[JsonDerivedType(typeof(ImproveShelter), "improveshelter")]
[JsonDerivedType(typeof(PackTent), "packtent")]
[JsonDerivedType(typeof(Make), "make")]
[JsonDerivedType(typeof(Abandon), "abandon")]
[JsonDerivedType(typeof(PutOn), "puton")]
[JsonDerivedType(typeof(TakeFinished), "takefinished")]
[JsonDerivedType(typeof(TakeBack), "takeback")]
[JsonDerivedType(typeof(StopWork), "stopwork")]
public abstract record Activity(AgentId Who);

public sealed record Wait(AgentId Who) : Activity(Who);
public sealed record Rest(AgentId Who) : Activity(Who);
public sealed record Sleep(AgentId Who) : Activity(Who);

public sealed record StartFire(AgentId Who, ToolType Tool, Resource Tinder, double TinderKg, double KindlingKg, PitType Pit = PitType.Open) : Activity(Who);
public sealed record AddFuel(AgentId Who, Resource Fuel, double Kg) : Activity(Who);
public sealed record LightTorch(AgentId Who) : Activity(Who);
public sealed record GatherCharcoal(AgentId Who) : Activity(Who);

public sealed record Gather(AgentId Who, int Pool, ForageFocus Focus = ForageFocus.General) : Activity(Who);
/// A carcass keeps the mode it was first butchered in.
public sealed record Butcher(AgentId Who, int Carcass, ButcherMode? Mode = null) : Activity(Who);
public sealed record Eat(AgentId Who, Resource Food, double Kg) : Activity(Who);
public sealed record Drink(AgentId Who, double Liters = Work.MaxDrinkLiters) : Activity(Who);
public sealed record Wash(AgentId Who) : Activity(Who);
public sealed record WalkAbout(AgentId Who) : Activity(Who);
public sealed record PutDown(AgentId Who, Resource Resource, double Kg, CacheId? Into = null) : Activity(Who);
public sealed record PickUp(AgentId Who, Resource Resource, double Kg, CacheId? From = null) : Activity(Who);
public sealed record Bury(AgentId Who, Resource Resource, double Kg) : Activity(Who);
public sealed record RepairCache(AgentId Who, CacheId Cache) : Activity(Who);
public sealed record Wear(AgentId Who, string Gear) : Activity(Who);
public sealed record TakeOff(AgentId Who, string Gear) : Activity(Who);

public sealed record Fight(AgentId Who, CombatAction Action, int? Target = null, Cell? To = null) : Activity(Who);

/// Repeating it continues a crossing cut short; anything else abandons it.
public sealed record Travel(AgentId Who, Direction Dir, Pace Pace = Pace.Quick) : Activity(Who);

/// A new one takes its materials at the start; work left off takes none when picked up again.
public sealed record ImproveShelter(AgentId Who, ShelterStat Stat, Resource Material) : Activity(Who);
public sealed record PackTent(AgentId Who) : Activity(Who);

/// A new project takes its materials at the start; one in the pack takes none when picked up again. Target: a repair's gear kind.
public sealed record Make(AgentId Who, string Recipe, string? Target = null) : Activity(Who);
public sealed record Abandon(AgentId Who, string Recipe, string? Target = null) : Activity(Who);

/// At a station here: Rack names a rack, null the fire. TakeBack names the very thing put on: its input and kilos; StopWork, a recipe with no input.
public sealed record PutOn(AgentId Who, string Recipe, RackId? Rack = null) : Activity(Who);
public sealed record TakeFinished(AgentId Who, RackId? Rack = null) : Activity(Who);
public sealed record TakeBack(AgentId Who, Resource Input, double Kg, RackId? Rack = null) : Activity(Who);
public sealed record StopWork(AgentId Who, string Recipe) : Activity(Who);
