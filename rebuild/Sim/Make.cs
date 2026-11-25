using System.Globalization;

namespace Survival;

public enum OutcomeKind { Succeed, Fail }

/// One effect an outcome has (emergent-world-design §3.7). A closed list: a new kind is a code and
/// loader change. Shares are 0-1 of the named input's kilos.
public abstract record Effect;
public sealed record WasteMaterial(Resource Input, double SharePct) : Effect;
/// That share of the input named comes back to the pack instead of going into the result.
public sealed record SaveMaterial(Resource Input, double SharePct) : Effect;
public sealed record ExtraProduct(string What, double Amount) : Effect;
/// A cut, through Body.Damage: Amount in DamageInfo's units of tissue condition.
public sealed record Injury(BodyTarget Target, double Amount) : Effect;
/// The tool giving this capability loses whole uses of durability; no amount breaks it outright.
public sealed record BreakTool(Capability Capability, int? Uses) : Effect;

/// BasePct is before skill and hands. Text is the outcome as written, alternatives resolved.
public sealed record Outcome(OutcomeKind Kind, IReadOnlyList<Effect> Effects, double BasePct, string Text)
{
    /// Skill makes these rarer and clumsy hands likelier; the rest are success bonuses (§3.7).
    public bool FailureLike => Kind == OutcomeKind.Fail || Effects.Any(e => e is Injury or BreakTool);

    public string Words => Names.Outcome(this);
}

