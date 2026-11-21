using System.Globalization;

namespace Survival;

// ---- outcomes: what a finished piece of work can turn out as, besides the plain result ----

public enum OutcomeKind { Succeed, Fail }

/// One effect an outcome has (emergent-world-design §3.7). A closed list: a new kind is a code and
/// loader change. Shares are 0-1 of the named input's kilos.
public abstract record Effect;
/// That share of the input named (by any of its alternatives) is lost.
public sealed record WasteMaterial(Resource Input, double SharePct) : Effect;
/// That share of the input named comes back to the pack instead of going into the result.
public sealed record SaveMaterial(Resource Input, double SharePct) : Effect;
/// Something more besides: Amount pieces of a gear kind, or kilos of a resource.
public sealed record ExtraProduct(string What, double Amount) : Effect;
/// A cut, through Body.Damage: Amount on DamageInfo's 0-1 scale.
public sealed record Injury(BodyTarget Target, double Amount) : Effect;
/// The tool giving this capability loses whole uses of durability; no amount breaks it outright.
public sealed record BreakTool(Capability Capability, int? Uses) : Effect;

/// One way a finished piece of work can turn out, with its base odds before skill and hands.
/// Text is the outcome as written, alternatives resolved.
public sealed record Outcome(OutcomeKind Kind, IReadOnlyList<Effect> Effects, double BasePct, string Text)
{
    /// Failure-like weights are made rarer by skill and likelier by clumsy hands: a failure, an injury
    /// or a broken tool (§3.7). The rest are success bonuses, made likelier by skill.
    public bool FailureLike => Kind == OutcomeKind.Fail || Effects.Any(e => e is Injury or BreakTool);
}

/// The outcome syntax: `succeed|fail[:effect:params...]=<base odds>`. Effects stack; `|` in any
/// parameter lists alternatives, with the odds split evenly between every pairing of them.
public static class Outcomes
{
    /// Every concrete outcome the line stands for. Throws FormatException naming what is wrong.
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

// ---- recipes ----

/// Menu grouping (old NeedCategory, the families ported so far).
public enum MakeCategory { FireStarting, CuttingTool, HuntingWeapon, Lighting, Processing, Clothing }

/// What the place must have for the work to start, and to go on: a closed list. Wet or cold hands
/// and impairment never block; they slow the work and worsen the odds.
public enum Requirement { Light, Fire, Frost, Shelter, Dry }

/// Kilos of any of these resources, taken in the order listed.
public sealed record MaterialInput(IReadOnlyList<Resource> AnyOf, double Kg);

/// What the recipe makes when nothing goes differently: one piece of a gear kind, or kilos of a resource.
public sealed record RecipeResult(string? Gear, Resource? Resource, double Kg);

/// How one thing is made (GLOSSARY: Recipe). Items are gear kinds consumed into the result. Tools are
/// capabilities: a required one gates the recipe and is used once per finished piece; a faster one
/// only speeds it.
public sealed record Recipe(
    string Id, string Name, MakeCategory Category, IReadOnlyList<MaterialInput> Materials, IReadOnlyList<string> Items,
    IReadOnlyList<Capability> Tools, IReadOnlyList<Capability> Faster, IReadOnlyList<Requirement> Requires, int Minutes,
    RecipeResult Result, IReadOnlyList<Outcome> Outcomes);

/// The recipes (Content/recipes.json), in file order: the order the menu lists them.
public static class Recipes
{
    public static readonly IReadOnlyList<Recipe> All = Content.Tables.MakeRecipes;
    static readonly IReadOnlyDictionary<string, Recipe> ById = All.ToDictionary(r => r.Id);

