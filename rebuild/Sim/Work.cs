namespace Survival;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<WashedIn>))]
public enum WashedIn { Water, Snow, CarriedWater }

/// A focused search keeps what it looks for and drops about half of the rest.
public enum ForageFocus { General, Fuel, Food, Medicine, Materials }

/// One step of work in progress: what Begin worked out, and what the minutes have added up to.
/// An NPC's is saved with what it is doing (Agent.Doing); the player's lives for one Sim.Step, or across one job's steps when held.
public sealed class WorkSession
{
    public required Activity Activity { get; init; }
    public int Minutes { get; set; }
    public Inventory Got { get; init; } = new();
    /// What did not fit in the pack and went on the tile's pile.
    public Inventory Dropped { get; init; } = new();
    public PoolId? Pool { get; init; }
    public double TargetMinutes { get; init; }
    public double DoneKg { get; set; }
    /// A forage's luck, rolled once when it begins (Pools.RollLuck); 0 for other work.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public double LuckFactor { get; init; }
    /// The clues a forage began with, by their index in the clue table (Clues.At), and their draw (ClueDraw): a scavenge clue pays once a draw.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? Clues { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int ClueDraw { get; init; }
    /// Every find as it turned up, after focus and yield: what the forage screen shows minute by minute. Not saved:
    /// only that screen reads it, and the host saves no job part-way.
    [System.Text.Json.Serialization.JsonIgnore] public List<Find> Finds { get; } = [];
}

/// One find of a forage, in the minute it turned up.
public readonly record struct Find(long Minute, Resource Resource, double Kg);

public static class Work
{
    public const double MaxDrinkLiters = 1.0;
    public const int EatMinutes = 10, DrinkMinutes = 2;
    public const int WashMinutes = 5;
    public const double WashLiters = 0.5;
    public const string NoWashingWater = "no water or snow here";
    public const double ChopMinutesPerAxeUse = 60;

    public static bool Handles(Activity a) => a is Gather or Butcher or Eat or Drink or Wash or PutDown or PickUp or Wear or TakeOff or WalkAbout;

    /// Keep Walking: five minutes walking the ground to notice new clues.
    public const int WalkAboutMinutes = 5;

    // ---- light and ability ----

    public const string TooDark = "too dark to work";

    public const string WalkedAbout = "walked about";

    public const string Ate = "ate", Drank = "drank", WashedReason = "washed", TreeCutUp = "tree cut up", ButcheredReason = "butchered";

    public static bool TooDarkToWork(World world, Agent agent)
    {
        bool dark = Map.IsDark(Map.Of(world), agent.Tile) || Weather.IsNight(world.Minute);
        return dark && !Fire.HasLight(world, agent.Id);
    }

    /// A forecast: minutes until TooDarkToWork here, if nobody tends the light. Fires around the agent must be caught up.
    public static int MinutesUntilTooDark(World world, Agent agent) =>
        LightLeft(world, agent.Tile, Fire.MinutesOfLightLeft(world, agent.Id), 0);

    /// The same for a place it is not standing, starting work there in startInMinutes: by the light it carries and the
    /// sky, with no fire there to work by.
    public static int MinutesUntilTooDarkAt(World world, Agent agent, TileId tile, int startInMinutes)
    {
        int carried = world.Fires.Values.Where(f => f.CarriedBy == agent.Id && Fire.IsActive(f)).Select(f => (int)Math.Ceiling(f.TorchMinutesLeft)).DefaultIfEmpty(0).Max();
        return LightLeft(world, tile, Math.Max(0, carried - startInMinutes), startInMinutes);
    }

    static int LightLeft(World world, TileId tile, int litMinutes, int startInMinutes) =>
        Map.IsDark(Map.Of(world), tile) ? litMinutes : litMinutes + Weather.LightLeftMinutes(world.Minute + startInMinutes + litMinutes);

    /// A forecast: what the minutes given bring in, the agent staying as they are and stopping for the dark; a tree only if
    /// it comes down, a harvest by whole cycles. Fires around the agent must be caught up.
    public static IReadOnlyDictionary<Resource, double> ExpectedGather(World world, Agent agent, Gather g, int minutes)
    {
        if (Refusal(world, g) != null) return new SortedDictionary<Resource, double>();
        var id = new PoolId(agent.Tile, g.Pool);
        if (NeedsLight(world, Pools.Def(world, id), g)) minutes = Math.Min(minutes, MinutesUntilTooDark(world, agent));
        var clues = Pools.Def(world, id) is ForagePool ? [.. Survival.Clues.At(world, agent.Id).Select(c => c.Index)] : new List<int>();
        return Expected(world, agent, id, g, minutes, ForageYield(world, agent).TotalPct, rememberedDensity: null, clues);
    }

    /// The same for a pool elsewhere, starting there in startInMinutes, on the ground as remembered (null: as it is).
    /// What only being there can refuse (trees all felled, the pool unseen) is the caller's to know.
    public static IReadOnlyDictionary<Resource, double> ExpectedGatherAt(World world, Agent agent, PoolId id, ForageFocus focus, int minutes,
        int startInMinutes, double? rememberedDensity)
    {
        var def = Pools.Def(world, id);
        var g = new Gather(agent.Id, id.Index, focus);
        if (Items.PackOf(agent) is not { } pack || ToolRefusal(def, pack) != null) return new SortedDictionary<Resource, double>();
        if (NeedsLight(world, def, g)) minutes = Math.Min(minutes, MinutesUntilTooDarkAt(world, agent, id.Tile, startInMinutes));
        double perception = Body.Perception(agent.Body, new AbilityContext(Items.EncumbrancePct(agent), Weather.DarknessLevel(world, id.Tile), false));
        return Expected(world, agent, id, g, minutes, ForageYield(agent, pack, perception).TotalPct, rememberedDensity, []); // a place out of sight shows no clues
    }

