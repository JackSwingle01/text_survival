namespace Survival;

/// One thing a person can do now, ready to run: the exact activity, and the minutes to step it for
/// (Sim.Step(world, Activity, Minutes)). An activity that stops itself (a crossing, a butchering)
/// gets a budget it never reaches. Refused is why it cannot start now, in the system's own words
/// (Sim.Refusal); a refused verb is still listed, so the player sees why. Odds, for a bet; Trip, for
/// a walk that can start: its minutes as their terms (Trip.Time), the one number dock and preview show.
/// Outcomes, for making: the recipe's odds table (Making.Odds), shown before the work is committed to.
/// Risk, for a walk that can start: the injury odds the crossing will roll (Trip.Risk), beside its time.
public sealed record Verb(string Label, Activity Activity, int Minutes, string? Refused, Odds? Odds = null, TripTime? Trip = null,
    MakeOdds? Outcomes = null, CrossingRisk? Risk = null)
{
    public bool Allowed => Refused == null;
}

/// What an action sees about a thing: a fact read off its effects and its place, never stored
/// (GLOSSARY: Condition). A blocking one stands in front of the thing's verbs; Clear is the verb that
/// changes the world so it stops being true (none exists yet: Dig, Contest and the rest are step 4.4).
public sealed record Condition(string Name, bool Blocking, Verb? Clear = null);

/// One thing here as the person sees it: what it is, its conditions (outermost first), and every
/// verb it offers (Menu). Action is the one shown (emergent-world-design §3.5): the outermost
/// blocking condition's clearing verb; otherwise the thing's only verb; otherwise null, which is
/// Interact when there are several (open Menu) and nothing to do when there are none.
public sealed record ThingVerbs(ThingRef Thing, string Name, IReadOnlyList<Condition> Conditions, IReadOnlyList<Verb> Menu)
{
    public Verb? Action => Conditions.FirstOrDefault(c => c.Blocking && c.Clear != null)?.Clear ?? (Menu.Count == 1 ? Menu[0] : null);
    public bool Interact => Action == null && Menu.Count > 1;
}

/// What a person can do where they stand (Verbs.At).
/// Things: what they can see on the tile, in Things.At's order, themselves left out.
/// Walk: a crossing each way, then a careful one each way that has hazardous ground to take carefully.
/// Tile: what needs nothing but the place - wait, rest, sleep, the fire verbs, drinking.
/// Pack: what can be done with what is carried - eat each food, put down each thing, wear each
/// garment in the pack, take off each one worn.
/// Make: every recipe, in menu order (none is hidden: a refused one says what it lacks), then every
/// structure to build here (shelters, bedding, the tent: Shelters, until Make takes building over in
/// roadmap 8), then abandoning each unfinished thing in the pack.
public sealed record Here(AgentId Who, TileId At, IReadOnlyList<ThingVerbs> Things, IReadOnlyList<Verb> Walk, IReadOnlyList<Verb> Tile,
    IReadOnlyList<Verb> Pack, IReadOnlyList<Verb> Make)
{
    /// Every verb listed: each thing's menu, then the walks, then the tile's, the pack's, then making.
    public IEnumerable<Verb> All => Things.SelectMany(t => t.Menu).Concat(Walk).Concat(Tile).Concat(Pack).Concat(Make);
}

/// The one source of "what can I do here" (emergent-world-design §3.5), for the player and NPCs
/// alike, and for the CLI, the UI host and tests. A verb is listed when what it acts on is there
/// (the pit, the fuel in the pack, the carcass); whether it can start now is its Refused. Reads
/// only: nothing is caught up, so the fires around the person must already be (they are after any
/// step, which catches them up every minute it lives).
public static class Verbs
{
    public const int WaitMinutes = 60, RestMinutes = 60, SleepMinutes = 8 * 60, ForageMinutes = 60, WorkMinutes = 4 * 60,
        ImproveMinutes = 10;
    /// The budget for an activity that stops itself: never reached.
    public const int UntilDoneMinutes = 24 * 60;
    /// What one Add fuel verb puts on: up to a kilo of it.
    public const double FuelKg = 1.0;
    /// What lighting a fire takes from the pack: a pinch of tinder, up to a kilo of kindling.
    public const double TinderKg = 0.05, KindlingKg = 1.0;
    /// The least an Eat verb offers: a meal. Past what the stomach holds it goes on as fat (Body.Eat).
    public const double MealKcal = 500;