/// The outcome syntax: `succeed|fail[:effect:params...]=<base odds>`. Effects stack; `|` in any
/// parameter lists alternatives, with the odds split evenly between every pairing of them.
public static class Outcomes
{
    /// Throws FormatException naming what is wrong.
    public static IReadOnlyList<Outcome> Parse(string line)
    {
        int eq = line.LastIndexOf('=');
        if (eq < 0) throw Bad(line, "no '=<odds>'");
        double odds = Number(line, line[(eq + 1)..]);
        if (odds is <= 0 or > 1) throw Bad(line, $"odds {odds} are not in (0, 1]");
        var tokens = line[..eq].Split(':');
        var kind = tokens[0] switch
        {
            "succeed" => OutcomeKind.Succeed,
            "fail" => OutcomeKind.Fail,
            _ => throw Bad(line, $"'{tokens[0]}' is not succeed or fail"),
        };

        // Each effect as its alternatives: one list per effect, then every pairing of them.
        var effects = new List<List<Effect>>();
        for (int i = 1; i < tokens.Length;)
        {
            string name = tokens[i++];
            string Param(string what) => i < tokens.Length ? tokens[i++] : throw Bad(line, $"{name} needs its {what}");
            List<string> Alternatives(string what) => [.. Param(what).Split('|')];
            switch (name)
            {
                case "waste_material":
                case "save_material":
                    {
                        var inputs = Alternatives("resource").Select(r => Named<Resource>(line, r)).ToList();
                        double share = Share(line, Param("share"));
                        effects.Add([.. inputs.Select(r => name == "waste_material" ? (Effect)new WasteMaterial(r, share) : new SaveMaterial(r, share))]);
                        break;
                    }
                case "extra_product":
                    {
                        var what = Alternatives("item or resource");
                        double n = Number(line, Param("amount"));
                        if (n <= 0) throw Bad(line, $"extra_product amount {n}");
                        effects.Add([.. what.Select(w => (Effect)new ExtraProduct(w, n))]);
                        break;
                    }
                case "injury":
                    {
                        var parts = Alternatives("body part").Select(p => Named<BodyTarget>(line, p)).ToList();
                        double damage = Share(line, Param("damage"));
                        effects.Add([.. parts.Select(p => (Effect)new Injury(p, damage))]);
                        break;
                    }
                case "break_tool":
                    {
                        var caps = Alternatives("capability").Select(c => Named<Capability>(line, c)).ToList();
                        if (caps.Contains(Capability.None)) throw Bad(line, "break_tool names no capability");
                        // An amount is optional: the next token is one only if it is a number.
                        int? uses = null;
                        if (i < tokens.Length && int.TryParse(tokens[i], NumberStyles.None, CultureInfo.InvariantCulture, out int n))
                        {
                            if (n <= 0) throw Bad(line, $"break_tool loses {n} uses");
                            uses = n;
                            i++;
                        }
                        else if (i < tokens.Length && double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                            throw Bad(line, $"break_tool loses whole uses, not {tokens[i]}");
                        effects.Add([.. caps.Select(c => (Effect)new BreakTool(c, uses))]);
                        break;
                    }
                default: throw Bad(line, $"no effect '{name}'");
            }
        }

        IEnumerable<List<Effect>> pairings = [[]];
        foreach (var alternatives in effects)
            pairings = pairings.SelectMany(p => alternatives.Select(e => (List<Effect>)[.. p, e])).ToList();
        var all = pairings.ToList();
        return [.. all.Select(p => new Outcome(kind, p.AsReadOnly(), odds / all.Count, Text(kind, p, odds / all.Count)))];
    }

    static string Text(OutcomeKind kind, List<Effect> effects, double odds) =>
        string.Concat(kind == OutcomeKind.Succeed ? "succeed" : "fail", string.Concat(effects.Select(e => ":" + Spelling(e))),
            "=", odds.ToString("0.####", CultureInfo.InvariantCulture));

    static string Spelling(Effect e) => e switch
    {
        WasteMaterial w => $"waste_material:{w.Input}:{w.SharePct.ToString(CultureInfo.InvariantCulture)}",
        SaveMaterial s => $"save_material:{s.Input}:{s.SharePct.ToString(CultureInfo.InvariantCulture)}",
        ExtraProduct x => $"extra_product:{x.What}:{x.Amount.ToString(CultureInfo.InvariantCulture)}",
        Injury j => $"injury:{j.Target}:{j.Amount.ToString(CultureInfo.InvariantCulture)}",
        BreakTool b => b.Uses is int n ? $"break_tool:{b.Capability}:{n}" : $"break_tool:{b.Capability}",
        _ => throw new NotSupportedException(e.GetType().Name),
    };

    static double Share(string line, string text)
    {
        double v = Number(line, text);
        return v is > 0 and <= 1 ? v : throw Bad(line, $"{text} is not in (0, 1]");
    }

    static double Number(string line, string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v) ? v
        : throw Bad(line, $"'{text}' is not a number");

    static T Named<T>(string line, string name) where T : struct, Enum =>
        Enum.GetNames<T>().Contains(name, StringComparer.Ordinal) ? Enum.Parse<T>(name) : throw Bad(line, $"no {typeof(T).Name} '{name}'");

    static FormatException Bad(string line, string why) => new($"outcome '{line}': {why}");
}

public enum MakeCategory { FireStarting, CuttingTool, HuntingWeapon, Lighting, Processing, Clothing, Camp }

/// What the place must have for the work to start, and to go on: a closed list. Wet or cold hands
/// and impairment never block; they slow the work and worsen the odds. Tent: one carried, to start.
public enum Requirement { Light, Fire, Frost, Shelter, Dry, NoShelter, Tent }

/// Count pieces of the discrete alternatives (any piece of the kind will do), or Kg of the continuous ones, taken in
/// the order listed: the first alternative's form is tried first.
public sealed record MaterialInput(IReadOnlyList<Resource> AnyOf, double Kg, int Count = 0)
{
    public IEnumerable<Resource> Discrete() => AnyOf.Where(Items.IsDiscrete);
    public IEnumerable<Resource> Continuous() => AnyOf.Where(r => !Items.IsDiscrete(r));
}

/// A gear kind; Kg of a continuous resource; Count pieces of a discrete one, together SharePct of the inputs' actual
/// weight; or placed on the tile: a shelter (ShelterName, or the tent) or a bedding.
public sealed record RecipeResult(string? Gear, Resource? Resource, double Kg, ShelterKind? Shelter = null, string? ShelterName = null, BeddingKind? Bedding = null,
    int Count = 0, double SharePct = 0)
{
    public bool Placed => Shelter != null || Bedding != null;
}

/// (GLOSSARY: Recipe). Items are gear kinds consumed into the result. Tools are capabilities: a
/// required one gates the recipe and wears once per piece; a faster one only speeds it, by FasterFactor.
public sealed record Recipe(
    string Id, string Name, MakeCategory Category, IReadOnlyList<MaterialInput> Materials, IReadOnlyList<string> Items,
    IReadOnlyList<Capability> Tools, IReadOnlyList<Capability> Faster, IReadOnlyList<Requirement> Requires, int Minutes,
    RecipeResult Result, IReadOnlyList<Outcome> Outcomes, double FasterFactor = Making.FasterToolFactor,
    StationKind? Station = null, StationDriver? Driver = null)
{
    /// Worked by a person through Make, not by a station (Stations).
    public bool ByHand => Station == null;
}

/// A placed recipe's unfinished work (GLOSSARY: Project), on its tile for anyone there to go on with.
public sealed class TileProject
{
    public required string Recipe { get; init; }
    public double MinutesDone { get; set; }
    public Inventory Materials { get; init; } = new();
}

/// In Content/recipes.json file order, which is the menu order.
public static class Recipes
{
    public static readonly IReadOnlyList<Recipe> All = Content.Tables.MakeRecipes;
    static readonly IReadOnlyDictionary<string, Recipe> ById = All.ToDictionary(r => r.Id);

