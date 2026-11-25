namespace Survival;

/// GLOSSARY: Refusal. Missing is what getting would help, as data a planner reads; null where nothing fetched would.
public sealed record Refusal(string Reason, Missing? Missing = null)
{
    public override string ToString() => Reason;
}

public abstract record Missing;

public sealed record MissingResource(IReadOnlyList<Resource> AnyOf, double Kg) : Missing
{
    public MissingResource(Resource resource, double kg) : this([resource], kg) { }
}

public sealed record MissingTool(ToolType Tool) : Missing;

public sealed record MissingCapability(Capability AnyOf) : Missing;

public sealed record MissingGear(IReadOnlyList<string> AnyOf) : Missing
{
    public MissingGear(string kind) : this([kind]) { }
}

/// Need is the line and Have where it stands, in the unit the Lack names; both null for a state that is there or not.
public sealed record MissingState(Lack Lack, double? Need = null, double? Have = null) : Missing;

public enum Lack { Light, BurningFire, FireHeatF, FireRoomKg, PackRoomKg, MovingPct, FrostF, Shelter, Dry }

/// One thing a person can do now, ready to run (Sim.Step(world, Activity, Minutes)). An activity that
/// stops itself (a crossing, a butchering) gets a budget it never reaches. A refused verb is still listed, so the player sees why.
/// Trip (Trip.Time) is the one number dock and preview show; Risk is the injury odds the crossing rolls.
/// Durations: the lengths it can be run for, Minutes among them, where how long is the person's choice (a forage).
public sealed record Verb(string Label, Activity Activity, int Minutes, Refusal? Refused, Odds? Odds = null, TripTime? Trip = null,
    MakeOdds? Outcomes = null, Odds? Risk = null, IReadOnlyList<int>? Durations = null)
{
    public bool Allowed => Refused == null;
}

/// A fact read off a location feature's effects and place, never stored (GLOSSARY: Condition). A blocking one
/// stands in front of the location feature's verbs; Clear is the verb that ends it (none exists yet).
public sealed record Condition(string Name, bool Blocking, Verb? Clear = null);

/// Conditions are outermost first. Action is the one shown (emergent-world-design §3.5): the outermost
/// blocking condition's clearing verb; otherwise the only verb; otherwise null, which is
/// Interact when there are several (open Menu) and nothing to do when there are none.
public sealed record LocationFeatureVerbs(LocationFeature LocationFeature, string Name, IReadOnlyList<Condition> Conditions, IReadOnlyList<Verb> Menu)
{
    public Verb? Action => Conditions.FirstOrDefault(c => c.Blocking && c.Clear != null)?.Clear ?? (Menu.Count == 1 ? Menu[0] : null);
    public bool Interact => Action == null && Menu.Count > 1;
}

/// What a person can do where they stand (Verbs.At). Location features leave the person out, in LocationFeatures.At's order.
/// Walk adds a careful crossing only where quick risk is worth weighing (Trip.PaceMatters). Tile is what needs
/// nothing but the place (wait, rest, sleep, fire, drink); Pack is what can be done with what is carried.
/// Make lists every recipe, hidden none (a refused one says what it lacks), then structures (Shelters, until Make takes building over).
public sealed record Here(AgentId Who, TileId At, IReadOnlyList<LocationFeatureVerbs> LocationFeatures, IReadOnlyList<Verb> Walk, IReadOnlyList<Verb> Tile,
    IReadOnlyList<Verb> Pack, IReadOnlyList<Verb> Make)
{
    public IEnumerable<Verb> All => LocationFeatures.SelectMany(t => t.Menu).Concat(Walk).Concat(Tile).Concat(Pack).Concat(Make);
}

/// The one source of "what can I do here" (emergent-world-design §3.5), for player, NPCs, CLI, UI and
/// tests. A verb is listed when what it acts on is there; whether it can start now is its Refused.
/// Reads only: nothing is caught up, so the fires around the person must already be (any step catches them up).
public static class Verbs
{
    public const int WaitMinutes = 60, RestMinutes = 60, SleepMinutes = 8 * 60, ForageMinutes = 30, WorkMinutes = 4 * 60,
        ImproveMinutes = 10;
    public const int UntilDoneMinutes = 24 * 60;
    public static readonly IReadOnlyList<int> ForageDurations = [15, 30, 60];
    public const double FuelKg = 1.0;
    public const double TinderKg = 0.05, KindlingKg = 1.0;
    /// The least an Eat verb offers: a meal. Past what the stomach holds it goes on as fat (Body.Eat).
    public const double MealKcal = 500;

