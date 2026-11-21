using System.Text.Json.Serialization;

namespace Survival.Web;

// The view: everything the page draws, as plain data. No Sim type crosses the boundary; names are
// the game's own words, numbers carry their units. Built fresh for every call (ViewBuilder).

/// What the client posts to Act. Exactly one of Verb, Advance or Stop.
/// Verb: an id from the current view (starts it; a combat Move also takes To).
/// Advance: run the job under way for up to this many game minutes.
/// Stop: drop the job under way, keeping what it did so far.
public sealed record ActDto(string? Verb = null, int? Advance = null, bool? Stop = null, CellDto? To = null);

public sealed record ViewDto(
    int Gen,
    long Minute,
    ClockDto Clock,
    FeelsDto Feels,
    IReadOnlyList<VitalDto> Vitals,
    IReadOnlyList<AlertDto> Alerts,
    PlaceDto Place,
    FireDto? Fire,
    IReadOnlyList<ThingDto> Things,
    IReadOnlyList<VerbDto> Walk,
    IReadOnlyList<VerbDto> Tile,
    IReadOnlyList<VerbDto> Make,
    PackDto Pack,
    BodyDto Body,
    MapDto Map,
    CombatDto? Combat,
    JobDto? Job,
    ReceiptDto? Receipt,
    IReadOnlyList<LogDto> Log,
    string Outcome,
    string? DeathCause);

/// Day is the run's day number; MinuteOfDay 0-1439. DaylightLeftMinutes is 0 at night.
public sealed record ClockDto(int Day, int MinuteOfDay, string Season, string Sky, double AirF, double WindMph, string WindDirection,
    int SunriseMinute, int SunsetMinute, int DaylightLeftMinutes);

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
public sealed record ThingDto(string Kind, string Name, IReadOnlyList<ConditionDto> Conditions, string? Action, bool Interact,
    IReadOnlyList<VerbDto> Menu);
public sealed record ConditionDto(string Name, bool Blocking);

/// One verb. Id is what the client posts back. Kind is the activity's family, for icons and keys only.
/// Minutes: what it runs for, or null for work that stops itself. Refused: why it cannot start now.
/// Uses: what it takes from the pack, in words, where the verb says (lighting a fire, feeding one).
public sealed record VerbDto(string Id, string Label, string Kind, int? Minutes, string? Refused,
    OddsDto? Odds = null, TripDto? Trip = null, MakeDto? Make = null, string? Dir = null, bool Careful = false,
    IReadOnlyList<string>? Uses = null);

/// A bet as its factors (Survival.Odds): start at BasePct, apply each term, clamp. DeltaPct is each
/// term's signed share, so BasePct plus the deltas is RawPct; TotalPct is that clamped.
public sealed record OddsDto(string Base, double BasePct, IReadOnlyList<OddsTermDto> Terms, double RawPct, double TotalPct, bool Capped);
public sealed record OddsTermDto(string Factor, string Op, double Value, double DeltaPct);

/// A crossing's minutes as their terms (Trip.Time), zeros left out; they sum to TotalMinutes.
public sealed record TripDto(int TotalMinutes, IReadOnlyList<TripTermDto> Terms, bool Hazardous, string To, bool ToKnown);
public sealed record TripTermDto(string Factor, int Minutes);

/// A recipe and its odds table (Making.Odds): every outcome's odds; what is left is the plain result.
public sealed record MakeDto(string Recipe, string Name, string Category, int WorkMinutes, IReadOnlyList<string> Materials,
    IReadOnlyList<string> Tools, IReadOnlyList<string> Requires, string Result, double PlainPct, IReadOnlyList<MakeOutcomeDto> Outcomes,
    double? ProgressPct);
public sealed record MakeOutcomeDto(string Text, bool Succeeds, bool FailureLike, OddsDto Odds);

public sealed record PackDto(double CarriedKg, double CapacityKg, IReadOnlyList<StackDto> Stacks, IReadOnlyList<GearDto> Gear,
    IReadOnlyList<GearDto> Worn, IReadOnlyList<ProjectDto> Projects);
public sealed record StackDto(string Resource, string Name, double Kg);
public sealed record GearDto(string Kind, string Name, double ConditionPct, string? Slot);
public sealed record ProjectDto(string Recipe, string Name, double ProgressPct);

public sealed record BodyDto(double CoreTempF, double BloodPct, double CaloriesKcal, double HydrationMl, double EnergyPct,
    IReadOnlyList<EffectDto> Effects, IReadOnlyList<CapacityDto> Impaired, IReadOnlyList<SkillDto> Skills, double ClothingClo);
public sealed record EffectDto(string Name, double Severity);
public sealed record CapacityDto(string Name, double Pct);
public sealed record SkillDto(string Name, int Level);

/// The map as the player knows it. Known: one character per tile, row by row (Width per row): '.'
/// for never seen, otherwise the terrain's code (TerrainCodes). Visible: indices (y*Width+x) in sight now.
/// Everything else is memory: places as last seen, sightings with their minute.
public sealed record MapDto(int Width, int Height, string Known, IReadOnlyList<int> Visible, int CampX, int CampY,
    IReadOnlyList<PlaceMarkDto> Places, IReadOnlyList<SightingDto> Sightings, IReadOnlyList<TrackDto> Tracks,
    IReadOnlyList<NamedDto> Named);

/// A remembered tile with something on it. Fire: the phase as last seen. FireHoursLeft: the fuel
/// reckoned when last beside it, aged to now (null when not known to be burning). SeenMinute: when.
public sealed record PlaceMarkDto(int X, int Y, long SeenMinute, string? Fire, double? FireHoursLeft, string? Shelter, bool Carcass,
    bool Stash, bool Den, bool Hurt);
public sealed record SightingDto(int X, int Y, string Species, int Count, long Minute);
public sealed record TrackDto(int X, int Y, string Maker, string Reading, long AgeMinutes);
public sealed record NamedDto(int X, int Y, string Name);

/// The fight the player is in. Grid cells are metres, 0..Size-1. Actions: every round action with its
/// refusal (Combat.Refusal); Move takes a cell.
public sealed record CombatDto(int Size, int Round, IReadOnlyList<UnitDto> Units, IReadOnlyList<VerbDto> Actions, string Zone,
    double NearestM, int? Target, double MoveM);
public sealed record UnitDto(int Index, int Team, string Species, string Name, int X, int Y, bool InField, bool IsPlayer,
    string Awareness, string Condition, string? Weapon);
public sealed record CellDto(int X, int Y);

/// A timed action under way: what it is, how far along. Finished once it stopped (Reason says why
/// if it was cut short).
public sealed record JobDto(string Label, string Kind, int DoneMinutes, int? TotalMinutes, bool Finished, string? Reason,
    int? FromX, int? FromY, string? Dir);

/// "While you were away" (Knowledge.Receipt): what happened here, what changed, what prints say.
public sealed record ReceiptDto(string Title, long? SinceMinute, long Minute, bool Full, IReadOnlyList<LogDto> Lines);

/// One line of the journal. Kind: plain, bad, good or rare. Icon: an icon path under icons/.
public sealed record LogDto(long Minute, string Text, string Kind, string Icon);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ActDto))]
[JsonSerializable(typeof(ViewDto))]
public partial class HostJson : JsonSerializerContext;