    /// The recipes a person works through Make, in menu order.
    public static readonly IReadOnlyList<Recipe> ByHand = [.. All.Where(r => r.ByHand)];

    public static Recipe Get(string id) => ById.TryGetValue(id, out var r) ? r : throw new KeyNotFoundException($"No recipe '{id}'");
    public static bool Exists(string id) => ById.ContainsKey(id);
}

public sealed record OutcomeOdds(Outcome Outcome, Odds Odds);

/// Shown before the work is committed to; whatever is left is the plain result.
public sealed record MakeOdds(IReadOnlyList<OutcomeOdds> Outcomes)
{
    public double PlainPct => Math.Max(0, 1 - Outcomes.Sum(o => o.Odds.TotalPct));
}

/// Outcome is null for the plain result. Spent: kilos gone from what anyone holds. Produced: kilos
/// made. ExtraGear: pieces made over the result, from the offcuts. Everything else moved: into the
/// result's recorded parts, or back to the pack.
public sealed record Made(long Minute, AgentId Who, TileId Tile, string Recipe, string? Outcome, bool Succeeded,
    SortedDictionary<Resource, double> Spent, SortedDictionary<Resource, double> Produced, IReadOnlyList<string> ExtraGear) : WorldEvent(Minute, Tile);

/// Spent: the kilos built into it, gone from what anyone holds.
public sealed record Built(long Minute, AgentId Who, TileId Tile, string Recipe, SortedDictionary<Resource, double> Spent) : WorldEvent(Minute, Tile);

public sealed record ProjectAbandoned(long Minute, AgentId Who, TileId Tile, string Recipe, SortedDictionary<Resource, double> Lost) : WorldEvent(Minute, Tile);

/// One Make activity for everything made (emergent-world-design §3.7), shared by the player and
/// NPCs. A placed result's project is on the tile (World.Projects), the rest in the pack.
public static class Making
{
    /// Each Crafting level: failure-like weights x(1 - this), success bonuses x(1 + this).
    public const double SkillPctPerLevel = 0.04;
    public const double AbandonReclaimPct = 0.8;
    public const double FasterToolFactor = 1.5;
    /// Below this dexterity the work slows, by up to half again at none.
    const double DexterityThreshold = 0.7, MaxDexterityPenaltyPct = 0.5;

    public const string MadeIt = "made", Spoiled = "spoiled";

    public static bool Handles(Activity a) => a is Make or Abandon;

    public static MakeProject? ProjectFor(Inventory pack, string recipe) => pack.Projects?.FirstOrDefault(p => p.Recipe == recipe);

    public static TileProject? TileProjectFor(World world, Agent agent, Recipe r) =>
        world.Projects.TryGetValue(agent.Tile, out var p) && p.Recipe == r.Id ? p : null;

    /// Null if none is begun.
    public static double? MinutesDone(World world, Agent agent, Recipe r) => r.Result.Placed
        ? TileProjectFor(world, agent, r)?.MinutesDone
        : Items.PackOf(agent) is { } pack ? ProjectFor(pack, r.Id)?.MinutesDone : null;


    public static Refusal? Refusal(World world, Activity activity)
    {
        var agent = world.Agent(activity.Who);
        if (Items.PackOf(agent) is not Inventory pack) return new("carries nothing to work with");
        switch (activity)
        {
            case Make m:
                {
                    var r = Recipes.Get(m.Recipe);
                    if (r.Station is StationKind station) return new($"{Names.Lower(r.Name)} is not done by hand: put it on at a {Stations.Word(station)}");
                    if (r.Result.Placed) return PlacedRefusal(world, agent, pack, r);
                    bool begun = ProjectFor(pack, r.Id) != null;
                    if (!begun && MissingMaterial(pack, r) is { } material) return material;
                    return MissingTool(pack, r, begun) ?? Unmet(world, agent, r);
                }
            case Abandon a:
                {
                    var r = Recipes.Get(a.Recipe);
                    return ProjectFor(pack, r.Id) == null ? new($"you have no unfinished {r.Name.ToLowerInvariant()}") : null;
                }
            default: throw new NotSupportedException($"{activity.GetType().Name} is not making");
        }
    }

