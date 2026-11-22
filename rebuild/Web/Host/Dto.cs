using System.Text.Json.Serialization;

namespace Survival.Web;

// The view: everything the page draws, as plain data. No Sim type crosses the boundary; names are
// the game's own words, numbers carry their units. Built fresh for every call (ViewBuilder).

/// What the client posts to Act. Exactly one of Verb, Advance or Stop.
/// Verb: an id from the current view (starts it; a combat Move also takes To).
/// Advance: run the job under way for up to this many game minutes.
/// Stop: drop the job under way, keeping what it did so far.
/// Minutes: with a Verb that offers Durations, how long to run it.
public sealed record ActDto(string? Verb = null, int? Advance = null, bool? Stop = null, CellDto? To = null, int? Minutes = null);

public sealed record ViewDto(
    int Gen,
    long Minute,
    ClockDto Clock,
    FeelsDto Feels,
    IReadOnlyList<VitalDto> Vitals,
    IReadOnlyList<AlertDto> Alerts,
    PlaceDto Place,
    FireDto? Fire,
    IReadOnlyList<LocationFeatureDto> LocationFeatures,
    IReadOnlyList<VerbDto> Walk,
    IReadOnlyList<VerbDto> Tile,
    IReadOnlyList<VerbDto> PackVerbs,
    IReadOnlyList<VerbDto> Make,
    PackDto Pack,
    BodyDto Body,
    MapDto Map,
    CombatDto? Combat,
    ForageDto? Forage,
    JobDto? Job,
    ReceiptDto? Receipt,
    IReadOnlyList<LogDto> Log,
    string Outcome,
    string? DeathCause);

/// Day is the run's day number; MinuteOfDay 0-1439. LightFromMinute and DarkFromMinute bound the light
/// to work by (Weather.IsNight); DaylightLeftMinutes counts down to it, 0 at night.
public sealed record ClockDto(int Day, int MinuteOfDay, string Season, string Sky, double AirF, double WindMph, string WindDirection,
    int LightFromMinute, int DarkFromMinute, int DaylightLeftMinutes);

/// What the air feels like on the player: the base air, then each thing that moves it, summing to TotalF.
public sealed record FeelsDto(double TotalF, IReadOnlyList<FeelsTermDto> Terms);
public sealed record FeelsTermDto(string Name, double DeltaF);

/// Stat: warmth, food, water, rest. Pct 0-1 for the bar. Tier from Tiers.Of (none, want, need, critical):
/// the bar's colour. Detail is the hover line.
public sealed record VitalDto(string Stat, double Pct, string Tier, string Detail);

/// Kind: danger, cold or warn. Rank orders them (higher first). Lines: the hover detail.
public sealed record AlertDto(string Id, string Text, string Kind, int Rank, string Icon, IReadOnlyList<string> Lines);

public sealed record PlaceDto(string Name, string Terrain, int X, int Y, bool Camp, bool Dark, string? Story);

public sealed record FireDto(string Phase, bool Burning, double FuelHours, double TemperatureF, double CharcoalKg);

/// One thing on the tile (Verbs.At): its conditions, the action shown for it (or Interact), and its menu.
public sealed record LocationFeatureDto(string Kind, string Name, IReadOnlyList<ConditionDto> Conditions, string? Action, bool Interact,
    IReadOnlyList<VerbDto> Menu);
public sealed record ConditionDto(string Name, bool Blocking);

/// One verb. Id is what the client posts back. Kind is the activity's family, for icons and keys only.
/// Minutes: what it runs for, or null for work that stops itself. Refused: why it cannot start now.
/// Uses: what it takes from the pack, in words, where the verb says (lighting a fire, feeding one).
/// Durations: the minutes it can be run for (ActDto.Minutes), where how long is the player's choice.
public sealed record VerbDto(string Id, string Label, string Kind, int? Minutes, string? Refused,
    OddsDto? Odds = null, TripDto? Trip = null, MakeDto? Make = null, string? Dir = null, bool Careful = false,
    IReadOnlyList<string>? Uses = null, IReadOnlyList<int>? Durations = null);