    public static Recipe Get(string id) => ById.TryGetValue(id, out var r) ? r : throw new KeyNotFoundException($"No recipe '{id}'");
    public static bool Exists(string id) => ById.ContainsKey(id);
}

// ---- the odds sheet ----

/// One outcome's chance as its terms: the base odds, the maker's skill, their hands and head.
public sealed record OutcomeOdds(Outcome Outcome, Odds Odds);

/// Every outcome of a recipe as its odds, shown before the work is committed to; whatever is left
/// is the plain result.
public sealed record MakeOdds(IReadOnlyList<OutcomeOdds> Outcomes)
{
    public double PlainPct => Math.Max(0, 1 - Outcomes.Sum(o => o.Odds.TotalPct));
}

/// A finished piece of work: the recipe, the outcome rolled (null for the plain result), and what it
/// did to the things held. Spent: kilos gone from what anyone holds (wasted, or worked into a resource
/// result). Produced: kilos made (a resource result, an extra product). ExtraGear: pieces made over
/// the result, from the offcuts. Everything else moved: into the result's recorded parts, or back
/// to the pack.
public sealed record Made(long Minute, AgentId Who, TileId Tile, string Recipe, string? Outcome, bool Succeeded,
    SortedDictionary<Resource, double> Spent, SortedDictionary<Resource, double> Produced, IReadOnlyList<string> ExtraGear) : WorldEvent(Minute, Tile);

/// An unfinished thing given up: most of its materials came back; Lost is the rest.
public sealed record ProjectAbandoned(long Minute, AgentId Who, TileId Tile, string Recipe, SortedDictionary<Resource, double> Lost) : WorldEvent(Minute, Tile);

// ---- the Make activity ----

/// One Make activity for everything carried (emergent-world-design §3.7), shared by the player and
/// NPCs: start or go on with a recipe's project, and when its work is done roll one outcome.
/// Building on a tile is still Shelters' Build until it folds in (roadmap 8).
public static class Making
{
    /// Each Crafting level: failure-like weights x(1 - this), success bonuses x(1 + this).
    public const double SkillPctPerLevel = 0.04;
    /// What abandoning a project gives back of its materials' kilos (old ReclaimPct, never wired).
    public const double AbandonReclaimPct = 0.8;
    /// How much more work a minute does with each faster tool carried.
    public const double FasterToolFactor = 1.5;
    /// Old CraftingEffort: below this dexterity the work slows, by up to half again at none.
    const double DexterityThreshold = 0.7, MaxDexterityPenaltyPct = 0.5;

    public const string MadeIt = "made", Spoiled = "spoiled";

    public static bool Handles(Activity a) => a is Make or Abandon;

    /// The unfinished thing for this recipe in the pack, if there is one.
    public static MakeProject? ProjectFor(Inventory pack, string recipe) => pack.Projects?.FirstOrDefault(p => p.Recipe == recipe);

    // ---- refusal ----

    public static string? Refusal(World world, Activity activity)
    {
        var agent = world.Agent(activity.Who);
        if (Items.PackOf(agent) is not Inventory pack) return "carries nothing to work with";
        switch (activity)
        {
            case Make m:
            {
                var r = Recipes.Get(m.Recipe);
                // Going on with it: the materials went in when it began.
                bool begun = ProjectFor(pack, r.Id) != null;
                if (!begun && MissingMaterial(pack, r) is string material) return material;
                return MissingTool(pack, r, begun) ?? Unmet(world, agent, r);
            }
            case Abandon a:
            {
                var r = Recipes.Get(a.Recipe);
                return ProjectFor(pack, r.Id) == null ? $"you have no unfinished {r.Name.ToLowerInvariant()}" : null;
            }
            default: throw new NotSupportedException($"{activity.GetType().Name} is not making");
        }
    }

    static string? MissingMaterial(Inventory pack, Recipe r)
    {
        foreach (var m in r.Materials)
            if (m.AnyOf.Sum(x => Items.Have(pack, x)) < m.Kg - 1e-9)
                return $"you need {m.Kg:0.##} kg of {string.Join(" or ", m.AnyOf.Select(Words))}";
        foreach (var (kind, n) in r.Items.GroupBy(k => k).Select(g => (g.Key, g.Count())))
            if (pack.Gear.Count(g => g.Kind == kind) < n) return $"you need a {Items.GearKinds[kind].Name.ToLowerInvariant()}";
        return null;
    }