    static SortedDictionary<Resource, double> Expected(World world, Agent agent, PoolId id, Gather g, int minutes, double yieldPct, double? rememberedDensity,
        IReadOnlyList<int> clues)
    {
        var kg = new SortedDictionary<Resource, double>();
        double effectivePct = 1 / ImpairmentFactor(world, agent, id.Tile, g), workMinutes = minutes * effectivePct;
        switch (Pools.Def(world, id))
        {
            case ForagePool:
                foreach (var (r, found) in Pools.ExpectedForage(world, id, minutes, effectivePct, rememberedDensity))
                    kg[r] = found * yieldPct * FocusFactor(r, g.Focus) * Survival.Clues.YieldFactor(clues, g.Focus, r);
                break;
            case TreePool:
                // The axe felling would pick up (Items.Axe), at its rate.
                double chopFactor = Items.PackOf(agent) is { } axePack ? Items.ChopFactor(Items.Axe(axePack)) : 1;
                foreach (var (r, got) in Pools.ExpectedChop(world, id, workMinutes * chopFactor)) kg[r] = got;
                break;
            case StockPool stock:
                int cycles = (int)Math.Floor(((Pools.StateOf(world, id) as StockState)?.MinutesWorked ?? 0) / stock.MinutesPerUnit + workMinutes / stock.MinutesPerUnit + 1e-9);
                var units = Pools.UnitsNow(world, id);
                var pack = Items.PackOf(agent);
                for (int i = 0; i < units.Length; i++)
                    if (Pools.CanWork(stock.Items[i], pack))
                        kg[stock.Items[i].Resource] = kg.GetValueOrDefault(stock.Items[i].Resource) + Math.Min(units[i], cycles) * stock.Items[i].KgPerUnit;
                break;
        }
        return kg;
    }

    static double FocusFactor(Resource r, ForageFocus focus) =>
        focus == ForageFocus.General || InFocus(r, focus) ? 1 : OffFocusKeepPct + OffFocusKeepSpreadPct / 2;

    /// Whether a focus is looking for this resource. General looks for nothing in particular.
    public static bool InFocus(Resource r, ForageFocus focus) => focus != ForageFocus.General && Items.Resources[r].Category == FocusCategory(focus);

    const double OffFocusKeepPct = 0.4, OffFocusKeepSpreadPct = 0.2;

    static ResourceCategory FocusCategory(ForageFocus focus) => focus switch
    {
        ForageFocus.Fuel => ResourceCategory.Fuel,
        ForageFocus.Food => ResourceCategory.Food,
        ForageFocus.Medicine => ResourceCategory.Medicine,
        _ => ResourceCategory.Material,
    };

    /// Work that needs to see: everything but foraging, which can be done by feel.
    static bool NeedsLight(World world, Agent agent, Activity activity) => activity switch
    {
        Butcher => true,
        Gather g => NeedsLight(world, Pools.Def(world, new PoolId(agent.Tile, g.Pool)), g),
        _ => false,
    };

    static bool NeedsLight(World world, PoolDef def, Gather g) => def is not ForagePool;

    /// Bad legs +20%, bad lungs +15%, bad hands +25% time; a frozen carcass +50%. Read every minute,
    /// so how the work is split into steps cannot change what it gets done.
    static double ImpairmentFactor(World world, Agent agent, Activity activity) => ImpairmentFactor(world, agent, agent.Tile, activity);

    static double ImpairmentFactor(World world, Agent agent, TileId tile, Activity activity)
    {
        var body = agent.Body;
        return activity switch
        {
            Gather g => Pools.Def(world, new PoolId(tile, g.Pool)) switch
            {
                ForagePool => ImpairmentFactor(body, moving: true),
                TreePool => ImpairmentFactor(body, moving: true, manipulation: true),
                StockPool { Kind: StockKind.Salvage } => ImpairmentFactor(body, moving: true),
                _ => 1.0,
            },
            // CutMinute catches the carcass up first: frozen is the air it felt last.
            Butcher b => ImpairmentFactor(body, manipulation: true) * (Carcasses.IsFrozen(world, Carcasses.Find(world, agent.Tile, b.Carcass)!) ? 1.5 : 1.0),
            _ => 1.0,
        };
    }

    static double ImpairmentFactor(BodyState body, bool moving = false, bool manipulation = false)
    {
        var c = Body.GetCapacities(body);
        double f = 1.0;
        if (moving && c.Moving < 0.5) f *= 1.20;
        if (manipulation && c.Manipulation < 0.5) f *= 1.25;
        return f;
    }

    // ---- refusal: before any time passes ----

    static Refusal? BodyRefusal(World world, Agent agent, Activity activity)
    {
        PoolDef? pool = activity is Gather g && Pools.At(world, agent.Tile) is var pools && g.Pool >= 0 && g.Pool < pools.Count ? pools[g.Pool] : null;
        return Crippled.Refusal(world, agent, Needs(activity, pool, agent.Species));
    }