    /// The old Build's order: light, what stands or is half-built here, tools, the place, then what it takes.
    static Refusal? PlacedRefusal(World world, Agent agent, Inventory pack, Recipe r)
    {
        var tile = agent.Tile;
        if (r.Requires.Contains(Requirement.Light) && Work.TooDarkToWork(world, agent)) return Work.Dark;
        if (world.Projects.TryGetValue(tile, out var other) && other.Recipe != r.Id) return new($"a {Names.Lower(Recipes.Get(other.Recipe).Name)} is half-built here");
        if (r.Requires.Contains(Requirement.NoShelter) && Shelters.Peek(world, tile) is { } s) return new($"there is already a shelter here ({s.Name})");
        if (r.Result.Bedding is BeddingKind bed && Shelters.BeddingAt(world, tile) is { } there
            && there.QualityPct >= Shelters.Beddings[bed].QualityPct && there.WarmthBonusF >= Shelters.Beddings[bed].WarmthBonusF)
            return new($"the {there.Name.ToLowerInvariant()} here is as good as a {Shelters.Beddings[bed].Name.ToLowerInvariant()}");
        bool begun = other != null;
        return MissingTool(pack, r, begun) ?? Unmet(world, agent, r) ?? (begun ? null : MissingMaterial(pack, r));
    }

    static bool Enough(Inventory pack, MaterialInput m, bool discrete) => discrete
        ? m.Discrete().Any() && m.Discrete().Sum(x => Items.Count(pack, x)) >= m.Count
        : m.Continuous().Any() && m.Continuous().Sum(x => Items.Have(pack, x)) >= m.Kg - 1e-9;

    /// "2 stones", "300 g of plant fibre or 1 sinew".
    public static string Words(MaterialInput m) => string.Join(" or ",
        m.AnyOf.Select(x => Items.IsDiscrete(x) ? Names.Pieces(x, m.Count) : $"{Names.Grams(m.Kg)} of {Names.Of(x)}"));

    static Refusal? MissingMaterial(Inventory pack, Recipe r)
    {
        foreach (var m in r.Materials)
            if (!Enough(pack, m, discrete: true) && !Enough(pack, m, discrete: false))
            {
                // For a planner: kilos that, gathered, would make up the shortfall of the input's first form.
                bool discrete = Items.IsDiscrete(m.AnyOf[0]);
                int shortPieces = Math.Max(0, m.Count - m.Discrete().Sum(x => Items.Count(pack, x)));
                double kg = discrete ? shortPieces * m.Discrete().Average(x => (Items.Resources[x].MinPieceKg + Items.Resources[x].MaxPieceKg) / 2)
                    : m.Kg - m.Continuous().Sum(x => Items.Have(pack, x));
                return new($"you need {Words(m)}", new MissingResource(m.AnyOf, kg, discrete ? shortPieces : 0));
            }
        foreach (var (kind, n) in r.Items.GroupBy(k => k).Select(g => (g.Key, g.Count())))
            if (pack.Gear.Count(g => g.Kind == kind) < n) return new($"you need a {Items.GearKinds[kind].Name.ToLowerInvariant()}", new MissingGear(kind));
        return null;
    }

    /// Before the work begins, the items it would consume do not count: the knife cannot cut its own handle.
    static Refusal? MissingTool(Inventory pack, Recipe r, bool begun)
    {
        var consumed = begun ? [] : ItemsToConsume(pack, r);
        foreach (var cap in r.Tools)
            if (!pack.Gear.Any(g => !consumed.Contains(g) && Gives(g, cap))) return new($"you need something for {Names.Of(cap)}", new MissingCapability(cap));
        return null;
    }

    /// Checked to start and every minute of the work. Fires around the agent must be caught up.
    public static Refusal? Unmet(World world, Agent agent, Recipe r)
    {
        var tile = agent.Tile;
        foreach (var need in r.Requires)
            switch (need)
            {
                case Requirement.Light when Work.TooDarkToWork(world, agent): return Work.Dark;
                case Requirement.Fire when !(Fire.PitAt(world, tile) is FireId id && Fire.IsActive(world.Fires[id])):
                    return new("you need a fire burning here", new MissingState(Lack.BurningFire));
                // The tile's surface air, the air that melts a snow shelter.
                case Requirement.Frost when Exposure.Surface(world, tile, world.Minute).AirTemperatureF is var airF && airF > 32:
                    return new("too warm to build in snow", new MissingState(Lack.FrostF, 32, airF));
                case Requirement.Shelter when Shelters.Peek(world, tile) == null: return new("you need shelter here", new MissingState(Lack.Shelter));
                case Requirement.Dry when Falling(world, tile) && !(Shelters.Peek(world, tile)?.OverheadLevel >= DryRoofLevel):
                    return new("it is too wet here: this needs a roof or a dry sky", new MissingState(Lack.Dry));
                case Requirement.NoShelter when Shelters.Peek(world, tile) is { } s: return new($"there is already a shelter here ({s.Name})");
                case Requirement.Tent when TileProjectFor(world, agent, r) == null && !(Items.PackOf(agent) is { } pack && Shelters.CarriedTent(pack) != null):
                    return new("you have no tent", new MissingGear([.. Shelters.Tents.Keys]));
            }
        return null;
    }