/// The forage ground here, as the forage screen draws it. Quality: the ground's word, once foraged here. Effort: how
/// hard the player has searched the tile (never what is left). Foci: a verb for each focus on offer, General last, each
/// with its sub-category hint. Clues: what the player notices now; Focus, the offered focus a resource clue points to.
/// Yield: what a minute's search brings against an unhindered one (eyes, axe, shovel, skill, snow over the ground).
public sealed record ForageDto(string? Quality, string Effort, IReadOnlyList<FocusDto> Foci, IReadOnlyList<ClueDto> Clues, string? WalkVerb,
    OddsDto Yield);
public sealed record FocusDto(string Focus, string Hint, string Verb);
public sealed record ClueDto(string Text, string Kind, string? Focus);

/// A bet as its factors (Survival.Odds): start at BasePct, apply each term, clamp. DeltaPct is each
/// term's signed share, so BasePct plus the deltas is RawPct; TotalPct is that clamped.
public sealed record OddsDto(string Base, double BasePct, IReadOnlyList<OddsTermDto> Terms, double RawPct, double TotalPct, bool Capped);
public sealed record OddsTermDto(string Factor, string Op, double Value, double DeltaPct);

/// A crossing's minutes as their terms (Trip.Time), zeros left out; they sum to TotalMinutes. Hazardous:
/// the pace is a choice (Trip.PaceMatters). Risk: the injury odds this crossing rolls at its pace (Trip.Risk), when there is any.
public sealed record TripDto(int TotalMinutes, IReadOnlyList<TripTermDto> Terms, bool Hazardous, string To, bool ToKnown, OddsDto? Risk = null);
public sealed record TripTermDto(string Factor, int Minutes);

/// A recipe and its odds table (Making.Odds): every outcome's odds; what is left is the plain result.
public sealed record MakeDto(string Recipe, string Name, string Category, int WorkMinutes, IReadOnlyList<string> Materials,
    IReadOnlyList<string> Tools, IReadOnlyList<string> Requires, string Result, double PlainPct, IReadOnlyList<MakeOutcomeDto> Outcomes,
    double? ProgressPct);
public sealed record MakeOutcomeDto(string Text, bool Succeeds, bool FailureLike, OddsDto Odds);

public sealed record PackDto(double CarriedKg, double CapacityKg, IReadOnlyList<StackDto> Stacks, IReadOnlyList<GearDto> Gear,
    IReadOnlyList<GearDto> Worn, IReadOnlyList<ProjectDto> Projects);
/// Amount: Kg in words (Names.Kg), so a crumb reads as one rather than as nothing.
public sealed record StackDto(string Resource, string Name, double Kg, string Amount);
public sealed record GearDto(string Kind, string Name, double ConditionPct, string? Slot);
public sealed record ProjectDto(string Recipe, string Name, double ProgressPct);

public sealed record BodyDto(double CoreTempF, double BloodPct, double CaloriesKcal, double HydrationMl, double EnergyPct,
    IReadOnlyList<EffectDto> Effects, IReadOnlyList<CapacityDto> Impaired, IReadOnlyList<SkillDto> Skills, double ClothingClo);
public sealed record EffectDto(string Name, double Severity);
public sealed record CapacityDto(string Name, double Pct);
public sealed record SkillDto(string Name, int Level);

/// The map as the player knows it. Known: one character per tile, row by row (Width per row): '.'
/// for never seen, otherwise the terrain's code (TerrainCodes). Visible: indices (y*Width+x) in sight now.
/// Everything else is memory: places as last seen, sightings with their minute. Surface: the ground of
/// each known tile with anything on it, in tile order: in sight, as it is now (Ground.SurfaceNow); out
/// of sight, as last seen (Memory.Explored). A known tile that is not listed is bare, dry ground.
/// FrozenWater: the known lake and deep-water tiles that are ice (Map.IsFrozen), in tile order: in sight, now;
/// out of sight, when last seen. A known one not listed is open water.
public sealed record MapDto(int Width, int Height, string Known, IReadOnlyList<int> Visible, int CampX, int CampY,
    IReadOnlyList<PlaceMarkDto> Places, IReadOnlyList<SightingDto> Sightings, IReadOnlyList<TrackDto> Tracks,
    IReadOnlyList<NamedDto> Named, IReadOnlyList<SurfaceDto> Surface, IReadOnlyList<int> FrozenWater);