    /// The faculties each verb needs above zero, for a gather the pool it works. A forage is a search too: at zero perception it would find nothing.
    public static Faculty[] Needs(Activity activity, PoolDef? pool, Species species) => activity switch
    {
        Gather when pool is ForagePool => [Faculty.Speed, Faculty.Dexterity, Faculty.Perception],
        Gather => [Faculty.Speed, Faculty.Dexterity],
        Butcher => [Faculty.Dexterity],
        WalkAbout => [Faculty.Speed, Faculty.Perception],
        Eat or Drink => [Crippled.ToEat(species)],
        _ => [],
    };

    /// Why this work cannot start, or null. Fires around the agent must be caught up.
    public static Refusal? Refusal(World world, Activity activity)
    {
        var agent = world.Agent(activity.Who);
        var pack = Items.PackOf(agent);
        if (pack == null) return new("carries nothing to work with");
        if (BodyRefusal(world, agent, activity) is { } crippled) return crippled;
        switch (activity)
        {
            case Gather g:
                {
                    var pools = Pools.At(world, agent.Tile);
                    if (g.Pool < 0 || g.Pool >= pools.Count) return new("nothing like that here");
                    var id = new PoolId(agent.Tile, g.Pool);
                    if (!Pools.Knows(world, agent.Id, id)) return new("nothing like that here"); // a hidden pool names nothing yet
                    if (Access.Refusal(world, agent.Id, new LocationFeature(LocationFeatureKind.Pool, agent.Tile, g.Pool)) is { } barred) return barred;
                    var def = pools[g.Pool];
                    if (NeedsLight(world, agent, activity) && TooDarkToWork(world, agent)) return Dark;
                    return def switch
                    {
                        TreePool when !Pools.HasTrees(world, id) => new("all the trees here have been felled"),
                        ShelterFind f => new($"the {f.Name} is shelter, not something to gather"),
                        CarcassFind or PileFind => new("nothing like that here"), // found, it lies on the tile as itself
                        StockPool s when !Pools.HasStock(world, id) => new($"the {s.Name} is picked clean"),
                        StockPool when Pools.MissingTool(world, id, pack) is ToolType t => new($"you need {Names.A(Names.Of(t))}", new MissingTool(t)),
                        TreePool when ToolRefusal(def, pack) is { } noAxe => noAxe,
                        TreePool when Pools.NextPieceKg(world, id) is double kg && kg > RoomKg(agent, pack) + 1e-9 =>
                            new(PackFull, new MissingState(Lack.PackRoomKg, kg, RoomKg(agent, pack))),
                        _ => ToolRefusal(def, pack),
                    };
                }
            case Butcher b:
                {
                    var c = Carcasses.Find(world, agent.Tile, b.Carcass);
                    if (c == null) return new("no such carcass here");
                    if (Access.Refusal(world, agent.Id, new LocationFeature(LocationFeatureKind.Carcass, agent.Tile, c.Id)) is { } barred) return barred;
                    if (Carcasses.IsButchered(c)) return new("there's nothing left to butcher");
                    if (b.Mode is ButcherMode asked && c.Mode is ButcherMode started && asked != started)
                        return new($"this carcass is being butchered {Names.Of(started)}, not {Names.Of(asked)}");
                    bool blade = Items.HasCuttingTool(pack);
                    if (Carcasses.AllCut(c, blade) && c.Pending.Count == 0) return new(NeedsABlade, new MissingCapability(Capability.Cutting));
                    if (Carcasses.AllCut(c, blade) && Items.CarriedKg(agent) >= Items.CapacityKg(pack))
                        return new(PackFull, new MissingState(Lack.PackRoomKg, Have: Items.CapacityKg(pack) - Items.CarriedKg(agent)));
                    return NeedsLight(world, agent, activity) && TooDarkToWork(world, agent) ? Dark : null;
                }
            case WalkAbout:
                return Pools.ForageAt(world, agent.Tile) == null ? new("nothing to forage here") : null;
            case Eat e:
                if (e.Kg <= 0) throw new ArgumentOutOfRangeException(nameof(activity), e.Kg, "Eat a positive amount");
                if (Items.Resources[e.Food].Category != ResourceCategory.Food) return new($"{Names.Cap(Names.Of(e.Food))} is not food");
                return Items.Have(pack, e.Food) > 0 ? null : new($"no {Names.Of(e.Food)} to eat", new MissingResource(e.Food, e.Kg));
            case Drink d:
                if (d.Liters <= 0) throw new ArgumentOutOfRangeException(nameof(activity), d.Liters, "Drink a positive amount");
                if (Items.Have(pack, Resource.Water) <= 0 && !CanDrinkHere(world, agent.Tile)) return new("no water here", new MissingResource(Resource.Water, d.Liters));
                return (Body.MaxHydrationMl - agent.Body.HydrationMl) / 1000 < 0.01 ? new("not thirsty") : null;
            case Wash:
                return WashingWater(world, agent, pack) == null
                    ? new(NoWashingWater, new MissingResource(Resource.Water, WashLiters - Items.Have(pack, Resource.Water)))
                    : null;
            case PutDown s:
                if (s.Kg <= 0) throw new ArgumentOutOfRangeException(nameof(activity), s.Kg, "Put down a positive amount");
                // Putting down, on a pile already here or the first thing that makes one: one gate.
                if (Access.PutDownRefusal(world, agent.Id, agent.Tile) is { } pileBarred) return pileBarred;
                return Items.Have(pack, s.Resource) > 0 ? null : new($"no {Names.Of(s.Resource)} to put down", new MissingResource(s.Resource, s.Kg));
            case PickUp u:
                if (u.Kg <= 0) throw new ArgumentOutOfRangeException(nameof(activity), u.Kg, "Take a positive amount");
                var pile = world.Tiles.GetValueOrDefault(agent.Tile)?.Pile;
                if (pile == null || Items.Have(pile, u.Resource) <= 0) return new($"no {Names.Of(u.Resource)} in the pile here");
                if (Access.Refusal(world, agent.Id, LocationFeatures.PileAt(world, agent.Tile)!.Value) is { } pickUpBarred) return pickUpBarred;
                if (Items.CarriedKg(agent) >= Items.CapacityKg(pack))
                    return new("your pack is full", new MissingState(Lack.PackRoomKg, Have: Items.CapacityKg(pack) - Items.CarriedKg(agent)));
                return Items.LightestKg(pile, u.Resource) is double pieceKg && pieceKg > Items.CapacityKg(pack) - Items.CarriedKg(agent) + 1e-9
                    ? new($"no room in your pack for a whole {Names.PieceWord(u.Resource)}",
                        new MissingState(Lack.PackRoomKg, pieceKg, Items.CapacityKg(pack) - Items.CarriedKg(agent)))
                    : null;
            case Wear w:
                var gear = pack.Gear.FirstOrDefault(x => x.Kind == w.Gear);
                if (gear == null) return new($"no {(Items.GearKinds.ContainsKey(w.Gear) ? Names.GearWords(w.Gear) : w.Gear)} in your pack", new MissingGear(w.Gear));
                return Items.Def(gear).Slot == null ? new($"{Items.Def(gear).Name} is not something to wear") : null;
            case TakeOff t:
                return agent.Body.Worn.Any(x => x.Kind == t.Gear) ? null : new($"you are not wearing {Names.GearWords(t.Gear)}");
            default:
                throw new NotSupportedException($"{activity.GetType().Name} is not work");
        }
    }