    /// A required tool the pack lacks. Before the work begins, the items it would consume do not count:
    /// the knife cannot cut its own handle.
    static string? MissingTool(Inventory pack, Recipe r, bool begun)
    {
        var consumed = begun ? [] : ItemsToConsume(pack, r);
        foreach (var cap in r.Tools)
            if (!pack.Gear.Any(g => !consumed.Contains(g) && Gives(g, cap))) return $"you need something for {Words(cap.ToString())}";
        return null;
    }

    /// The place falls short of one of the recipe's requirements: why, or null. Checked to start and
    /// every minute of the work. Fires around the agent must be caught up.
    public static string? Unmet(World world, Agent agent, Recipe r)
    {
        var tile = agent.Tile;
        foreach (var need in r.Requires)
            switch (need)
            {
                case Requirement.Light when Work.TooDarkToWork(world, agent): return Work.TooDark;
                case Requirement.Fire when !(Fire.PitAt(world, tile) is FireId id && Fire.IsActive(world.Fires[id])): return "you need a fire burning here";
                // The tile's surface air, the air that melts a snow shelter.
                case Requirement.Frost when Exposure.Surface(world, tile, world.Minute).AirTemperatureF > 32: return "it is too warm: this needs a frost";
                case Requirement.Shelter when Shelters.Peek(world, tile) == null: return "you need shelter here";
                case Requirement.Dry when Falling(world, tile) && !(Shelters.Peek(world, tile)?.OverheadLevel >= DryRoofLevel):
                    return "it is too wet here: this needs a roof or a dry sky";
            }
        return null;
    }

    /// A roof this good keeps the work dry.
    const double DryRoofLevel = 0.5;

    static bool Falling(World world, TileId tile)
    {
        var s = Exposure.Surface(world, tile, world.Minute);
        return s.SnowfallWeMPerMinute + s.RainfallMPerMinute > 0;
    }

    static bool Gives(Gear g, Capability cap) => Items.Works(g) && Items.Def(g).Capabilities.Contains(cap);

    /// The working tool in the pack that gives this capability: the first, in pack order.
    static Gear? ToolFor(Inventory pack, Capability cap) => pack.Gear.FirstOrDefault(g => Gives(g, cap));

    /// The pieces a new project would take: the first of each kind in the pack, as many as it needs.
    static List<Gear> ItemsToConsume(Inventory pack, Recipe r)
    {
        var taken = new List<Gear>();
        foreach (var kind in r.Items)
            if (pack.Gear.FirstOrDefault(g => g.Kind == kind && !taken.Contains(g)) is Gear g) taken.Add(g);
        return taken;
    }

    // ---- the odds ----

    /// This maker's odds for the recipe here, now: their skill, their hands (manipulation, wet, cold,
    /// the dark) and their head. Fires around the agent must be caught up.
    public static MakeOdds Odds(World world, Agent agent, Recipe r)
    {
        var context = Fire.AbilityContextFor(world, agent);
        return Odds(r, Skills.Level(agent, Skill.Crafting), Body.Dexterity(agent.Body, context),
            dazed: Body.GetCapacities(agent.Body).Consciousness < 0.5, handsWhy: string.Join(", ", Body.DexterityCosts(agent.Body, context)));
    }