    const double DryRoofLevel = 0.5;

    static bool Falling(World world, TileId tile)
    {
        var s = Exposure.Surface(world, tile, world.Minute);
        return s.SnowfallWeMPerMinute + s.RainfallMPerMinute > 0;
    }

    static bool Gives(Gear g, Capability cap) => Items.Works(g) && Items.Def(g).Capabilities.Contains(cap);

    static Gear? ToolFor(Inventory pack, Capability cap) => pack.Gear.FirstOrDefault(g => Gives(g, cap));

    /// The first of each kind in the pack, as many as it needs.
    static List<Gear> ItemsToConsume(Inventory pack, Recipe r)
    {
        var taken = new List<Gear>();
        foreach (var kind in r.Items)
            if (pack.Gear.FirstOrDefault(g => g.Kind == kind && !taken.Contains(g)) is Gear g) taken.Add(g);
        return taken;
    }


    /// Fires around the agent must be caught up.
    public static MakeOdds Odds(World world, Agent agent, Recipe r)
    {
        var context = Fire.AbilityContextFor(world, agent);
        return Odds(r, Skills.Level(agent, Skill.Crafting), Body.Dexterity(agent.Body, context),
            handsWhy: string.Join(", ", Body.DexterityCosts(agent.Body, context)));
    }

    /// Clumsy hands make failure-like outcomes up to twice as likely at no dexterity, and success
    /// bonuses rarer in proportion. Outcomes summing past certainty are scaled back to share it.
    public static MakeOdds Odds(Recipe r, int craftingLevel, double dexterityPct = 1.0, string handsWhy = "")
    {
        string hands = handsWhy.Length > 0 ? $"Hands ({handsWhy})" : "Hands";
        List<OddsTerm> Terms(Outcome o) => o.FailureLike
            ? Survival.Odds.Moving(OddsTerm.Scale("Crafting", 1 - SkillPctPerLevel * craftingLevel), OddsTerm.Scale(hands, 1 + (1 - dexterityPct)))
            : Survival.Odds.Moving(OddsTerm.Scale("Crafting", 1 + SkillPctPerLevel * craftingLevel), OddsTerm.Scale(hands, dexterityPct));
        var table = r.Outcomes.Select(o => new OutcomeOdds(o, new Survival.Odds("Base odds", o.BasePct, Terms(o)))).ToList();
        double sum = table.Sum(t => t.Odds.TotalPct);
        if (sum > 1)
            table = [.. table.Select(t => t with { Odds = t.Odds with { Terms = [.. t.Odds.Terms, OddsTerm.Scale("Capped", 1 / sum)] } })];
        return new MakeOdds(table.AsReadOnly());
    }