    static double RoomKg(Agent agent, Inventory pack) => Items.CapacityKg(pack) - Items.CarriedKg(agent);

    public static readonly Refusal Dark = new(TooDark, new MissingState(Lack.Light));

    /// The tool a pool's work needs whatever is there: an axe for trees. A stock's items each say their own (Pools.CanWork).
    static Refusal? ToolRefusal(PoolDef def, Inventory pack) => def switch
    {
        TreePool => Items.Tool(pack, ToolType.Axe) == null ? new("you need an axe to fell trees", new MissingTool(ToolType.Axe)) : null,
        _ => null,
    };

    /// Water to drink here: a river or a lake underfoot, ice or not, a site's water while it is not ice, or an
    /// open lake a step away. Ice should need a hole cut; that needs ice thickness, which Ground does not model yet.
    public static bool HasOpenWater(World world, TileId tile)
    {
        var m = Map.Of(world);
        if (m.BesideRiver(tile) || m.TerrainAt(tile) == Terrain.Water) return true;
        if (m.SiteAt(tile)?.Water != null && !Map.IsFrozen(m, tile, world.Minute)) return true;
        return MapGen.Neighbors(tile).Any(n => m.InBounds(n) && m.TerrainAt(n) == Terrain.Water && !Map.IsFrozen(m, n, world.Minute)
            && Map.Blocked(m, tile, n) == null);
    }

    /// Water a person can drink here: open water (HasOpenWater) or an unfrozen pond standing on the tile.
    public static bool CanDrinkHere(World world, TileId tile) =>
        HasOpenWater(world, tile) || Ground.Underfoot(world, tile).DrinkableWaterM >= MinStandingWaterM;

    /// HasOpenWater without the ice, for an animal, which cuts no hole.
    public static bool HasUnfrozenWater(World world, TileId tile)
    {
        var m = Map.Of(world);
        bool Open(TileId t) => Map.HoldsWater(m, t) && m.TerrainAt(t) != Terrain.DeepWater && !Map.IsFrozen(m, t, world.Minute);
        if (Open(tile)) return true;
        foreach (var d in Map.Directions)
        {
            var n = Map.Step(tile, d);
            if (m.InBounds(n) && m.TerrainAt(n) == Terrain.Water && Open(n) && Map.Blocked(m, tile, n) == null) return true;
        }
        return false;
    }

    /// Melting snow (80 kcal a litre) and warming it to the body's heat (37).
    public const double SnowKcalPerMl = 0.117;
    public const double OpenWaterDrinkPctPerMinute = 0.05, SnowDrinkPctPerMinute = 0.01, MinStandingWaterM = 0.02, MinSnowM = 0.02;

    /// A forecast: the ml a body drinks a minute here as the view knows it; open water or a pond first, else snow at its kcal.
    public static Source? WaterAt(WorldView view, TileId tile, BodyState body)
    {
        double capacityMl = Body.MaxHydrationMlOf(body);
        var ground = view.Ground(tile);
        if (view.OpenWater(tile) || ground.PondLiquidM >= MinStandingWaterM) return new Source(OpenWaterDrinkPctPerMinute * capacityMl);
        return ground.SnowDepthM >= MinSnowM ? new Source(SnowDrinkPctPerMinute * capacityMl, SnowKcalPerMl) : null;
    }

    public static void DrinkFrom(BodyState body, Source water)
    {
        double ml = Math.Min(water.PerMinute, Math.Max(0, Body.MaxHydrationMlOf(body) - body.HydrationMl));
        Body.Drink(body, ml);
        if (water.KcalPerUnit > 0) Body.SpendKcal(body, ml * water.KcalPerUnit);
    }

