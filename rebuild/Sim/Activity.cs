using System.Text.Json.Serialization;

namespace Survival;

/// Saved while an NPC or herd is part-way through one: a new activity type needs its line here, or saving a
/// world where someone is doing it throws.
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
[JsonDerivedType(typeof(WalkAbout), "walkabout")]
[JsonDerivedType(typeof(PutDown), "putdown")]
[JsonDerivedType(typeof(PickUp), "pickup")]
[JsonDerivedType(typeof(Wear), "wear")]
[JsonDerivedType(typeof(TakeOff), "takeoff")]
[JsonDerivedType(typeof(Fight), "fight")]
[JsonDerivedType(typeof(Travel), "travel")]
[JsonDerivedType(typeof(Graze), "graze")]
[JsonDerivedType(typeof(Feed), "feed")]
[JsonDerivedType(typeof(Watering), "watering")]
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

public sealed record StartFire(AgentId Who, ToolType Tool, Resource Tinder, double TinderKg, double KindlingKg, PitType Pit = PitType.Open) : Activity(Who);
public sealed record AddFuel(AgentId Who, Resource Fuel, double Kg) : Activity(Who);
public sealed record LightTorch(AgentId Who) : Activity(Who);
public sealed record GatherCharcoal(AgentId Who) : Activity(Who);

public sealed record Gather(AgentId Who, int Pool, ForageFocus Focus = ForageFocus.General) : Activity(Who);
/// A carcass keeps the mode it was first butchered in.
public sealed record Butcher(AgentId Who, int Carcass, ButcherMode? Mode = null) : Activity(Who);
public sealed record Eat(AgentId Who, Resource Food, double Kg) : Activity(Who);
public sealed record Drink(AgentId Who, double Liters = Work.MaxDrinkLiters) : Activity(Who);
public sealed record Cook(AgentId Who, Resource Raw, double Kg) : Activity(Who);
public sealed record MeltSnow(AgentId Who) : Activity(Who);
public sealed record WalkAbout(AgentId Who) : Activity(Who);
public sealed record PutDown(AgentId Who, Resource Resource, double Kg) : Activity(Who);
public sealed record PickUp(AgentId Who, Resource Resource, double Kg) : Activity(Who);
public sealed record Wear(AgentId Who, string Gear) : Activity(Who);
public sealed record TakeOff(AgentId Who, string Gear) : Activity(Who);

public sealed record Fight(AgentId Who, CombatAction Action, int? Target = null, Cell? To = null) : Activity(Who);

/// Repeating it continues a crossing cut short; anything else abandons it.
public sealed record Travel(AgentId Who, Direction Dir, Pace Pace = Pace.Quick) : Activity(Who);

/// A new one takes its materials at the start; work left off takes none when picked up again.
public sealed record Build(AgentId Who, Structure What) : Activity(Who);
public sealed record ImproveShelter(AgentId Who, ShelterStat Stat, Resource Material) : Activity(Who);
public sealed record PackTent(AgentId Who) : Activity(Who);

/// A new project takes its materials at the start; one in the pack takes none when picked up again.
public sealed record Make(AgentId Who, string Recipe) : Activity(Who);
public sealed record Abandon(AgentId Who, string Recipe) : Activity(Who);