    public static void Begin(World world, Activity activity, List<WorldEvent> events)
    {
        if (Refusal(world, activity) is { } refused) throw new InvalidOperationException($"Making.Begin on a refused activity: {refused}");
        var agent = world.Agent(activity.Who);
        var pack = Items.Pack(world, agent.Id);
        switch (activity)
        {
            case Make m when Recipes.Get(m.Recipe) is { Result.Placed: true } r:
                {
                    if (world.Projects.ContainsKey(agent.Tile)) break; // going on with it
                    var p = new TileProject { Recipe = r.Id };
                    if (r.Requires.Contains(Requirement.Tent) && Shelters.CarriedTent(pack) is Gear tent)
                    {
                        pack.Gear.Remove(tent);
                        p.Materials.Gear.Add(tent);
                    }
                    TakeMaterials(pack, r, p.Materials);
                    world.Projects[agent.Tile] = p;
                    break;
                }
            case Make m when ProjectFor(pack, m.Recipe) == null:
                {
                    var r = Recipes.Get(m.Recipe);
                    var p = new MakeProject { Recipe = r.Id };
                    foreach (var g in ItemsToConsume(pack, r))
                    {
                        pack.Gear.Remove(g);
                        p.Materials.Gear.Add(g);
                    }
                    TakeMaterials(pack, r, p.Materials);
                    (pack.Projects ??= []).Add(p);
                    break;
                }
            case Make: break; // going on with it
            case Abandon a:
                {
                    var p = ProjectFor(pack, a.Recipe)!;
                    RemoveProject(pack, p);
                    var back = new Inventory { Gear = [.. p.Materials.Gear] };
                    var lost = new SortedDictionary<Resource, double>();
                    foreach (var (res, kg) in p.Materials.Amounts)
                    {
                        Items.Add(back, res, kg * AbandonReclaimPct);
                        lost[res] = kg - kg * AbandonReclaimPct;
                    }
                    // Whole pieces: the lightest of the share not reclaimed are lost.
                    foreach (var (res, pieces) in p.Materials.Pieces)
                    {
                        int keep = (int)Math.Round(pieces.Count * AbandonReclaimPct, MidpointRounding.AwayFromZero);
                        var gone = pieces.Take(pieces.Count - keep).ToList();
                        if (gone.Count > 0) lost[res] = gone.Sum();
                        Items.AddPieces(back, res, pieces.Skip(gone.Count));
                    }
                    Items.Receive(world, agent, back, events);
                    events.Add(new ProjectAbandoned(world.Minute, agent.Id, agent.Tile, a.Recipe, lost));
                    break;
                }
        }
    }

    /// Each input from one form: the first alternative's if there is enough of it, else the other's.
    static void TakeMaterials(Inventory pack, Recipe r, Inventory into)
    {
        foreach (var input in r.Materials)
        {
            bool discrete = Items.IsDiscrete(input.AnyOf[0]) ? Enough(pack, input, true) : !Enough(pack, input, false);
            if (discrete)
            {
                int left = input.Count;
                foreach (var res in input.Discrete())
                {
                    var took = Items.TakePieces(pack, res, left);
                    Items.AddPieces(into, res, took);
                    left -= took.Count;
                }
                continue;
            }
            double kgLeft = input.Kg;
            foreach (var res in input.Continuous())
            {
                if (kgLeft <= 0) break;
                double took = Items.Take(pack, res, kgLeft);
                Items.Add(into, res, took);
                kgLeft -= took;
            }
        }
    }

    static void RemoveProject(Inventory pack, MakeProject p)
    {
        pack.Projects!.Remove(p);
        if (pack.Projects.Count == 0) pack.Projects = null;
    }

    /// Returns why the work stops: finished (made or spoiled), the place or tools no longer serving, or death by a slip.
    public static string? Minute(World world, Agent agent, Activity activity, List<WorldEvent> events)
    {
        if (activity is not Make m) return null; // abandoning was done at the start
        var pack = Items.Pack(world, agent.Id);
        var r = Recipes.Get(m.Recipe);
        if (r.Result.Placed) return PlacedMinute(world, agent, pack, r, events);
        var p = ProjectFor(pack, r.Id) ?? throw new InvalidOperationException($"{agent.Id} is making {r.Id} with no project for it");
        if ((MissingTool(pack, r, begun: true) ?? Unmet(world, agent, r)) is { } stop) return stop.Reason;
        p.MinutesDone += WorkPerMinute(world, agent, pack, r);
        Skills.Award(world, agent, Skill.Crafting, Skills.CraftXpPerHour / 60, events);
        return p.MinutesDone < r.Minutes - 1e-9 ? null : Finish(world, agent, r, events);
    }

    static string? PlacedMinute(World world, Agent agent, Inventory pack, Recipe r, List<WorldEvent> events)
    {
        var p = TileProjectFor(world, agent, r) ?? throw new InvalidOperationException($"{agent.Id} is building {r.Id} with nothing half-built for it");
        if (r.Requires.Contains(Requirement.Light) && Work.TooDarkToWork(world, agent)) return Work.TooDark;
        if ((MissingTool(pack, r, begun: true) ?? Unmet(world, agent, r)) is { } stop) return stop.Reason;
        p.MinutesDone += WorkPerMinute(world, agent, pack, r);
        Skills.Award(world, agent, Skill.Crafting, Skills.CraftXpPerHour / 60, events);
        return p.MinutesDone < r.Minutes - 1e-9 ? null : Place(world, agent, pack, r, p, events);
    }