    /// What a wash here would use: open water or a pond first, then snow underfoot, then carried water; null with none of them.
    static WashedIn? WashingWater(World world, Agent agent, Inventory pack)
    {
        if (CanDrinkHere(world, agent.Tile)) return WashedIn.Water;
        if (Ground.Underfoot(world, agent.Tile).SnowDepthM >= MinSnowM) return WashedIn.Snow;
        return Items.Have(pack, Resource.Water) >= WashLiters ? WashedIn.CarriedWater : null;
    }

    // ---- the session ----

    /// Starts work Refusal allowed. Instant work (putting down, wear, eating and drinking) happens here.
    public static WorkSession Begin(World world, Activity activity, List<WorldEvent> events)
    {
        if (Refusal(world, activity) is { } refused) throw new InvalidOperationException($"Work.Begin on a refused activity: {refused}");
        var agent = world.Agent(activity.Who);
        var pack = Items.PackOf(agent)!;
        var body = agent.Body;
        switch (activity)
        {
            case Gather g when Pools.Def(world, new PoolId(agent.Tile, g.Pool)) is ForagePool:
                {
                    var clues = Survival.Clues.At(world, agent.Id);
                    if (agent.Id == AgentId.Player && clues.Count > 0) Tutorials.Show(world, agent, Tutorial.FirstClue, events);
                    return new WorkSession
                    {
                        Activity = activity,
                        Pool = new PoolId(agent.Tile, g.Pool),
                        LuckFactor = Pools.RollLuck(world.Rng("work", agent.Id)),
                        Clues = [.. clues.Select(c => c.Index)],
                        ClueDraw = (Pools.StateOf(world, new PoolId(agent.Tile, g.Pool)) as ForageState)?.ClueDraw ?? 0,
                    };
                }
            case Gather g:
                return new WorkSession { Activity = activity, Pool = new PoolId(agent.Tile, g.Pool) };
            case WalkAbout:
                return new WorkSession { Activity = activity, TargetMinutes = WalkAboutMinutes };
            case Butcher b:
                {
                    var c = Carcasses.Find(world, agent.Tile, b.Carcass)!;
                    c.Mode ??= b.Mode ?? ButcherMode.Careful;
                    return new WorkSession { Activity = activity };
                }
            case Eat e:
                {
                    var eaten = new Inventory();
                    double kg = Items.Move(pack, eaten, e.Food, e.Kg); // a cut of meat is eaten whole
                    var def = Items.Resources[e.Food];
                    double kcal = kg * def.KcalPerKg;
                    Body.Eat(body, kcal);
                    body.HydrationMl = Math.Clamp(body.HydrationMl + kg * def.HydrationMlPerKg, 0, Body.MaxHydrationMl);
                    events.Add(new Ate(world.Minute, agent.Id, agent.Tile, e.Food, kg, kcal, Items.Count(eaten, e.Food)));
                    return new WorkSession { Activity = activity, TargetMinutes = EatMinutes };
                }
            case Drink d:
                {
                    double room = (Body.MaxHydrationMl - body.HydrationMl) / 1000;
                    double liters = Math.Round(Math.Min(Math.Min(d.Liters, MaxDrinkLiters), room), 2);
                    if (Items.Have(pack, Resource.Water) > 0) liters = Items.Take(pack, Resource.Water, liters);
                    Body.Drink(body, liters * 1000);
                    // Cold water takes the edge off overheating.
                    if (body.Effects.FirstOrDefault(x => x.Kind == EffectKind.Hyperthermia) is { } hot)
                        hot.Severity = Math.Max(0, hot.Severity - 0.15 * (liters / 0.25));
                    events.Add(new Drank(world.Minute, agent.Id, agent.Tile, liters));
                    return new WorkSession { Activity = activity, TargetMinutes = DrinkMinutes };
                }
            case PutDown s:
                {
                    int had = Items.Count(pack, s.Resource);
                    double kg = Items.Move(pack, Items.PileAt(world, agent.Tile, new Cause(agent.Id, $"put down by {Cause.Describe(world, agent.Id)}")), s.Resource, s.Kg);
                    events.Add(new PutOnPile(world.Minute, agent.Id, agent.Tile, s.Resource, kg, had - Items.Count(pack, s.Resource)));
                    return new WorkSession { Activity = activity };
                }
            case PickUp u:
                {
                    int had = Items.Count(pack, u.Resource);
                    double kg = Items.MoveFromPile(world, agent.Tile, pack, u.Resource, u.Kg, Items.CapacityKg(pack) - Items.CarriedKg(agent));
                    bool taken = agent.Mind is not PersonMind || Memories.CampOf(world, agent) != agent.Tile;
                    events.Add(new PickedUp(world.Minute, agent.Id, agent.Tile, u.Resource, kg, taken, Items.Count(pack, u.Resource) - had));
                    return new WorkSession { Activity = activity };
                }
            case Wear w:
                {
                    var gear = pack.Gear.First(x => x.Kind == w.Gear);
                    var slot = Items.Def(gear).Slot!.Value;
                    pack.Gear.Remove(gear);
                    foreach (var off in body.Worn.Where(x => Items.Def(x).Slot == slot).ToList())
                    {
                        body.Worn.Remove(off);
                        pack.Gear.Add(off);
                    }
                    body.Worn.Add(gear);
                    return new WorkSession { Activity = activity };
                }
            case TakeOff t:
                {
                    var off = body.Worn.First(x => x.Kind == t.Gear);
                    body.Worn.Remove(off);
                    pack.Gear.Add(off);
                    return new WorkSession { Activity = activity };
                }
            case Wash:
                return new WorkSession { Activity = activity, TargetMinutes = WashMinutes };
            default:
                throw new NotSupportedException($"{activity.GetType().Name} is not work");
        }
    }