    public static Here At(World world, AgentId who)
    {
        var agent = world.Agent(who);
        var m = Map.Of(world);
        var pack = Items.PackOf(agent);
        RequireFiresCurrent(world, agent);

        var features = new List<LocationFeatureVerbs>();
        foreach (var t in LocationFeatures.At(world, agent.Tile))
            if (Seen(world, who, t))
            {
                var (name, menu) = Describe(world, who, t, pack);
                features.Add(new LocationFeatureVerbs(t, name, Conditions(world, who, t), [.. menu.Select(Ready)]));
            }

        var walk = new List<Verb>();
        foreach (var dir in Directions) walk.Add(Ready(($"Walk {Names.Of(dir)}", new Travel(who, dir), UntilDoneMinutes)));
        foreach (var dir in Directions)
            if (m.InBounds(Map.Step(agent.Tile, dir)) && Trip.PaceMatters(world, who, dir))
                walk.Add(Ready(($"Walk {Names.Of(dir)} carefully", new Travel(who, dir, Pace.Careful), UntilDoneMinutes)));

        var tile = new List<(string, Activity, int)>
        {
            ("Wait", new Wait(who), WaitMinutes),
            ("Rest", new Rest(who), RestMinutes),
            ("Sleep", new Sleep(who), SleepMinutes),
        };
        if (pack != null)
        {
            if (Fire.PitAt(world, agent.Tile) == null) tile.Add(("Start a fire", Light(who, pack, PitType.Open), 1));
            if (Items.Tool(pack, ToolType.Torch) != null) tile.Add(("Light a torch", new LightTorch(who), 1));
            if (Items.Have(pack, Resource.Water) > 0 || Work.CanDrinkHere(world, agent.Tile)) tile.Add(("Drink", new Drink(who), Work.DrinkMinutes));
            tile.Add(("Wash", new Wash(who), Work.WashMinutes));
        }
        var carried = new List<(string, Activity, int)>();
        if (pack != null)
        {
            foreach (var (r, kg) in pack.Kg)
                if (kg > 0 && Items.Resources[r].Category == ResourceCategory.Food)
                    carried.Add(($"Eat {Names.Of(r)}", new Eat(who, r, Portion(agent.Body, r, kg)), Work.EatMinutes));
            foreach (var (r, kg) in pack.Kg)
                if (kg > 0) carried.Add(($"Put down {Names.Of(r)}", new PutDown(who, r, kg), 1));
            // By kind: Wear and TakeOff name a kind, so two of one kind are one verb.
            foreach (var kind in pack.Gear.Where(g => Items.Def(g).Slot != null).Select(g => g.Kind).Distinct())
                carried.Add(($"Wear {Names.GearWords(kind)}", new Wear(who, kind), 1));
        }
        foreach (var kind in agent.Body.Worn.Select(g => g.Kind).Distinct()) carried.Add(($"Take off {Names.GearWords(kind)}", new TakeOff(who, kind), 1));

        var make = new List<(string, Activity, int)>();
        if (pack != null)
        {
            foreach (var r in Recipes.All)
                make.Add((MakeLabel(r, Making.MinutesDone(world, agent, r) != null), new Make(who, r.Id), UntilDoneMinutes));
            foreach (var p in pack.Projects ?? []) make.Add(($"Abandon the unfinished {Names.Lower(Recipes.Get(p.Recipe).Name)}", new Abandon(who, p.Recipe), 1));
        }
        return new Here(who, agent.Tile, features, walk, [.. tile.Select(Ready)], [.. carried.Select(Ready)], [.. make.Select(Ready)]);

        static string MakeLabel(Recipe r, bool begun) => (r.Result.Placed, r.Requires.Contains(Requirement.Tent), begun) switch
        {
            (true, true, false) => "Pitch the tent",
            (true, true, true) => "Go on pitching the tent",
            (true, false, false) => $"Build a {Names.Lower(r.Name)}",
            (true, false, true) => $"Go on building the {Names.Lower(r.Name)}",
            (false, _, false) => $"Make {Names.Lower(r.Name)}",
            (false, _, true) => $"Go on making {Names.Lower(r.Name)}",
        };

        Verb Ready((string Label, Activity Activity, int Minutes) v)
        {
            var refused = Sim.Refusal(world, v.Activity);
            return new(v.Label, v.Activity, v.Minutes, refused, v.Activity is StartFire s ? StartOdds(world, agent, s) : null,
                v.Activity is Travel go && refused == null ? Trip.Time(world, who, go.Dir, go.Pace) : null,
                v.Activity is Make m ? Making.Odds(world, agent, Recipes.Get(m.Recipe)) : null,
                v.Activity is Travel walk && refused == null ? Trip.Risk(world, who, walk.Dir, walk.Pace) : null,
                v.Activity is Gather g && Pools.Def(world, new PoolId(agent.Tile, g.Pool)) is ForagePool ? ForageDurations : null);
        }
    }