    static string Place(World world, Agent agent, Inventory pack, Recipe r, TileProject p, List<WorldEvent> events)
    {
        var tile = agent.Tile;
        world.Projects.Remove(tile);
        foreach (var tool in r.Tools.Select(c => ToolFor(pack, c)).OfType<Gear>().Distinct().ToList()) Items.Use(tool);
        if (r.Result.Bedding is BeddingKind bed) Shelters.PlaceBedding(world, tile, bed);
        else if (p.Materials.Gear.FirstOrDefault(g => Shelters.Tents.ContainsKey(g.Kind)) is Gear tent)
            Shelters.Place(world, tile, Shelters.Tents[tent.Kind], tent, new Cause(agent.Id, $"pitched by {Cause.Describe(world, agent.Id)}"));
        else Shelters.Place(world, tile, new ShelterDef(r.Result.ShelterName!, r.Result.Shelter!.Value), null, new Cause(agent.Id, $"built by {Cause.Describe(world, agent.Id)}"));
        events.Add(new Built(world.Minute, agent.Id, tile, r.Id, Items.KgByResource(p.Materials)));
        return Shelters.BuiltReason;
    }

    /// Bad hands count twice, through manipulation and through the dexterity built on it. Read every
    /// minute, so how the work is split into steps cannot change when it finishes. Camp work: legs and hands.
    public static double WorkPerMinute(World world, Agent agent, Inventory pack, Recipe r)
    {
        double work = 1.0;
        foreach (var cap in r.Faster) if (ToolFor(pack, cap) != null) work *= r.FasterFactor;
        var c = Body.GetCapacities(agent.Body);
        if (r.Result.Placed) return work / ((c.Moving < 0.5 ? 1.20 : 1.0) * (c.Manipulation < 0.5 ? 1.25 : 1.0));
        double slow = c.Manipulation < 0.5 ? 1.25 : 1.0;
        double dexterity = Body.Dexterity(agent.Body, Fire.AbilityContextFor(world, agent));
        if (dexterity < DexterityThreshold) slow *= 1 + (DexterityThreshold - dexterity) / DexterityThreshold * MaxDexterityPenaltyPct;
        return work / slow;
    }

    static readonly Cause Slip = new(null, "a slip at work");

    /// Rolled at this minute's odds, not the sheet shown at the start: hands that froze or a light
    /// that failed since change the roll.
    static string Finish(World world, Agent agent, Recipe r, List<WorldEvent> events)
    {
        var odds = Odds(world, agent, r);
        double roll = world.Rng("make", agent.Id).NextDouble();
        Outcome? outcome = null;
        foreach (var o in odds.Outcomes)
            if ((roll -= o.Odds.TotalPct) < 0) { outcome = o.Outcome; break; }
        return Complete(world, agent, r, outcome, events);
    }

    /// An extra product of a discrete result itself: more, smaller pieces of the same weight.
    internal static int ExtraPieces(IEnumerable<Effect> effects, Resource result) =>
        effects.OfType<ExtraProduct>().Where(x => x.What == result.ToString()).Sum(x => (int)x.Amount);