    /// One minute of work lived. Returns why to stop, if anything: done, nothing left, or too dark
    /// for the next minute - the same check Refusal makes, so a run split into steps stops where an
    /// unbroken one does.
    public static string? Minute(World world, Agent agent, WorkSession s, List<WorldEvent> events)
    {
        s.Minutes++;
        if (WorkMinute(world, agent, s, events) is string done) return done;
        return NeedsLight(world, agent, s.Activity) && TooDarkToWork(world, agent) ? TooDark : null;
    }

    static string? WorkMinute(World world, Agent agent, WorkSession s, List<WorldEvent> events)
    {
        if (s.Activity is Butcher b) return CutMinute(world, agent, b, s, events);
        double effective = 1.0 / ImpairmentFactor(world, agent, s.Activity);
        var pack = Items.PackOf(agent)!;
        switch (s.Activity)
        {
            case Gather g:
                {
                    var id = s.Pool!.Value;
                    switch (Pools.Def(world, id))
                    {
                        case ForagePool:
                            {
                                // This minute's eyes, light, tools and skill, as impairment is: not fixed when the step began.
                                double perception = Body.Perception(agent.Body, Fire.AbilityContextFor(world, agent));
                                double yieldFactor = ForageYield(agent, pack, perception).TotalPct;
                                var rng = world.Rng("work", agent.Id);
                                var finds = Pools.Forage(world, id, effective / 60.0, rng, s.LuckFactor);
                                var got = new Inventory();
                                var pieceRng = world.Rng("yield", agent.Id);
                                foreach (var (r, kg) in Focus(finds, g.Focus, world.Rng("focus", agent.Id)))
                                {
                                    double factor = yieldFactor * Survival.Clues.YieldFactor(s.Clues ?? [], g.Focus, r);
                                    if (!Items.IsDiscrete(r))
                                    {
                                        Items.Add(got, r, kg * factor);
                                        s.Finds.Add(new Find(world.Minute, r, kg * factor));
                                        continue;
                                    }
                                    int pieces = (int)Math.Floor(factor); // the factor counts pieces, never sizes them
                                    if (pieceRng.NextDouble() < factor - pieces) pieces++;
                                    for (int i = 0; i < pieces; i++)
                                    {
                                        Items.AddPiece(got, r, kg);
                                        s.Finds.Add(new Find(world.Minute, r, kg));
                                    }
                                }
                                Keep(world, agent, s, got, events);
                                Pools.Search(world, agent, perception / 60.0, events);
                                if (agent.Mind is PersonMind mind && mind.Memory.Places.TryGetValue(agent.Tile, out var place)) place.Foraged = true;
                                Skills.Award(world, agent, Skill.Foraging, Skills.ForageXpPerHour / 60, events);
                                return null;
                            }
                        case TreePool:
                            {
                                if (Items.Axe(pack) is not { } axe) return "your axe is broken";
                                // A piece the pack can't take stays on the trunk for the next trip.
                                if (Pools.NextPieceKg(world, id) is double next && next > RoomKg(agent, pack) + 1e-9) return PackFull;
                                Items.UseFor(axe, 1, ChopMinutesPerAxeUse);
                                var chop = Pools.Chop(world, id, effective * Items.ChopFactor(axe), world.Rng("work", agent.Id), RoomKg(agent, pack));
                                Keep(world, agent, s, chop.Got, events);
                                if (chop.CameDown) events.Add(new TreeFelled(world.Minute, agent.Id, id));
                                return chop.CutUp ? TreeCutUp : null;
                            }
                        case StockPool stock:
                            if (Pools.WorksWith(world, id, pack, ToolType.Axe)) Items.UseFor(Items.Axe(pack)!, 1, ChopMinutesPerAxeUse);
                            Keep(world, agent, s, Pools.WorkStock(world, id, effective, pack), events);
                            return Pools.HasStock(world, id, pack) ? null : $"the {stock.Name} is picked clean";
                        default:
                            throw new NotSupportedException("Unknown pool kind");
                    }
                }
            case Eat:
                return s.Minutes >= s.TargetMinutes ? Ate : null;
            case Drink:
                return s.Minutes >= s.TargetMinutes ? Drank : null;
            case Wash:
                {
                    if (s.Minutes < s.TargetMinutes) return null;
                    if (WashingWater(world, agent, pack) is not WashedIn source) return NoWashingWater;
                    if (source == WashedIn.CarriedWater) Items.Take(pack, Resource.Water, WashLiters);
                    Body.Wash(agent.Body);
                    events.Add(new Washed(world.Minute, agent.Id, agent.Tile, source));
                    return WashedReason;
                }
            case PutDown or PickUp or Wear or TakeOff:
                return null; // done on the first minute; the agent waits out the rest
            case WalkAbout:
                Pools.Search(world, agent, Traversal.TravelSearchPct * Body.Perception(agent.Body, Fire.AbilityContextFor(world, agent)) / 60.0, events);
                return s.Minutes >= s.TargetMinutes ? WalkedAbout : null;
            default:
                throw new NotSupportedException($"{s.Activity.GetType().Name} is not work");
        }
    }