    public static Here At(World world, AgentId who)
    {
        var agent = world.Agent(who);
        var m = Map.Of(world);
        var pack = Items.PackOf(agent);
        RequireFiresCurrent(world, agent);

        var things = new List<ThingVerbs>();
        foreach (var t in Things.At(world, agent.Tile))
            if (Seen(world, who, t))
            {
                var (name, menu) = Describe(world, who, t, pack);
                things.Add(new ThingVerbs(t, name, Conditions(world, who, t), [.. menu.Select(Ready)]));
            }

        var walk = new List<Verb>();
        foreach (var dir in Directions) walk.Add(Ready(($"Walk {Names.Of(dir)}", new Travel(who, dir), UntilDoneMinutes)));
        // Pace only changes a crossing with hazardous ground in it (Traversal.CrossingMinutes).
        foreach (var dir in Directions)
            if (m.InBounds(Map.Step(agent.Tile, dir)) && (Trip.IsHazardousNow(world, agent.Tile) || Trip.IsHazardousNow(world, Map.Step(agent.Tile, dir))))
                walk.Add(Ready(($"Walk {Names.Of(dir)} carefully", new Travel(who, dir, Pace.Careful), UntilDoneMinutes)));

        var tile = new List<(string, Activity, int)>
        {
            ("Wait", new Wait(who), WaitMinutes),
            ("Rest", new Rest(who), RestMinutes),
            ("Sleep", new Sleep(who), SleepMinutes),
        };
        if (pack != null)
        {
            // A pit's own menu lights it again; with none, lighting one makes the pit.
            if (Fire.PitAt(world, agent.Tile) == null) tile.Add(("Start a fire", Light(who, pack, PitType.Open), 1));
            if (Items.Tool(pack, ToolType.Torch) != null) tile.Add(("Light a torch", new LightTorch(who), 1));
            if (Items.Have(pack, Resource.Water) > 0 || Work.HasOpenWater(world, agent.Tile)) tile.Add(("Drink", new Drink(who), Work.DrinkMinutes));
        }
        var carried = new List<(string, Activity, int)>();
        if (pack != null)
        {
            foreach (var (r, kg) in pack.Kg)
                if (kg > 0 && Items.Resources[r].Category == ResourceCategory.Food)
                    carried.Add(($"Eat {Names.Of(r)}", new Eat(who, r, Portion(agent.Body, r, kg)), Work.EatMinutes));
            foreach (var (r, kg) in pack.Kg)
                if (kg > 0) carried.Add(($"Put down {Names.Of(r)}", new Stash(who, r, kg), 1));
            // By kind: Wear and TakeOff name a kind, so two of one kind are one verb.
            foreach (var kind in pack.Gear.Where(g => Items.Def(g).Slot != null).Select(g => g.Kind).Distinct())
                carried.Add(($"Wear {Names.GearWords(kind)}", new Wear(who, kind), 1));
        }
        foreach (var kind in agent.Body.Worn.Select(g => g.Kind).Distinct()) carried.Add(($"Take off {Names.GearWords(kind)}", new TakeOff(who, kind), 1));

        var make = new List<(string, Activity, int)>();
        if (pack != null)
        {
            foreach (var r in Recipes.All)
                make.Add((Making.ProjectFor(pack, r.Id) != null ? $"Go on making {Names.Lower(r.Name)}" : $"Make {Names.Lower(r.Name)}", new Make(who, r.Id), UntilDoneMinutes));
            var building = world.Projects.GetValueOrDefault(agent.Tile)?.Building;
            foreach (var s in Enum.GetValues<Structure>())
                make.Add((s == Structure.Tent ? (building == s ? "Go on pitching the tent" : "Pitch the tent")
                    : building == s ? $"Go on building the {Names.Of(s)}" : $"Build a {Names.Of(s)}", new Build(who, s), UntilDoneMinutes));
            foreach (var p in pack.Projects ?? []) make.Add(($"Abandon the unfinished {Names.Lower(Recipes.Get(p.Recipe).Name)}", new Abandon(who, p.Recipe), 1));
        }
        return new Here(who, agent.Tile, things, walk, [.. tile.Select(Ready)], [.. carried.Select(Ready)], [.. make.Select(Ready)]);

        Verb Ready((string Label, Activity Activity, int Minutes) v)
        {
            var refused = Sim.Refusal(world, v.Activity);
            return new(v.Label, v.Activity, v.Minutes, refused, v.Activity is StartFire s ? StartOdds(world, agent, s) : null,
                v.Activity is Travel go && refused == null ? Trip.Time(world, who, go.Dir, go.Pace) : null,
                v.Activity is Make m ? Making.Odds(world, agent, Recipes.Get(m.Recipe)) : null,
                v.Activity is Travel walk && refused == null ? Trip.Risk(world, who, walk.Dir, walk.Pace) : null);
        }
    }

