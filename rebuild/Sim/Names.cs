using System.Globalization;

namespace Survival;

/// The one owner of the words the player reads for the game's names (Content/names.json): "BirchBark"
/// is "birch bark", ToolType.HandDrill "hand drill". Odds factors, outcomes, verbs and event lines
/// (Narration) name things through here; the CLI and the web host read the same words.
public static class Names
{
    /// Every enum the player reads a value of, each with a complete table in names.json (Content checks).
    internal static readonly Type[] Tables =
    [
        typeof(Resource), typeof(ToolType), typeof(Capability), typeof(WeatherCondition), typeof(Precipitation), typeof(BodyTarget),
        typeof(EffectKind), typeof(Capacity), typeof(Species), typeof(ButcherMode), typeof(FirePhase),
        typeof(PitType), typeof(ShelterStat), typeof(Terrain), typeof(Skill),
        typeof(MakeCategory), typeof(Requirement), typeof(TrackMaker), typeof(TrailTier), typeof(Season), typeof(WindDirection),
        typeof(Direction), typeof(EquipSlot), typeof(ForageFocus),
    ];

    /// Lower case: "birch bark".
    public static string Of(Enum value) =>
        Content.Tables.Names.TryGetValue(value, out var words) ? words
        : throw new KeyNotFoundException($"No words for {value.GetType().Name}.{value}: add its table to Names.Tables and names.json");

    public static string Title(Enum value) => Cap(Of(value));

    public static string Gear(string kind) =>
        Items.GearKinds.TryGetValue(kind, out var g) ? g.Name : throw new KeyNotFoundException($"No gear kind '{kind}'");

    public static string GearWords(string kind) => Gear(kind).ToLowerInvariant();

    public static string A(string words) => ("aeiou".Contains(char.ToLowerInvariant(words[0])) ? "an " : "a ") + words;

    public static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    public static string Lower(string s) => s.ToLowerInvariant();

    public static string Kg(double kg) =>
        kg > 0 && kg < 0.005 ? "<0.01 kg" : kg.ToString(kg < 1 ? "0.##" : "0.#", CultureInfo.InvariantCulture) + " kg";

    public static string Kg(double kg, Resource r) => $"{Kg(kg)} {Of(r)}";

    /// "3 stones"; "120 g plant fibre", "1.2 kg plant fibre".
    public static string Amount(Resource r, double kg, int pieces) =>
        Items.IsDiscrete(r) ? Pieces(r, pieces) : $"{Grams(kg)} {Of(r)}";

    public static string PieceWord(Resource r) => Pieces(r, 1)[2..];

    public static string Pieces(Resource r, int count) =>
        $"{count} {(Content.Tables.PieceWords.TryGetValue(r, out var w) ? w[count == 1 ? 0 : 1] : throw new KeyNotFoundException($"No piece words for {r} in names.json Pieces"))}";

    public static string Grams(double kg) =>
        kg > 0 && kg < 0.0005 ? "<1 g"
        : kg < 0.4995 ? (kg * 1000).ToString("0", CultureInfo.InvariantCulture) + " g"
        : kg.ToString("0.0", CultureInfo.InvariantCulture) + " kg";

    public static string Outcome(Outcome o)
    {
        var parts = o.Effects.Select(e => e switch
        {
            WasteMaterial w => w.SharePct >= 1 ? $"the {Of(w.Input)} is wasted" : $"{Pct(w.SharePct)} of the {Of(w.Input)} is wasted",
            SaveMaterial s => $"{Pct(s.SharePct)} of the {Of(s.Input)} is left over",
            ExtraProduct x => Items.GearKinds.TryGetValue(x.What, out var g) ? $"an extra {Lower(g.Name)}" : Extra(Enum.Parse<Resource>(x.What), x.Amount),
            Injury j => $"you cut your {Of(j.Target)}",
            BreakTool b => b.Uses is int n ? $"the {Of(b.Capability)} tool loses {n} {(n == 1 ? "use" : "uses")}" : $"the {Of(b.Capability)} tool breaks",
            _ => throw new NotSupportedException(e.GetType().Name),
        }).ToList();
        string head = o.Kind == OutcomeKind.Fail ? "Fails" : "Done";
        return parts.Count == 0 ? head : o.Kind == OutcomeKind.Fail ? $"{head}: {string.Join(", ", parts)}" : $"{head}, but {string.Join(", ", parts)}";
    }

    static string Extra(Resource r, double amount) =>
        !Items.IsDiscrete(r) ? $"{Kg(amount)} more {Of(r)}" : amount == 1 ? $"an extra {PieceWord(r)}" : $"{Pieces(r, (int)amount)} more";

    static string Pct(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
}