    /// What a minute of foraging brings in, against an unhindered search: eyes and light (no better
    /// than whole), an axe and a shovel +10% each, and practice, in that order.
    public static Odds ForageYield(Agent agent, Inventory pack, double perceptionPct) =>
        new("Search", 1.0, Odds.Moving(
            OddsTerm.Scale("Eyes", Math.Min(perceptionPct, 1.0)),
            OddsTerm.Scale("Axe", Items.Tool(pack, ToolType.Axe) != null ? 1.10 : 1.0),
            OddsTerm.Scale("Shovel", Items.Tool(pack, ToolType.Shovel) != null ? 1.10 : 1.0),
            OddsTerm.Scale("Foraging", Skills.ForageYieldFactor(Skills.Level(agent, Skill.Foraging)))), MinPct: 0, MaxPct: double.MaxValue);

    public static Odds ForageYield(World world, Agent agent) =>
        ForageYield(agent, Items.PackOf(agent) ?? throw new InvalidOperationException($"{agent.Id} carries nothing"),
            Body.Perception(agent.Body, Fire.AbilityContextFor(world, agent)));

    public static void End(World world, Agent agent, WorkSession s, List<WorldEvent> events)
    {
        if (s.Activity is Butcher b) EndButcher(world, agent, b, s, events);
        bool foraged = s.Minutes > 0 && s.Pool is PoolId searched && Pools.Def(world, searched) is ForagePool;
        if (foraged) EndForage(world, agent, s, events);
        if (s.Activity is WalkAbout && s.Minutes > 0) Survival.Clues.Redraw(world, agent.Tile);
        LuckTier? luck = foraged ? Pools.TierOf(s.LuckFactor) : null;
        double? density = foraged ? Pools.ForageDensity(world, s.Pool!.Value) : null;
        if (!Items.IsEmpty(s.Got))
            events.Add(new Gathered(world.Minute, agent.Id, agent.Tile, Items.Describe(s.Got), luck, density));
        else if (foraged)
            events.Add(new Gathered(world.Minute, agent.Id, agent.Tile, "", luck, density));
        if (!Items.IsEmpty(s.Dropped))
            events.Add(new Dropped(world.Minute, agent.Id, agent.Tile, Items.Describe(s.Dropped)));
    }

    /// Cuts at this minute's decay and hands; what is ready goes in the pack now, the rest waits on the carcass.
    static string? CutMinute(World world, Agent agent, Butcher b, WorkSession s, List<WorldEvent> events)
    {
        // Old bones can weather away under the knife (Remains.Hour settles them before this minute).
        if (Carcasses.Find(world, agent.Tile, b.Carcass) is not Carcass c) return "there's nothing left to butcher";
        Carcasses.CatchUp(world, agent.Tile, c); // before anything reads whether it is frozen
        double effective = 1.0 / ImpairmentFactor(world, agent, b);
        var mode = c.Mode!.Value;
        var props = Carcasses.ModeProps(mode);
        bool handsImpaired = Body.Dexterity(agent.Body, Fire.AbilityContextFor(world, agent)) < 0.7;
        bool blade = Items.HasCuttingTool(Items.PackOf(agent)!);
        Carcasses.Cut(c, Carcasses.Prepare(c, effective, mode), blade, handsImpaired, mode,
            Skills.ButcherYieldFactor(Skills.Level(agent, Skill.Butchering)));
        Skills.Award(world, agent, Skill.Butchering, Skills.ButcherXpPerHour / 60, events);
        var ready = Carcasses.TakeReady(c);
        var leftovers = Items.Receive(world, agent, ready, events, dropExcess: false);
        Carcasses.PutBack(c, leftovers);
        Items.AddAll(s.Got, Items.Without(ready, leftovers));
        // Blood on you an hour, at most 0.5 from butchering; scent on the carcass an hour.
        double bloody = Effects.Severity(agent.Body, EffectKind.Bloody);
        if (bloody < 0.5) Effects.Set(agent.Body, EffectKind.Bloody, Math.Min(0.5, bloody + props.BloodySeverity / 60));
        c.ScentBonus += props.ScentIncrease / 60;
        Effects.Set(agent.Body, EffectKind.Dirty, Effects.Severity(agent.Body, EffectKind.Dirty) + DirtyPerButcheringHour / 60);
        if (!Carcasses.AllCut(c, blade)) return null;
        if (c.Pending.Count > 0) return PackFull; // all cut; what the pack could not take waits on the carcass
        if (!Carcasses.AllCut(c)) return NeedsABlade; // the tusks and the great hide wait on it for someone with one
        Carcasses.Remove(world, agent.Tile, c);
        return ButcheredReason;
    }

    public const string PackFull = "your pack is full";
    public const string NeedsABlade = "the rest needs a blade";

    /// One rate for every mode: the mode changes how much blood, not how long your hands are in the carcass.
    const double DirtyPerButcheringHour = 0.1;

    static void EndButcher(World world, Agent agent, Butcher b, WorkSession s, List<WorldEvent> events)
    {
        if (s.Minutes == 0) return;
        events.Add(new Butchered(world.Minute, agent.Id, agent.Tile, b.Carcass, Finished: Carcasses.Find(world, agent.Tile, b.Carcass) == null));
    }