    /// The odds table: each outcome's base odds, then the skill, the hands and the head, in that order.
    /// Clumsy hands make failure-like outcomes likelier, up to twice as likely at no dexterity, and
    /// success bonuses rarer, in proportion. Should the outcomes ever add up to more than a certainty,
    /// every one is scaled back to share it.
    public static MakeOdds Odds(Recipe r, int craftingLevel, double dexterityPct = 1.0, bool dazed = false, string handsWhy = "")
    {
        string hands = handsWhy.Length > 0 ? $"Hands ({handsWhy})" : "Hands";
        List<OddsTerm> Terms(Outcome o) => o.FailureLike
            ? Survival.Odds.Moving(OddsTerm.Scale("Crafting", 1 - SkillPctPerLevel * craftingLevel), OddsTerm.Scale(hands, 1 + (1 - dexterityPct)),
                OddsTerm.Scale("Dazed", dazed ? 1.2 : 1))
            : Survival.Odds.Moving(OddsTerm.Scale("Crafting", 1 + SkillPctPerLevel * craftingLevel), OddsTerm.Scale(hands, dexterityPct));
        var table = r.Outcomes.Select(o => new OutcomeOdds(o, new Survival.Odds(o.Text, o.BasePct, Terms(o)))).ToList();
        double sum = table.Sum(t => t.Odds.TotalPct);
        if (sum > 1)
            table = [.. table.Select(t => t with { Odds = t.Odds with { Terms = [.. t.Odds.Terms, OddsTerm.Scale("Capped", 1 / sum)] } })];
        return new MakeOdds(table.AsReadOnly());
    }

    // ---- the work ----

    /// Starts the work Refusal allowed: a new project takes its materials now; abandoning is instant.
    public static void Begin(World world, Activity activity, List<WorldEvent> events)
    {
        if (Refusal(world, activity) is string refused) throw new InvalidOperationException($"Making.Begin on a refused activity: {refused}");
        var agent = world.Agent(activity.Who);
        var pack = Items.Pack(world, agent.Id);
        switch (activity)
        {
            case Make m when ProjectFor(pack, m.Recipe) == null:
            {
                var r = Recipes.Get(m.Recipe);
                var p = new MakeProject { Recipe = r.Id };
                foreach (var g in ItemsToConsume(pack, r))
                {
                    pack.Gear.Remove(g);
                    p.Materials.Gear.Add(g);
                }
                foreach (var input in r.Materials)
                {
                    double left = input.Kg;
                    foreach (var res in input.AnyOf)
                    {
                        if (left <= 0) break;
                        double took = Items.Take(pack, res, left);
                        Items.Add(p.Materials, res, took);
                        left -= took;
                    }
                }
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
                foreach (var (res, kg) in p.Materials.Kg)
                {
                    Items.Add(back, res, kg * AbandonReclaimPct);
                    lost[res] = kg - kg * AbandonReclaimPct;
                }
                Items.Receive(world, agent, back, events);
                events.Add(new ProjectAbandoned(world.Minute, agent.Id, agent.Tile, a.Recipe, lost));
                break;
            }
        }
    }

    static void RemoveProject(Inventory pack, MakeProject p)
    {
        pack.Projects!.Remove(p);
        if (pack.Projects.Count == 0) pack.Projects = null;
    }

    /// One minute of the work lived. Returns why it stops: finished (made or spoiled), the place or
    /// the tools no longer serving, or death from a slip of the blade.
    public static string? Minute(World world, Agent agent, Activity activity, List<WorldEvent> events)
    {
        if (activity is not Make m) return null; // abandoning was done at the start
        var pack = Items.Pack(world, agent.Id);
        var r = Recipes.Get(m.Recipe);
        var p = ProjectFor(pack, r.Id) ?? throw new InvalidOperationException($"{agent.Id} is making {r.Id} with no project for it");
        if ((MissingTool(pack, r, begun: true) ?? Unmet(world, agent, r)) is string stop) return stop;
        p.MinutesDone += WorkPerMinute(world, agent, pack, r);
        Skills.Award(world, agent, Skill.Crafting, Skills.CraftXpPerHour / 60, events);
        return p.MinutesDone < r.Minutes - 1e-9 ? null : Finish(world, agent, r, events);
    }