/// One known tile's ground. I: its index (y*Width+x). SnowCm: lying snow; Crust: its top refrozen;
/// MudLevel: 0-1 on bare ground; WaterCm: standing water (ice included); Ice: that water mostly frozen.
/// Zeros and falses are left out: every view lists every known tile's ground, mostly snow alone.
public sealed record SurfaceDto(int I,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] double SnowCm,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Crust,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] double MudLevel,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] double WaterCm,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Ice);

/// A remembered tile with something on it. Fire: the phase as last seen. FireHoursLeft: the fuel
/// reckoned when last beside it, aged to now (null when not known to be burning). SeenMinute: when.
public sealed record PlaceMarkDto(int X, int Y, long SeenMinute, string? Fire, double? FireHoursLeft, string? Shelter, bool Carcass,
    bool Stash, bool Den, bool Hurt);
public sealed record SightingDto(int X, int Y, string Species, int Count, long Minute);
public sealed record TrackDto(int X, int Y, string Maker, string Reading, long AgeMinutes);
public sealed record NamedDto(int X, int Y, string Name);

/// The fight the player is in. Grid cells are metres, 0..Size-1. Actions: every round action with its
/// refusal (Combat.Refusal); Move takes a cell. Target is the unit faced; Targets every enemy that could
/// be, nearest first, each with the verb that turns to face it (turning costs the round). Lines: what this
/// fight's rounds told, oldest first.
public sealed record CombatDto(int Size, int Round, IReadOnlyList<UnitDto> Units, IReadOnlyList<VerbDto> Actions, string Zone,
    double TargetM, int? Target, double MoveM, IReadOnlyList<TargetDto> Targets, ChaseDto? Chase, IReadOnlyList<LogDto> Lines);
/// Someone has just run from the player: whom, and the odds of running it down if they give chase (the
/// Chase action), how long the chase would take and how far they would run.
public sealed record ChaseDto(int Index, string Name, double Pct, double Seconds, double Metres);
/// An enemy the player could face: its unit index, how far, whether it is faced now, and the Turn verb.
public sealed record TargetDto(int Index, string Name, double DistanceM, bool Facing, VerbDto Turn);
/// State is Combat.StateOf: Unaware, Wary, Pressing, Standoff, Giving ground, Fleeing, Gone or Down.
public sealed record UnitDto(int Index, int Team, string Species, string Name, int X, int Y, bool InField, bool IsPlayer,
    string State, string Condition, string? Weapon);
public sealed record CellDto(int X, int Y);

/// A timed action under way: what it is, how far along. Finished once it stopped. Outcome, once
/// finished: finished, refused, interrupted (Sim's StepOutcome), or stopped (by the client). Reason:
/// the stop in the game's words ("arrived", "the fire went out"), null when it simply ran its time; a
/// round that ended the fight gives the fight's end as Narration tells it.
/// Finds, FoundKg, Mood and Luck: a forage's haul so far, by resource with how many finds, its luck's mood line and tier.
public sealed record JobDto(string Label, string Kind, int DoneMinutes, int? TotalMinutes, bool Finished, string? Outcome, string? Reason,
    int? FromX, int? FromY, string? Dir, IReadOnlyList<FindDto>? Finds = null, double? FoundKg = null, string? Mood = null, string? Luck = null);
/// Category: fuel, food, medicine, material or water, for its colour.
public sealed record FindDto(string Resource, string Name, string Category, int Count, double Kg);

/// "While you were away" (Memories.Receipt): what happened here, what changed, what prints say.
public sealed record ReceiptDto(string Title, long? SinceMinute, long Minute, bool Full, IReadOnlyList<LogDto> Lines);

/// One line of the journal. Kind: plain, bad, good or rare. Icon: an icon path under icons/.
public sealed record LogDto(long Minute, string Text, string Kind, string Icon);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ActDto))]
[JsonSerializable(typeof(ViewDto))]
public partial class HostJson : JsonSerializerContext;