    static void Keep(World world, Agent agent, WorkSession s, Inventory got, List<WorldEvent> events)
    {
        if (Items.IsEmpty(got)) return;
        Items.AddAll(s.Got, got);
        Items.AddAll(s.Dropped, Items.Receive(world, agent, got, events: null));
    }

    /// The focus is kept whole; each find outside it survives a 40-60% roll, on its own stream so the finds never depend on it.
    static IEnumerable<(Resource, double)> Focus(List<(Resource Resource, double Kg)> finds, ForageFocus focus, Rng rng)
    {
        if (focus == ForageFocus.General) return finds;
        var target = FocusCategory(focus);
        var kept = new List<(Resource, double)>();
        foreach (var group in finds.GroupBy(f => f.Resource))
        {
            if (Items.Resources[group.Key].Category == target)
            {
                kept.AddRange(group);
                continue;
            }
            double keepPct = OffFocusKeepPct + rng.NextDouble() * OffFocusKeepSpreadPct;
            foreach (var f in group) if (rng.NextDouble() < keepPct) kept.Add((f.Resource, f.Kg));
        }
        return kept;
    }

    /// A forage's end: a scavenge clue of the draw still showing lays its remains, the clues move on, tutorials come up.
    static void EndForage(World world, Agent agent, WorkSession s, List<WorldEvent> events)
    {
        bool drawShowing = (Pools.StateOf(world, s.Pool!.Value) as ForageState)?.ClueDraw is int draw ? draw == s.ClueDraw : s.ClueDraw == 0;
        if (drawShowing && s.Clues?.Select(i => Survival.Clues.Table.Clues[i]).FirstOrDefault(c => c.Kind == ClueKind.Scavenge) is { } scavenge)
            Scavenge(world, agent, s, scavenge, events);
        Survival.Clues.Redraw(world, agent.Tile);
        if (agent.Id != AgentId.Player || Items.PackOf(agent) is not { } pack) return;
        if (Items.Tool(pack, ToolType.Axe) != null) Tutorials.Show(world, agent, Tutorial.Axe, events);
        if (Items.Tool(pack, ToolType.Shovel) != null) Tutorials.Show(world, agent, Tutorial.Shovel, events);
        double fuelFound = Items.Totals(s.Got).Where(x => Items.Resources[x.Resource].Category == ResourceCategory.Fuel).Sum(x => x.Kg);
        double fuelHeld = Items.Totals(pack).Where(x => Items.Resources[x.Resource].Category == ResourceCategory.Fuel).Sum(x => x.Kg);
        if (Weather.DayNumber(world.Minute) == 1 && fuelFound > 0 && fuelHeld < DayOneFuelKg)
            Tutorials.Show(world, agent, Tutorial.DayOneFuel, events, DayOneFuelKg - fuelHeld);
    }

    public const double DayOneFuelKg = 8.0;

    /// Bone scraps, or on a roll a part-eaten carcass of an animal that ranges here; what is near finds it by its own search.
    static void Scavenge(World world, Agent agent, WorkSession s, ClueDef clue, List<WorldEvent> events)
    {
        var rng = Rng.ForSystem(world.Seed, $"scavenge {TileIdConverter.Format(agent.Tile)} {s.ClueDraw}");
        if (clue.Animals is not { Count: > 0 })
        {
            var bones = new Inventory();
            Items.AddPiece(bones, Resource.Bone, ScrapBoneKg[0] + rng.NextDouble() * (ScrapBoneKg[1] - ScrapBoneKg[0]));
            Keep(world, agent, s, bones, events);
            events.Add(new Scavenged(world.Minute, agent.Id, agent.Tile, null, clue.Remains!));
            return;
        }
        var animals = clue.Animals.Where(a => Survival.Clues.RangesNear(world, agent.Tile, a.Animal)).ToList();
        if (animals.Count == 0 || rng.NextDouble() >= ScavengeCarcassPct) return;
        double pick = rng.NextDouble() * animals.Sum(a => a.Weight);
        var animal = animals[^1].Animal;
        foreach (var a in animals) { if ((pick -= a.Weight) < 0) { animal = a.Animal; break; } }
        double eaten = clue.HarvestedPct![0] + rng.NextDouble() * (clue.HarvestedPct[1] - clue.HarvestedPct[0]);
        var cause = new Cause(null, Pools.FoundDead);
        var body = SpeciesTable.NewBody(animal, rng);
        if (!clue.Frozen) Combat.LeaveCarcass(world, agent.Tile, animal, body, cause, eaten);
        else Carcasses.FrozenThrough(world, agent.Tile,
            Combat.LeaveCarcass(world, agent.Tile, animal, body, cause, eaten, world.Minute - Carcasses.FrozenThroughHours * 60L));
        events.Add(new Scavenged(world.Minute, agent.Id, agent.Tile, animal, clue.Remains!));
    }

    /// The chance a scavenge clue's trail ends at a carcass: sign often leads nowhere, and a carcass is a find, not a promise.
    public const double ScavengeCarcassPct = 0.25;

    static readonly double[] ScrapBoneKg = [0.1, 0.3];

    /// The forage's mood line as it stands: lean luck shows at once, good luck once it finds something.
    public static string? Mood(WorkSession s)
    {
        if (s.LuckFactor <= 0) return null;
        var tier = Pools.TierOf(s.LuckFactor);
        bool shows = tier == LuckTier.Lean || (tier != LuckTier.Normal && (Items.HasResources(s.Got) || Items.HasResources(s.Dropped)));
        return shows ? Survival.Clues.Table.Luck[tier] : null;
    }
}