    static double Portion(BodyState body, Resource food, double haveKg) =>
        Math.Min(haveKg, Math.Max(Body.CaloriesCapacityKcal(body) - body.CaloriesKcal, MealKcal) / Items.Resources[food].KcalPerKg);

    static Odds? StartOdds(World world, Agent agent, StartFire s) =>
        Items.PackOf(agent) is { } pack && Items.Tool(pack, s.Tool) != null ? Fire.StartOdds(world, agent, s.Tool, s.Tinder) : null;

    static StartFire Light(AgentId who, Inventory pack, PitType pit)
    {
        var tool = Fire.Starters.FirstOrDefault(t => Items.Tool(pack, t) != null, ToolType.HandDrill);
        var tinder = Fire.Tinders.FirstOrDefault(r => Items.Have(pack, r) >= TinderKg, Resource.Tinder);
        return new StartFire(who, tool, tinder, TinderKg, Math.Min(KindlingKg, Items.Have(pack, Resource.Kindling)), pit);
    }

    /// A list read off stale fires would be wrong about light, heat and the pit: fail, never guess.
    static void RequireFiresCurrent(World world, Agent agent)
    {
        foreach (var (id, f) in world.Fires)
            if ((f.CarriedBy == agent.Id || f.CarriedBy == null && f.Tile == agent.Tile) && f.LastUpdatedMinute != world.Minute && (Fire.IsActive(f) || f.HasEmbers))
                throw new InvalidOperationException($"Fire {id.Value} is at minute {f.LastUpdatedMinute}, world at {world.Minute}: catch it up first");
    }

    static bool Seen(World world, AgentId who, LocationFeature t) => t.Kind switch
    {
        LocationFeatureKind.Agent or LocationFeatureKind.Body => t.Index != who.Value,
        LocationFeatureKind.Pool => Pools.Knows(world, who, new PoolId(t.Tile, t.Index)) && !Pools.LaysItsOwnFeature(Pools.Def(world, new PoolId(t.Tile, t.Index))),
        LocationFeatureKind.Carcass or LocationFeatureKind.Pile or LocationFeatureKind.Fire or LocationFeatureKind.Shelter or LocationFeatureKind.Track or LocationFeatureKind.Sign or LocationFeatureKind.Item or LocationFeatureKind.Trodden => true,
    };

    /// What a thing on a tile is called, the same whoever looks (and with no one there to look).
    public static string NameOf(World world, LocationFeature t) => t.Kind switch
    {
        LocationFeatureKind.Agent => Cause.Describe(world, new AgentId(t.Index)),
        LocationFeatureKind.Body => "a body",
        LocationFeatureKind.Pool => Pools.Def(world, new PoolId(t.Tile, t.Index)).Name,
        LocationFeatureKind.Carcass => Carcasses.Find(world, t.Tile, t.Index)!.Animal,
        LocationFeatureKind.Pile => "pile",
        LocationFeatureKind.Fire => $"fire ({Names.Of(Fire.Phase(world.Fires[new FireId(t.Index)]))})",
        LocationFeatureKind.Shelter => Shelters.Peek(world, t.Tile)!.Name,
        LocationFeatureKind.Track => t.TrailDirection is Direction dir ? $"{Names.Of(Tracks.TierOf(world, t.Tile, Map.Step(t.Tile, dir)))} {Names.Of(dir)}"
            : $"{Names.Of((TrackMaker)t.Index)} prints",
        LocationFeatureKind.Trodden => "trodden ground",
        LocationFeatureKind.Sign => throw new InvalidOperationException("LocationFeatures.At listed a sign, which has no ids yet"),
        LocationFeatureKind.Item => throw new InvalidOperationException("LocationFeatures.At listed an item, which has no ids yet"),
    };

    /// A herd offers a hunt (of its weakest, Fauna.PersonHunt); location features nobody can act on yet (a person, a body, prints) are listed with no verbs.
    static (string Name, List<(string Label, Activity Activity, int Minutes)> Menu) Describe(World world, AgentId who, LocationFeature t, Inventory? pack)
    {
        var have = pack?.Kg ?? [];
        return (NameOf(world, t), t.Kind switch
        {
            LocationFeatureKind.Agent => world.Agent(new AgentId(t.Index)).Mind is HerdMind ? [("Hunt", new Hunt(who, new AgentId(t.Index)), 1)] : [],
            LocationFeatureKind.Body or LocationFeatureKind.Track or LocationFeatureKind.Trodden => [],
            LocationFeatureKind.Pool => PoolVerbs(world, who, t),
            LocationFeatureKind.Carcass => CarcassVerbs(world, who, t),
            LocationFeatureKind.Pile => [.. world.Tiles[t.Tile].Pile!.Kg.Select(x => ($"Take {Names.Of(x.Key)}", (Activity)new PickUp(who, x.Key, x.Value), 1))],
            LocationFeatureKind.Fire => FireVerbs(world, who, t, pack, have),
            LocationFeatureKind.Shelter => ShelterVerbs(world, who, t, have),
            LocationFeatureKind.Sign or LocationFeatureKind.Item => throw new InvalidOperationException($"LocationFeatures.At listed a {t.Kind}, which has no ids yet"),
        });
    }