    /// What an Eat verb offers of a food: what the stomach has room for, and at least a meal, of what there is.
    static double Portion(BodyState body, Resource food, double haveKg) =>
        Math.Min(haveKg, Math.Max(Body.CaloriesCapacityKcal(body) - body.CaloriesKcal, MealKcal) / Items.Resources[food].KcalPerKg);

    /// The fire-start odds for a verb that has the tool to strike with: no tool, no bet to show.
    static Odds? StartOdds(World world, Agent agent, StartFire s) =>
        Items.PackOf(agent) is { } pack && Items.Tool(pack, s.Tool) != null ? Fire.StartOdds(world, agent, s.Tool, s.Tinder) : null;

    /// Lighting a fire with the best starter and tinder in the pack, and up to a kilo of kindling.
    static StartFire Light(AgentId who, Inventory pack, PitType pit)
    {
        var tool = new[] { ToolType.FireStriker, ToolType.BowDrill, ToolType.HandDrill }.FirstOrDefault(t => Items.Tool(pack, t) != null, ToolType.HandDrill);
        var tinder = new[] { Resource.BirchBark, Resource.Amadou, Resource.Usnea, Resource.Chaga, Resource.Tinder }
            .FirstOrDefault(r => Items.Have(pack, r) >= TinderKg, Resource.Tinder);
        return new StartFire(who, tool, tinder, TinderKg, Math.Min(KindlingKg, Items.Have(pack, Resource.Kindling)), pit);
    }

    /// A list read off stale fires would be wrong about light, heat and the pit: fail, never guess.
    /// A cold pit has nothing to catch up.
    static void RequireFiresCurrent(World world, Agent agent)
    {
        foreach (var (id, f) in world.Fires)
            if ((f.CarriedBy == agent.Id || f.CarriedBy == null && f.Tile == agent.Tile) && f.LastUpdatedMinute != world.Minute && (Fire.IsActive(f) || f.HasEmbers))
                throw new InvalidOperationException($"Fire {id.Value} is at minute {f.LastUpdatedMinute}, world at {world.Minute}: catch it up first");
    }

    /// Whether the person sees the thing at all: not themselves, and not a hidden pool they have not found.
    static bool Seen(World world, AgentId who, ThingRef t) => t.Kind switch
    {
        ThingKind.Agent or ThingKind.Body => t.Index != who.Value,
        ThingKind.Pool => Pools.Knows(world, who, new PoolId(t.Tile, t.Index)),
        ThingKind.Carcass or ThingKind.Stash or ThingKind.Fire or ThingKind.Shelter or ThingKind.Track or ThingKind.Sign or ThingKind.Item => true,
    };

    /// What a thing is called and the verbs it offers, before refusals: (label, activity, minutes).
    /// Things nobody can act on yet (another agent, a body, prints) are listed with no verbs.
    static (string Name, List<(string Label, Activity Activity, int Minutes)> Menu) Describe(World world, AgentId who, ThingRef t, Inventory? pack)
    {
        var have = pack?.Kg ?? [];
        return t.Kind switch
        {
            ThingKind.Agent => (Cause.Describe(world, new AgentId(t.Index)), []),
            ThingKind.Body => ("a body", []),
            ThingKind.Pool => PoolVerbs(world, who, t),
            ThingKind.Carcass => CarcassVerbs(world, who, t),
            ThingKind.Stash => ("stash", [.. world.Tiles[t.Tile].Stash!.Kg.Select(x => ($"Take {Names.Of(x.Key)}", (Activity)new Unstash(who, x.Key, x.Value), 1))]),
            ThingKind.Fire => FireVerbs(world, who, t, pack, have),
            ThingKind.Shelter => ShelterVerbs(world, who, t, have),
            ThingKind.Track => (t.TrailDirection is Direction dir ? $"{Names.Of(Tracks.TierOf(world, t.Tile, Map.Step(t.Tile, dir)))} {Names.Of(dir)}"
                : $"{Names.Of((TrackMaker)t.Index)} prints", []),
            ThingKind.Sign => throw new InvalidOperationException("Things.At listed a sign, which has no ids yet"),
            ThingKind.Item => throw new InvalidOperationException("Things.At listed an item, which has no ids yet"),
        };
    }