    /// The recipe's minutes this minute does: more with faster tools, less with bad hands, a fogged
    /// head or clumsy fingers (old CraftingEffort, which counted bad hands both through manipulation
    /// and through the dexterity built on it; kept as ported). Read every minute, so how the work is split into
    /// steps cannot change when it finishes.
    public static double WorkPerMinute(World world, Agent agent, Inventory pack, Recipe r)
    {
        double work = 1.0;
        foreach (var cap in r.Faster) if (ToolFor(pack, cap) != null) work *= FasterToolFactor;
        var c = Body.GetCapacities(agent.Body);
        double slow = (c.Manipulation < 0.5 ? 1.25 : 1.0) * (c.Consciousness < 0.5 ? 1.25 : 1.0);
        double dexterity = Body.Dexterity(agent.Body, Fire.AbilityContextFor(world, agent));
        if (dexterity < DexterityThreshold) slow *= 1 + (DexterityThreshold - dexterity) / DexterityThreshold * MaxDexterityPenaltyPct;
        return work / slow;
    }

    static readonly Cause Slip = new(null, "a slip at work");

    /// The work is done: one outcome rolled at this minute's odds, then Complete. The sheet shown at
    /// the start is the odds as things stand then; hands that froze or a light that failed since
    /// change what is rolled, as they would.
    static string Finish(World world, Agent agent, Recipe r, List<WorldEvent> events)
    {
        var odds = Odds(world, agent, r);
        double roll = world.Rng("make", agent.Id).NextDouble();
        Outcome? outcome = null;
        foreach (var o in odds.Outcomes)
            if ((roll -= o.Odds.TotalPct) < 0) { outcome = o.Outcome; break; }
        return Complete(world, agent, r, outcome, events);
    }

    /// The project for this recipe in the agent's pack turns out as this outcome (null: the plain
    /// result): its effects, then the result or the materials back. Tests name the outcome here.
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

        // Waste and saving act on the whole input they name, whichever alternative went in.
        double wastedKg = 0;
        foreach (var e in effects)
        {
            var (named, share, waste) = e switch { WasteMaterial w => (w.Input, w.SharePct, true), SaveMaterial s => (s.Input, s.SharePct, false), _ => (default(Resource?), 0.0, false) };
            if (named is not Resource input) continue;
            foreach (var res in r.Materials.First(m => m.AnyOf.Contains(input)).AnyOf)
            {
                double kg = Items.Take(materials, res, Items.Have(materials, res) * share);
                if (kg <= 0) continue;
                if (waste) { spent[res] = spent.GetValueOrDefault(res) + kg; wastedKg += kg; }
                else Items.Add(gained, res, kg);
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
            // A resource result: the inputs are worked into it, less in proportion to what was wasted.
            // Saved material is a bonus over a full result (the loader keeps it from making mass).
            double planned = r.Materials.Sum(m => m.Kg);
            foreach (var (res, kg) in materials.Kg) spent[res] = spent.GetValueOrDefault(res) + kg;
            var made = r.Result.Resource!.Value;
            double kgMade = r.Result.Kg * Math.Clamp((planned - wastedKg) / planned, 0, 1);
            Items.Add(gained, made, kgMade);
            produced[made] = produced.GetValueOrDefault(made) + kgMade;
        }

        string? died = null;
        foreach (var e in effects)
            switch (e)
            {
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

    /// What the result is made of: the kilos that went in, and the recorded parts of the items consumed.
    static SortedDictionary<Resource, double> Composition(Inventory materials)
    {
        var parts = new SortedDictionary<Resource, double>(materials.Kg);
        foreach (var g in materials.Gear)
            foreach (var (res, kg) in g.MadeOf ?? [])
                parts[res] = parts.GetValueOrDefault(res) + kg;
        return parts;
    }

    /// A cut at work, through Body.Damage, as a fall is on the trail.
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

    /// "PlantFiber" as "plant fiber".
    static string Words(Resource r) => Words(r.ToString());
    static string Words(string name) =>
        string.Concat(name.Select((ch, i) => i > 0 && char.IsUpper(ch) ? " " + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString()));
}