    static List<(string, Activity, int)> PoolVerbs(World world, AgentId who, LocationFeature t)
    {
        var def = Pools.Def(world, new PoolId(t.Tile, t.Index));
        if (def is ForagePool forage) return ForageVerbs(world, who, t, forage);
        (string, Activity, int)? verb = def switch
        {
            TreePool => ("Chop", new Gather(who, t.Index), WorkMinutes),
            StockPool s => (s.Kind switch { StockKind.Harvest => "Harvest", StockKind.Salvage => "Salvage", StockKind.Detail => "Examine" },
                new Gather(who, t.Index), WorkMinutes),
            ShelterFind => null, // once found it stands as a shelter, its own location feature
            CarcassFind or PileFind => null,
            _ => throw new NotSupportedException($"No verb for a {def.GetType().Name}"),
        };
        return verb is { } v ? [v] : [];
    }

    /// The old forage screen's choices: a general search, labelled with the ground's word once foraged here, one
    /// for each focus that grows here, and Keep Walking for new clues.
    static List<(string, Activity, int)> ForageVerbs(World world, AgentId who, LocationFeature t, ForagePool def)
    {
        var place = (world.Agent(who).Mind as PersonMind)?.Memory.Places.GetValueOrDefault(t.Tile);
        string general = place?.Foraged == true ? $"Forage ({Pools.QualityWord(Pools.ForageDensity(world, new PoolId(t.Tile, t.Index)))})" : "Forage";
        var menu = new List<(string, Activity, int)>();
        foreach (var (focus, _) in Pools.Foci(def))
            menu.Add((focus == ForageFocus.General ? general : $"Forage for {Names.Of(focus)}", new Gather(who, t.Index, focus), ForageMinutes));
        menu.Add(("Keep walking", new WalkAbout(who), Work.WalkAboutMinutes));
        return menu;
    }

    static List<(string, Activity, int)> CarcassVerbs(World world, AgentId who, LocationFeature t)
    {
        var c = Carcasses.Find(world, t.Tile, t.Index)!;
        var modes = c.Mode is ButcherMode begun ? [begun] : Enum.GetValues<ButcherMode>();
        return [.. modes.Select(mode => ($"Butcher ({Names.Of(mode)})", (Activity)new Butcher(who, c.Id, mode), UntilDoneMinutes))];
    }

    static List<(string, Activity, int)> FireVerbs(World world, AgentId who, LocationFeature t, Inventory? pack, IReadOnlyDictionary<Resource, double> have)
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
        return menu;
    }

    static List<(string, Activity, int)> ShelterVerbs(World world, AgentId who, LocationFeature t, IReadOnlyDictionary<Resource, double> have)
    {
        var s = Shelters.Peek(world, t.Tile)!;
        var menu = new List<(string, Activity, int)>();
        foreach (var stat in Enum.GetValues<ShelterStat>())
            foreach (var (r, _) in have)
                if (Shelters.Materials.TryGetValue(r, out var mat) && mat.Shelter)
                    menu.Add(($"Improve {Names.Of(stat)} with {Names.Of(r)}", new ImproveShelter(who, stat, r), ImproveMinutes));
        if (s.Tent != null) menu.Add(("Pack the tent", new PackTent(who), 1));
        return menu;
    }

    /// Today only the access gates in force (Access.Blocker); modifying conditions (wet, spoiled) arrive with the effects kernel.
    static List<Condition> Conditions(World world, AgentId who, LocationFeature t) =>
        Access.Blocker(world, who, t) is { Gate: not Access.Gate.None } b ? [new Condition(ConditionName(b.Gate), Blocking: true)] : [];

    static string ConditionName(Access.Gate gate) => gate switch
    {
        Access.Gate.Buried => "buried",
        Access.Gate.Occupied => "occupied",
        Access.Gate.FrozenIn => "frozen in",
        Access.Gate.AcrossWater => "across water",
        _ => throw new ArgumentOutOfRangeException(nameof(gate), gate, "Not one condition"),
    };

    static readonly Direction[] Directions = [Direction.North, Direction.East, Direction.South, Direction.West];

}