    static (string, List<(string, Activity, int)>) PoolVerbs(World world, AgentId who, ThingRef t)
    {
        var def = Pools.Def(world, new PoolId(t.Tile, t.Index));
        (string, Activity, int)? verb = def switch
        {
            ForagePool => ("Forage", new Gather(who, t.Index), ForageMinutes),
            TreePool => ("Chop", new Gather(who, t.Index), WorkMinutes),
            StockPool s => (s.Kind switch { StockKind.Harvest => "Harvest", StockKind.Salvage => "Salvage", StockKind.Detail => "Examine" },
                new Gather(who, t.Index), WorkMinutes),
            ShelterFind => null, // once found it stands as a shelter, which is its own thing
            _ => throw new NotSupportedException($"No verb for a {def.GetType().Name}"),
        };
        return (def.Name, verb is { } v ? [v] : []);
    }

    static (string, List<(string, Activity, int)>) CarcassVerbs(World world, AgentId who, ThingRef t)
    {
        var c = Carcasses.Find(world, t.Tile, t.Index)!;
        // A carcass keeps the way it was first butchered.
        var modes = c.Mode is ButcherMode begun ? [begun] : Enum.GetValues<ButcherMode>();
        return (c.Animal, [.. modes.Select(mode => ($"Butcher ({Names.Of(mode)})", (Activity)new Butcher(who, c.Id, mode), UntilDoneMinutes))]);
    }

    static (string, List<(string, Activity, int)>) FireVerbs(World world, AgentId who, ThingRef t, Inventory? pack, IReadOnlyDictionary<Resource, double> have)
    {
        var pit = world.Fires[new FireId(t.Index)];
        var menu = new List<(string, Activity, int)>();
        foreach (var (r, kg) in have)
            if (Fire.IsFuel(r)) menu.Add(($"Add {Names.Of(r)}", new AddFuel(who, r, Math.Min(FuelKg, kg)), 1));
        if (!Fire.IsActive(pit) && pack != null) menu.Add(("Light the fire", Light(who, pack, pit.Pit), 1));
        if (pit.CharcoalKg > 0) menu.Add(("Gather charcoal", new GatherCharcoal(who), 1));
        foreach (var raw in (ReadOnlySpan<Resource>)[Resource.RawMeat, Resource.RawFish])
            if (have.GetValueOrDefault(raw) > 0) menu.Add(($"Cook {Names.Of(raw)}", new Cook(who, raw, have[raw]), UntilDoneMinutes));
        menu.Add(("Melt snow", new MeltSnow(who), Work.MeltSnowMinutes));
        return ($"fire ({Names.Of(Fire.Phase(pit))})", menu);
    }

    static (string, List<(string, Activity, int)>) ShelterVerbs(World world, AgentId who, ThingRef t, IReadOnlyDictionary<Resource, double> have)
    {
        var s = Shelters.Peek(world, t.Tile)!;
        var menu = new List<(string, Activity, int)>();
        foreach (var stat in Enum.GetValues<ShelterStat>())
            foreach (var (r, _) in have)
                if (Shelters.Materials.TryGetValue(r, out var mat) && mat.Shelter)
                    menu.Add(($"Improve {Names.Of(stat)} with {Names.Of(r)}", new ImproveShelter(who, stat, r), ImproveMinutes));
        if (s.Tent != null) menu.Add(("Pack the tent", new PackTent(who), 1));
        return (s.Name, menu);
    }

    /// The conditions on a thing, outermost first. Today the only ones are the access gates in force
    /// (Access.Blocker), and none has a clearing verb yet; modifying conditions (wet, spoiled) arrive
    /// with the effects kernel and step 4.4.
    static List<Condition> Conditions(World world, AgentId who, ThingRef t) =>
        Access.Blocker(world, who, t) is { Gate: not Access.Gate.None } b ? [new Condition(ConditionName(b.Gate), Blocking: true)] : [];

    static string ConditionName(Access.Gate gate) => gate switch
    {
        Access.Gate.Buried => "buried",
        Access.Gate.Guarded => "occupied",
        Access.Gate.FrozenIn => "frozen in",
        Access.Gate.AcrossWater => "across water",
        _ => throw new ArgumentOutOfRangeException(nameof(gate), gate, "Not one condition"),
    };

    static readonly Direction[] Directions = [Direction.North, Direction.East, Direction.South, Direction.West];

}