    /// Outcome null is the plain result. Tests name the outcome here.
    internal static string Complete(World world, Agent agent, Recipe r, Outcome? outcome, List<WorldEvent> events)
    {
        var pack = Items.Pack(world, agent.Id);
        var p = ProjectFor(pack, r.Id) ?? throw new InvalidOperationException($"{agent.Id} has no unfinished {r.Id}");
        var effects = outcome?.Effects ?? [];
        RemoveProject(pack, p);
        var materials = p.Materials;
        var spent = new SortedDictionary<Resource, double>();
        var produced = new SortedDictionary<Resource, double>();
        var extraGear = new List<string>();
        var gained = new Inventory();

        // Waste and saving act on the whole input they name, whichever alternative went in: a share of an amount,
        // or that share of the pieces rounded to whole ones, lightest first (a quarter of one hide is none of it).
        double inputKg = Items.Totals(materials).Sum(x => x.Kg);
        double wastedKg = 0;
        foreach (var e in effects)
        {
            var (named, share, waste) = e switch { WasteMaterial w => (w.Input, w.SharePct, true), SaveMaterial s => (s.Input, s.SharePct, false), _ => (default(Resource?), 0.0, false) };
            if (named is not Resource input) continue;
            foreach (var res in r.Materials.First(m => m.AnyOf.Contains(input)).AnyOf)
            {
                var part = new Inventory();
                if (Items.IsDiscrete(res))
                    Items.AddPieces(part, res, Items.TakePieces(materials, res, (int)Math.Round(Items.Count(materials, res) * share, MidpointRounding.AwayFromZero)));
                else Items.Add(part, res, Items.Take(materials, res, Items.Have(materials, res) * share));
                double kg = Items.Have(part, res);
                if (kg <= 0) continue;
                if (waste) { spent[res] = spent.GetValueOrDefault(res) + kg; wastedKg += kg; }
                else Items.AddAll(gained, part);
            }
        }

        // The tools that did the work, found once: each is worn once per piece, however many of the
        // recipe's capabilities it gives, and a broken-tool outcome breaks the one that was used.
        var used = r.Tools.Concat(r.Faster).Distinct().Select(c => (c, Tool: ToolFor(pack, c))).Where(x => x.Tool != null)
            .ToDictionary(x => x.c, x => x.Tool!);
        foreach (var tool in r.Tools.Where(used.ContainsKey).Select(c => used[c]).Distinct()) Items.Use(tool);

        bool succeeded = outcome?.Kind != OutcomeKind.Fail;
        if (!succeeded) Items.AddAll(gained, materials); // time lost: what is left comes back
        else if (r.Result.Gear is string kind)
        {
            var item = Items.Make(kind);
            item.MadeOf = Composition(materials);
            gained.Gear.Add(item);
        }
        else
        {
            // A resource result: the inputs are worked into it, less in proportion to what was wasted; a discrete
            // one is its share of the actual weight that went in, in whole pieces. Saved material is a bonus over a
            // full result (the loader keeps it from making mass).
            foreach (var (res, kg) in Items.Totals(materials)) spent[res] = spent.GetValueOrDefault(res) + kg;
            var made = r.Result.Resource!.Value;
            double kgMade;
            if (Items.IsDiscrete(made))
            {
                kgMade = r.Result.SharePct * Math.Max(0, inputKg - wastedKg);
                int pieces = r.Result.Count + ExtraPieces(effects, made);
                for (int i = 0; i < pieces; i++) Items.AddPiece(gained, made, kgMade / pieces);
            }
            else
            {
                kgMade = r.Result.Kg * Math.Clamp((inputKg - wastedKg) / inputKg, 0, 1);
                Items.Add(gained, made, kgMade);
            }
            produced[made] = produced.GetValueOrDefault(made) + kgMade;
        }

        string? died = null;
        foreach (var e in effects)
            switch (e)
            {
                case ExtraProduct x when x.What == r.Result.Resource?.ToString():
                    break;
                case ExtraProduct x when Items.GearKinds.ContainsKey(x.What):
                    for (int i = 0; i < (int)x.Amount; i++) { gained.Gear.Add(Items.Make(x.What)); extraGear.Add(x.What); }
                    break;
                case ExtraProduct x:
                    {
                        var res = Enum.Parse<Resource>(x.What);
                        Items.Add(gained, res, x.Amount);
                        produced[res] = produced.GetValueOrDefault(res) + x.Amount;
                        break;
                    }
                case BreakTool b when used.TryGetValue(b.Capability, out var tool):
                    if (b.Uses is not int uses) tool.Durability = 0;
                    else for (int i = 0; i < uses; i++) Items.Use(tool);
                    break;
                case Injury j:
                    died ??= Injure(world, agent, j, events);
                    break;
            }

        Items.Receive(world, agent, gained, events);
        events.Add(new Made(world.Minute, agent.Id, agent.Tile, r.Id, outcome?.Text, succeeded, spent, produced, extraGear.AsReadOnly()));
        return died ?? (succeeded ? MadeIt : Spoiled);
    }

    /// The parts record: the very pieces and amounts that went in, and the parts of any item consumed.
    static Inventory Composition(Inventory materials)
    {
        var parts = Items.CopyResources(materials);
        foreach (var g in materials.Gear)
            if (g.MadeOf is { } made) Items.AddAll(parts, Items.CopyResources(made));
        return parts;
    }

    static string? Injure(World world, Agent agent, Injury j, List<WorldEvent> events)
    {
        var body = agent.Body;
        var before = body.Effects.Select(e => e.Kind).ToList();
        var hit = Body.Damage(body, new DamageInfo(j.Amount, DamageType.Sharp, j.Target, By: Slip), world.Rng("body", agent.Id));
        events.Add(new Injured(world.Minute, agent.Id, agent.Tile, hit.HitRegion, DamageType.Sharp, j.Amount));
        foreach (var k in body.Effects.Select(e => e.Kind).Except(before)) events.Add(new EffectStarted(world.Minute, agent.Id, agent.Tile, k));
        if (Body.IsAlive(body)) return null;
        Body.MarkDead(body, world.Minute, Slip);
        events.Add(new Died(world.Minute, agent.Id, agent.Tile));
        return "died";
    }

}
