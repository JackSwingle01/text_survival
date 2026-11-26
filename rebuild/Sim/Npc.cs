using System.Text.Json.Serialization;
using static Survival.Priorities;

namespace Survival;

/// Beyond the pack and memory every person has.
public sealed partial record NpcMind
{
    /// The camp it lives from: its fire and its pile. A warmth plan may move it (the fire's site is a one of).
    public TileId Camp { get; set; }
    public NpcPlan? Plan { get; set; }
    /// The fat this body is used to (Npc.AdaptTargetFat): falling short of it makes food a Need.
    public double TargetFatKg { get; set; }
    public long TargetFatMinute { get; set; }
    [JsonIgnore] public NpcDecision? LastDecision { get; set; }
}

/// The decision in force (not saved): its minute, the stat its activity serves (null: none), and the stats as ranked then.
public sealed record NpcDecision(long Minute, Stat? Serves, IReadOnlyList<(Stat Stat, Priority Priority)> Ranked);

/// A committed plan: the stat, the priority it was taken at, the minutes the forecast gave it, and the source chosen (At).
public sealed record NpcPlan(
    [property: JsonConverter(typeof(JsonStringEnumConverter<Stat>))] Stat Stat,
    [property: JsonConverter(typeof(JsonStringEnumConverter<Priority>))] Priority Priority,
    long StartedMinute, int BudgetMinutes, TileId? At = null);

/// Worst priority first; within a priority, warmth, water, rest, food. The top stat becomes a committed plan: water, food
/// and rest from the shared planner over its memory (MemoryView), a fire's makings from a requirement tree built fresh
/// every decision ("all of", each child a "one of" its sources). The survival forecast filters both. It reads the world
/// only through verbs, refusals, forecasts and its own memory.
public static class Npc
{

    // The NPC's own numbers. A plan looks SearchTiles from it and its camp; a source is
    // planned for MaxStintMinutes at a time, worked StintMinutes between decisions.
    const int SearchTiles = 10;
    const int MaxStintMinutes = 60, StintMinutes = 30, MinStintMinutes = 10;
    const double BudgetFactor = 1.5;
    const int MinBudgetMinutes = 60;
    /// A new fire site must be clearly quicker than the camp: the forecasts are too rough to split hairs over.
    const double SiteMoveFactor = 0.5;
    const int SiteMoveMinutes = 30, SiteCandidates = 6;
    /// Minutes of walking a certain injury is worth avoiding.
    const double InjuryPriceMinutes = 60;
    /// Half an hour short of the night is a night: the fire forecast is rough.
    const double NightSlackHours = 0.5;
    /// Tinder for StrikesPerFire expected fires at the strike's odds.
    const double StrikesPerFire = 1.5, LeastStrikePct = 0.05;
    const int MinStrikes = 2, MaxStrikes = 8;
    const int WakeBeforeOutMinutes = 30, MaxSleepMinutes = 240, MinSleepMinutes = 15;
    /// A sleeper with no fire wakes this far above the Critical line, with hands still good enough to strike with.
    const double ColdSleepBufferF = 0.9;
    const int ThreatMemoryMinutes = 60, ThreatTiles = 2;
    const double DustKg = 0.01, FullPackKg = 0.3;
    /// With less energy than the walk to camp plus this, it sleeps where it is.
    const double WalkReserveEnergyMinutes = 30;
    const double MeltFireHours = 1, PickUpFuelKg = 3;
    /// A fire's source is kept until another is this many times quicker: here is judged as seen, away as remembered.
    const double KeepSourceFactor = 2;
    const double StrikeReserveKg = Verbs.KindlingKg + MaxStrikes * Verbs.TinderKg;
    public const double TargetFatHalfLifeDays = 3;

    // ---- spawning ----

    public static AgentId Spawn(World world, TileId tile, TileId? camp = null)
    {
        var id = new AgentId(world.NextAgentId++);
        if (world.Agents.ContainsKey(id)) throw new InvalidOperationException($"Agent id {id} is already taken");
        var pack = Items.StartingPack();
        pack.Gear.Add(Items.Make("ground_stone_axe"));
        pack.Gear.Add(Items.Make("flint_knife"));
        var mind = new NpcMind { Pack = pack, Camp = camp ?? tile, TargetFatMinute = world.Minute };
        var body = SpeciesTable.NewBody(Species.Human, world.Birth("person", id));
        world.Agents[id] = new Agent { Id = id, Tile = tile, Mind = mind, Members = [body] };
        mind.TargetFatKg = Math.Clamp(body.FatKg, FatFloorKg(body), Body.BaselineFatPct * body.WeightKg);
        return id;
    }

    /// An average of the body's fat over TargetFatHalfLifeDays, between the floor and the baseline.
    public static void AdaptTargetFat(NpcMind mind, BodyState body, long minute)
    {
        long minutes = minute - mind.TargetFatMinute;
        if (minutes < 0) throw new InvalidOperationException($"TargetFatKg is at minute {mind.TargetFatMinute}, ahead of {minute}");
        double keep = Math.Pow(0.5, minutes / (TargetFatHalfLifeDays * 1440));
        mind.TargetFatKg = Math.Clamp(body.FatKg + (mind.TargetFatKg - body.FatKg) * keep, FatFloorKg(body), Body.BaselineFatPct * body.WeightKg);
        mind.TargetFatMinute = minute;
    }

    public static (Activity Activity, int Minutes) Decide(World world, Agent agent, List<WorldEvent> events)
    {
        var mind = agent.Mind as NpcMind ?? throw new ArgumentException($"{agent.Id} has no NPC mind", nameof(agent));
        if (!Sim.IsAlive(agent)) throw new InvalidOperationException($"{agent.Id} is dead and cannot decide");
        Memories.Observe(world, agent, events);
        AdaptTargetFat(mind, agent.Body, world.Minute);
        var next = new Mind(world, agent, mind, events).Decide();
        return (next.Activity, next.Minutes);
    }

    // ---- the tree ----

    readonly record struct Next(Activity Activity, int Minutes, double ForecastMinutes, TileId? At = null);

    /// A step, or what it lacks with nowhere to look, or why it is impossible. None of the three is a hole (NeedStuck).
    readonly record struct Pursuit(Next? Step, string? Lacks = null, string? Impossible = null)
    {
        public static implicit operator Pursuit(Next? step) => new(step);
        public static Pursuit Lacking(string what) => new(null, Lacks: what);
        public static Pursuit Cannot(string why) => new(null, Impossible: why);
        public string? Reason => Lacks is { } l ? $"lacks {l}" : Impossible is { } i ? $"impossible: {i}" : null;
    }

    /// An "all of" child: an amount of any of these resources, each kilo worth PerKg units of it. Counted: what is
    /// to hand is already in the amount, so only sources away fill it. UsedPerHour: what the time spent getting it
    /// uses up of it, so a source must bring more than its trip takes.
    sealed record Req(string Name, IReadOnlyList<Resource> AnyOf, Func<Resource, double> PerKg, double Amount, ForageFocus Focus,
        bool Counted = false, double UsedPerHour = 0);

    enum Via { PickUp, Gather, Butcher }

    sealed record Leaf(Via Via, TileId Tile, int Index, Req For, double Units, double Kg, int WorkMinutes);

    sealed class Tree
    {
        public List<Leaf> Leaves { get; } = [];
        public List<string> Missing { get; } = [];
        /// Why no fire can be had at all: in Missing too, for every other reader.
        public string? Impossible { get; set; }
        public double ForecastMinutes { get; set; }
    }

    static readonly Resource[] FoodResources = [.. Items.Resources.Where(x => x.Value.Category == ResourceCategory.Food).Select(x => x.Key).Order()];

    /// The fire's recipes as its verbs offer them: those that cook food from a piece put on, and the one that melts snow.
    static IEnumerable<Recipe> CookRecipes => Stations.FireRecipes.Where(r => !Stations.FromNothing(r) && Items.Resources[r.Result.Resource!.Value].Category == ResourceCategory.Food);
    static Recipe MeltRecipe => Stations.FireRecipes.Single(r => Stations.FromNothing(r) && r.Result.Resource == Resource.Water);
    static bool Cookable(Resource r) => CookRecipes.Any(c => Stations.InputOf(c) == r);

    /// What the fire forecast says is missing, to fetch: any of its fuels, by weight. Counted: the pack and pile are in the forecast.
    static Req NeedReq(FireNeed need, double kg, double usedPerHour) =>
        new(need.Kind.ToString().ToLowerInvariant(), need.AnyOf, _ => 1, kg, ForageFocus.Fuel, Counted: true, usedPerHour);
    static Req FoodReq(double kcal) => new("food", FoodResources, r => Items.Resources[r].KcalPerKg, kcal, ForageFocus.Food);

    static Req Of(MissingResource m) => new(string.Join("/", m.AnyOf), m.AnyOf, _ => 1, m.Kg,
        Items.Resources[m.AnyOf[0]].Category switch { ResourceCategory.Fuel => ForageFocus.Fuel, ResourceCategory.Food => ForageFocus.Food, _ => ForageFocus.Materials });

    sealed class Mind(World world, Agent agent, NpcMind mind, List<WorldEvent> events)
    {
        readonly Memory memory = Memories.Of(agent);
        readonly Inventory pack = mind.Pack;
        readonly GeneratedMap map = Map.Of(world);
        AgentId Id => agent.Id;
        TileId Here => agent.Tile;
        BodyState Body => agent.Body;
        TileId Camp => mind.Camp;
        bool AtCamp => Here == Camp;
        double RoomKg => Items.CapacityKg(pack) - Items.CarriedKg(agent);
        Inventory? PileHere => world.Tiles.GetValueOrDefault(Here)?.Pile;
        FireState? PitHere => Fire.CurrentPitAt(world, Here);
        bool BurningHere => PitHere is { } p && Fire.IsActive(p);
        bool LitHere => BurningHere || PitHere is { HasEmbers: true };
        double? fireHereHours;
        double FireHereHours => fireHereHours ??= BurningHere ? Fire.HoursItWillBurn(PitHere!) : 0;
        bool Cold => CorePriority(Body.CoreTempF, mind.Plan?.Stat == Stat.Warmth) >= Priority.Need;

        RouteMap? fromHere;
        readonly Dictionary<TileId, RouteMap> toSite = [];
        readonly Dictionary<(PoolId, ForageFocus, int), IReadOnlyDictionary<Resource, double>> expected = [];
        Dictionary<Stat, Priority>? priorities;
        Dictionary<Stat, Priority> PrioritiesNow => priorities ??= Priorities();

        // ---- the decision ----

        /// A stat at Need or Critical passed over is told (NeedDropped); no answer is a hole (NeedStuck), and it explores.
        public Next Decide()
        {
            ranked = [.. Ranked()];
            if (Threat() is TileId threat && !BurningHere && Flee(threat) is { } flee) return Chose(flee, null, "a predator close by");
            if (mind.Plan is { } worse && PrioritiesNow[worse.Stat] > worse.Priority) mind.Plan = worse with { Priority = PrioritiesNow[worse.Stat] };
            if (mind.Plan is { } held && Interrupt(held) != null) (mind.Plan, priorities) = (null, null);
            ranked = [.. Ranked()];
            // Health has no rank on the ladder: bleeding badly by a fire with nothing Critical, it sleeps, which mends fastest.
            if (HealthPriority(Body) == Priority.Critical && !PrioritiesNow.Values.Contains(Priority.Critical) && AtCamp && BurningHere && SleepHere() is { } mend)
                return Chose(mend, Stat.Rest, "bleeding: it mends asleep by the fire");
            // A Need met from what is in hand costs minutes, not a trip: it is met on the spot, before the plan goes on.
            foreach (var (stat, priority) in ranked.Where(x => x.Priority >= Priority.Need))
                if (InHand(stat) is { } quick) return Chose(quick, stat, $"{stat.ToString().ToLowerInvariant()} met first from what is in hand");
            var kept = mind.Plan;
            if (kept != null && Pursue(kept.Stat, kept.Priority).Step is { } step)
            {
                mind.Plan = kept with { At = step.At };
                return Chose(step, kept.Stat, $"held to its {kept.Stat.ToString().ToLowerInvariant()} plan");
            }
            mind.Plan = null;
            foreach (var (stat, priority) in ranked)
            {
                var pursuit = Pursue(stat, priority);
                if (pursuit.Step is { } next)
                {
                    mind.Plan = new NpcPlan(stat, priority, world.Minute, (int)Math.Max(MinBudgetMinutes, BudgetFactor * next.ForecastMinutes), next.At);
                    return Chose(next, stat, null);
                }
                if (priority < Priority.Need) continue;
                if (pursuit.Reason is { } why)
                {
                    Drop(stat, priority, why);
                    continue;
                }
                events.Add(new NeedStuck(world.Minute, Id, Here, stat, priority, kept));
                if (Explore() is { } look)
                {
                    mind.Plan = new NpcPlan(stat, priority, world.Minute, MinBudgetMinutes);
                    return Chose(look, stat, null);
                }
                Drop(stat, priority, "stuck, with nowhere to explore");
            }
            return Chose(Keep(), null, "nothing else to do");
        }

        List<(Stat Stat, Priority Priority)> ranked = [];
        readonly HashSet<Stat> dropped = [];

        void Drop(Stat stat, Priority priority, string reason)
        {
            if (dropped.Add(stat)) events.Add(new NeedDropped(world.Minute, Id, Here, stat, priority, reason));
        }

        /// The step taken, serving this stat (null: none): every stat at Need or Critical ranked above it is passed over for the reason.
        Next Chose(Next next, Stat? serves, string? reason)
        {
            foreach (var (stat, priority) in ranked.TakeWhile(x => x.Stat != serves).Where(x => x.Priority >= Priority.Need && !dropped.Contains(x.Stat)))
                Drop(stat, priority, reason ?? throw new InvalidOperationException($"{Id} passes over {stat} at {priority} with no reason"));
            mind.LastDecision = new NpcDecision(world.Minute, serves, ranked);
            return next;
        }

        /// The interrupts: a worse priority, the time budget run out, the walk to camp turned dangerous, the plan done.
        public string? Interrupt(NpcPlan plan)
        {
            if (PrioritiesNow.Any(t => t.Key != plan.Stat && t.Value > plan.Priority)) return "a worse priority";
            if (world.Minute - plan.StartedMinute > plan.BudgetMinutes) return "the time budget";
            if (!AtCamp && plan.Stat != Stat.Warmth && CorePriority(CoreAtCampF(), false) == Priority.Critical) return "the way to camp";
            return PrioritiesNow[plan.Stat] < plan.Priority ? "done" : null;
        }

        IEnumerable<(Stat Stat, Priority Priority)> Ranked() =>
            Ladder.Select(s => (Stat: s, Priority: PrioritiesNow[s])).Where(x => x.Priority > Priority.None).OrderByDescending(x => x.Priority).ThenBy(x => Array.IndexOf(Ladder, x.Stat));

        Next? InHand(Stat stat) => stat switch
        {
            Stat.Warmth => AtCamp && Cold ? LightOrTend(HoursToSunrise()) : null,
            Stat.Water => Sim.Refusal(world, new Drink(Id)) == null ? new(new Drink(Id), Work.DrinkMinutes, Work.DrinkMinutes)
                : PileHere is { } s && Items.Have(s, Resource.Water) > DustKg ? Take(Resource.Water, Items.Have(s, Resource.Water), [])
                : MeltHere(),
            Stat.Food => EatToHand(),
            _ => null,
        };

        Pursuit Pursue(Stat stat, Priority priority) => stat switch
        {
            Stat.Warmth => ForWarmth(priority),
            Stat.Water => ForWater(priority),
            Stat.Rest => ForRest(priority),
            _ => ForFood(priority),
        };

        // ---- the ladder ----

        Dictionary<Stat, Priority> Priorities()
        {
            bool seeing(Stat s) => mind.Plan?.Stat == s;
            double waterToHandL = Work.CanDrinkHere(world, Here) ? double.PositiveInfinity
                : Items.Have(pack, Resource.Water) + (PileHere is { } s ? Items.Have(s, Resource.Water) : 0) + (StationHere is { } st ? Items.Have(st.Ready, Resource.Water) : 0);
            return new()
            {
                [Stat.Warmth] = WarmthPriority(CoreAtCampF(), seeing(Stat.Warmth), RememberedLeftAt(Camp) + ForecastAt(Camp, 0).Hours, TonightHours() - NightSlackHours),
                [Stat.Water] = WaterPriority(Body.HydrationPct, seeing(Stat.Water), waterToHandL, (1440 - Weather.MinuteOfDay(world.Minute)) / 60.0),
                [Stat.Rest] = RestPriority(Body.EnergyPct, seeing(Stat.Rest)),
                [Stat.Food] = FoodPriority(Body, mind.TargetFatKg, seeing(Stat.Food), foodToHand: false),
            };
        }

        double HoursToSunrise() => HoursUntil(Weather.SunriseHour(world.Minute));

        double TonightHours() => Weather.IsDaytime(world.Minute) ? HoursToSunrise() - HoursUntil(Weather.SunsetHour(world.Minute)) : HoursToSunrise();

        double HoursUntil(int hourOfDay) => (hourOfDay * 60 - Weather.MinuteOfDay(world.Minute) + 1440) % 1440 / 60.0;

        readonly Dictionary<(TileId, double), (FireForecast Forecast, bool Lit)> forecasts = [];

        FireForecast ForecastAt(TileId site, double hoursWanted) => SiteForecast(site, hoursWanted).Forecast;

        /// From away, a new pit and the pile as remembered, for the hours the fire remembered there leaves wanting; lit if it outlasts the walk back.
        (FireForecast Forecast, bool Lit) SiteForecast(TileId site, double hoursWanted)
        {
            hoursWanted = Math.Max(0, hoursWanted - RememberedLeftAt(site));
            if (forecasts.TryGetValue((site, hoursWanted), out var known)) return known;
            FireState? pit;
            Inventory fuel;
            if (site == Here) (pit, fuel) = (PitHere, PackWith(PileHere));
            else (pit, fuel) = (null, PackWithRemembered(memory.Places.GetValueOrDefault(site)?.Pile));
            bool lit = pit != null ? Fire.IsActive(pit) || pit.HasEmbers : OutlastsTheWalk(site);
            var starter = Starter ?? (lit && pit == null ? Fire.Starters[0] : null); // fuel goes on a fire still lit: no strike to lack
            return forecasts[(site, hoursWanted)] = (Fire.Forecast(new FireAsk(pit, PitType.Open, fuel, starter, hoursWanted)), lit);
        }

        ToolType? Starter => Fire.Starters.Where(t => Items.Tool(pack, t) != null).Cast<ToolType?>().FirstOrDefault();

        Inventory PackWith(Inventory? pile)
        {
            var all = Items.CopyResources(pack);
            if (pile != null) Items.AddAll(all, Items.CopyResources(pile));
            return all;
        }

        /// The pack with a pile as remembered, its weights in the pieces they would be fetched in.
        Inventory PackWithRemembered(IReadOnlyDictionary<Resource, double>? pile)
        {
            var all = Items.CopyResources(pack);
            foreach (var (r, kg) in pile ?? new Dictionary<Resource, double>()) Items.AddKg(all, r, kg);
            return all;
        }

        double CoreAtCampF() => AtCamp ? Body.CoreTempF : CoreAfterF(WalkLegs(Here, Camp));

        double CoreAfterF(IEnumerable<(Activity, TileId, int)> legs)
        {
            var list = legs.ToList();
            foreach (var t in list.Select(l => l.Item2).Distinct()) Fire.CatchUpOn(world, t, events);
            return Sim.CoreAfterF(world, Id, list);
        }

        /// A leg on each tile it leaves: from here by the routes out, from elsewhere by the routes in to where it goes.
        IEnumerable<(Activity, TileId, int)> WalkLegs(TileId from, TileId to)
        {
            if (from == to) yield break;
            var routes = from == Here ? FromHere() : ToSite(to);
            var end = from == Here ? to : from;
            if (!routes.Reaches(end)) yield break;
            var path = from == Here ? routes.RouteTo(end) : routes.RouteBack(end);
            var at = from;
            foreach (var next in path)
            {
                yield return (new Travel(Id, Direction.North), at, Trip.StepMinutes(world, Id, at, next, Pace.Quick));
                at = next;
            }
        }

        // ---- warmth: a fire at the site that lasts the night, and a core warmed beside it ----

        Pursuit ForWarmth(Priority priority)
        {
            var camp = Camp;
            if (mind.Plan?.Stat != Stat.Warmth) mind.Camp = ChooseSite(priority);
            var plan = FirePlan(Camp, HoursWanted(priority));
            var step = AtFire(plan, priority) ?? NextLeaf(plan);
            if (step == null && priority >= Priority.Need && plan.Impossible is { } impossible)
            {
                mind.Camp = camp;
                return Pursuit.Cannot($"a fire, without {impossible}");
            }
            step ??= plan.Missing.Count > 0 ? Explore() : null;
            if (step is { } s) return s with { ForecastMinutes = plan.ForecastMinutes };
            mind.Camp = camp;
            return plan.Missing.Count > 0 ? Pursuit.Lacking($"{string.Join(", ", plan.Missing)}, with nowhere to look") : NoStep();
        }

        double HoursWanted(Priority priority) => TonightHours() + (priority == Priority.Want ? SpareFireHours : 0);

        /// The all of a fire: what the fire forecast names missing (tinder for StrikesPerFire strikes), and a refused strike's lack.
        Tree FirePlan(TileId site, double hoursWanted)
        {
            // A site it cannot walk to has no fire for it: a lame body makes its fire where it lies.
            if (!ToSite(site).Reaches(Here)) return Impossible("a way to the site");
            var (forecast, lit) = SiteForecast(site, hoursWanted);
            // A fire burning while it fetches by night: an hour out must bring more than an hour of fire.
            double shortHours = Math.Max(hoursWanted - RememberedLeftAt(site) - forecast.Hours, 1.0 / 60);
            bool burnsMeanwhile = lit && !Weather.IsDaytime(world.Minute);
            var reqs = new List<Req>();
            foreach (var need in forecast.Missing)
                switch (need.Kind)
                {
                    case FireNeedKind.Starter or FireNeedKind.Tinder when lit: break;
                    case FireNeedKind.Starter: return Impossible("a fire starter");
                    case FireNeedKind.Tinder:
                        double odds = Math.Max(LeastStrikePct, Starter is ToolType tool ? Fire.StartOdds(world, agent, tool, Fire.TinderToStrike(pack, need.Kg)).TotalPct : 0);
                        reqs.Add(NeedReq(need, need.Kg * Math.Clamp(StrikesPerFire / odds, MinStrikes, MaxStrikes), 0));
                        break;
                    default:
                        reqs.Add(NeedReq(need, need.Kg, burnsMeanwhile ? need.Kg / shortHours : 0));
                        break;
                }
            if (!lit && forecast.Strike != null && Here == site && StrikeHere(forecast) is { } strike && Sim.Refusal(world, strike) is { } refused)
            {
                if (refused.Missing is not MissingResource m) return Impossible("a strike it may make");
                if (!(PileHere is { } pile && Items.Have(pile, forecast.Strike.Tinder) > DustKg)) reqs.Add(Of(m));
            }
            if (!lit && forecast.Strike == null && forecast.Missing.Count == 0) return Impossible("room to strike a fire");
            return Build(site, reqs);
        }

        StartFire? StrikeHere(FireForecast forecast) =>
            Starter is ToolType tool && forecast.Strike is { } s ? Fire.StrikeFromPack(Id, tool, pack, s) : null;

        /// The fire's site is a one of: the camp, here, or one of the nearest places it remembers trees, by the walk
        /// there plus the tree there. Committing to the plan there moves the camp.
        TileId ChooseSite(Priority priority)
        {
            var here = FromHere();
            var candidates = here.Minutes.Keys.Where(t => t != Camp && memory.Places.GetValueOrDefault(t)?.Trees == true)
                .OrderBy(t => here.Minutes[t]).ThenBy(t => t).Take(SiteCandidates).Prepend(Here).Where(t => t != Camp).Distinct().ToList();
            if (candidates.Count == 0) return Camp;
            // A site whose plan lacks something has no cost to weigh: one that lacks nothing beats it outright.
            double? Cost(TileId site)
            {
                var plan = FirePlan(site, HoursWanted(priority));
                return plan.Missing.Count > 0 ? null : here.MinutesTo(site) + plan.ForecastMinutes;
            }
            double? campCost = Cost(Camp);
            var best = candidates.Select(t => (Tile: t, Cost: Cost(t))).Where(x => x.Cost != null).OrderBy(x => x.Cost).ThenBy(x => x.Tile).FirstOrDefault();
            if (best.Cost is not double bestCost) return Camp;
            return campCost is not double stay || bestCost < SiteMoveFactor * stay - SiteMoveMinutes ? best.Tile : Camp;
        }

        /// A fire is wanted now when the body is cold, or at night: lit, fed, and the core warmed beside it.
        Next? AtFire(Tree plan, Priority priority)
        {
            if (!AtCamp) return null;
            bool wanted = Cold || !Weather.IsDaytime(world.Minute);
            if (wanted && Light(HoursWanted(priority)) is { } light) return light;
            if (wanted && Tend(HoursWanted(priority)) is { } tend) return tend;
            if (BurningHere && Cold) return new(new Rest(Id), StintMinutes, StintMinutes);
            return plan.Leaves.FirstOrDefault(l => l.Tile == Here && l.Via == Via.PickUp) is { } makings ? Do(makings) : null;
        }

        /// No fire lit at camp: struck when the forecast lacks nothing, or, cold, gives any hours; tinder on the pile taken first.
        Next? Light(double hoursWanted)
        {
            if (!AtCamp || LitHere) return null;
            var forecast = ForecastAt(Here, hoursWanted);
            if (!(forecast.Missing.Count == 0 || Cold && forecast.Hours > 0) || StrikeHere(forecast) is not { } strike) return null;
            var refused = Sim.Refusal(world, strike);
            if (refused == null) return new(strike, 1, 1);
            var tinder = forecast.Strike!.Tinder;
            return refused.Missing is MissingResource && PileHere is { } pile && Items.Have(pile, tinder) > DustKg ? Take(tinder, Items.Have(pile, tinder), []) : null;
        }

        /// A lit fire here short of the hours wanted: the fire forecast's Next goes on, from the pack or picked up off the pile.
        Next? Tend(double hoursWanted)
        {
            if (!LitHere || FireHereHours >= hoursWanted || ForecastAt(Here, 0).Next is not { } next) return null;
            if (Items.Have(pack, next.Fuel) > DustKg)
            {
                var add = new AddFuel(Id, next.Fuel, Math.Min(next.Kg, Items.Have(pack, next.Fuel)));
                if (Sim.Refusal(world, add) == null) return new(add, 1, 1);
            }
            if (PileHere is not { } pile || Items.Have(pile, next.Fuel) <= DustKg) return null;
            return Take(next.Fuel, Math.Min(Items.Have(pile, next.Fuel), Math.Max(next.Kg, Math.Min(RoomKg, PickUpFuelKg))), []);
        }

        double RememberedFireMinutes(TileId site) => memory.Places.GetValueOrDefault(site) is { FireHours: double h } seen
            ? h * 60 - (world.Minute - (seen.VisitedMinute ?? seen.SeenMinute)) : 0;

        bool OutlastsTheWalk(TileId site) => RememberedFireMinutes(site) > ToSite(site).MinutesTo(Here, double.PositiveInfinity);

        double RememberedLeftAt(TileId site) => site != Here && OutlastsTheWalk(site) ? RememberedFireMinutes(site) / 60 : 0;

        Next? LightOrTend(double hoursWanted) => Light(hoursWanted) ?? Tend(hoursWanted);

        Resource? HeaviestToPutDown(ISet<Resource> keep) => Items.KgByResource(pack).Where(x => x.Value > DustKg && !keep.Contains(x.Key) && !KeptInHand(x.Key))
            .OrderByDescending(x => x.Value).ThenBy(x => x.Key).Select(x => (Resource?)x.Key).FirstOrDefault();

        static bool KeptInHand(Resource r) => Fire.IsStrikeMaking(r) || r == Resource.Water || FoodResources.Contains(r);

        /// Short of room for the trip: what it is not carrying home goes down, heaviest first; fuel on the pile still counts.
        Next? PutDownFor(Tree plan)
        {
            double wantedKg = plan.Leaves.Where(l => l.Tile != Here).Sum(l => l.Kg);
            if (!AtCamp || wantedKg + FullPackKg < RoomKg) return null;
            var kept = plan.Leaves.Where(l => !l.For.Counted).SelectMany(l => l.For.AnyOf).ToHashSet();
            var put = HeaviestToPutDown(kept);
            return put is Resource r ? new(new PutDown(Id, r, Items.Have(pack, r)), 1, 1) : null;
        }

        // ---- water: drink to the satisfied line, or carry a day's water ----

        Pursuit ForWater(Priority priority)
        {
            if (InHand(Stat.Water) is { } quick) return quick;
            // One of: snow melted on a fire at camp, or, to drink now, a walk to water it remembers (the planner's choice).
            var here = FromHere();
            var fire = FirePlan(Camp, MeltFireHours);
            double meltMinutes = fire.Missing.Count > 0 ? double.PositiveInfinity : here.MinutesTo(Camp, double.PositiveInfinity) + fire.ForecastMinutes + MeltMinutes();
            var water = priority == Priority.Want ? null : WaterSource(priority);
            // Both trips end at camp, so each step toward one makes the other dearer: no turning back and forth.
            double WaterTrip(TileId t) => here.Minutes[t] + ToSite(Camp).MinutesTo(t, here.Minutes[t]);
            if (water is TileId w && WaterTrip(w) < meltMinutes) return WalkTo(w) is { } go ? go with { ForecastMinutes = WaterTrip(w), At = w } : null;
            if (meltMinutes < double.PositiveInfinity) return (Light(MeltFireHours) ?? NextLeaf(fire)) is { } step ? step with { ForecastMinutes = meltMinutes } : NoStep();
            return priority < Priority.Need ? default : Explore() is { } look ? look : Pursuit.Lacking("water, with nowhere to look");
        }

        /// Water as remembered (Work.WaterAt), not snow, which a person melts at camp: the kept source, else the planner's choice.
        TileId? WaterSource(Priority priority)
        {
            var here = FromHere();
            Source? At(TileId t) => here.Reaches(t) && Work.WaterAt(View, t, Body) is { KcalPerUnit: 0 } s ? s : null;
            bool Passes(TileId t) => Survives(t, Work.DrinkMinutes, new Drink(Id), Camp);
            ToSite(Camp);
            if (Kept(Stat.Water) is TileId kept && At(kept) != null && Passes(kept)) return kept;
            var ask = Ask(Stat.Water, priority, (WaterAgainPct - Body.HydrationPct) * Survival.Body.MaxHydrationMlOf(Body));
            var plan = Planner.Choose(world, Id, View, ask, At, accept: Passes);
            return plan.At is TileId t && t != Here ? t : null;
        }

        TileId? Kept(Stat stat) => mind.Plan is { At: TileId at } p && p.Stat == stat && at != Here ? at : null;

        /// Minutes alone (no kcal earning rate), no herd, no noise (its memory is its own). Build FromHere and ToSite(Camp) first:
        /// a route search inside the planner's callbacks would take a fresh scratch.
        static Asking Ask(Stat stat, Priority priority, double amount) =>
            new(stat, priority, Math.Max(1, amount), double.PositiveInfinity, Cohesion: 0, KinAt: _ => true, Noise: 0);

        // ---- rest: sleep at camp, by its fire where there is one ----

        Pursuit ForRest(Priority priority)
        {
            if (priority == Priority.Want && Weather.IsDaytime(world.Minute)) return default;
            if (!AtCamp)
            {
                double walk = ToSite(Camp).MinutesTo(Here, double.PositiveInfinity);
                bool canWalk = priority < Priority.Critical && Body.EnergyMinutes > walk + WalkReserveEnergyMinutes;
                FromHere();
                var rest = new Source(Survival.Body.EnergyDeltaPerMinute(sleeping: true, Survival.Body.PersonSleepHoursPerDay));
                var ask = Ask(Stat.Rest, priority, (RestedPct - Body.EnergyPct) * Survival.Body.MaxEnergyMinutes);
                var plan = Planner.Choose(world, Id, View, ask, t => t == Camp || t == Here && !canWalk ? rest : null, only: Camp);
                if (plan.At == Camp && WalkTo(Camp) is { } home) return home with { ForecastMinutes = walk, At = Camp };
                return SleepHere() is { } there ? there : Pursuit.Cannot("sleep here, too cold with no fire");
            }
            if (LightOrTend(HoursToSunrise()) is { } fire) return fire;
            return SleepHere() is { } sleep ? sleep : Pursuit.Cannot("sleep, too cold with no fire");
        }

        /// As long as the fire allows, or with no fire, as the cold allows.
        Next? SleepHere()
        {
            if (BurningHere)
            {
                int minutes = Math.Clamp((int)(FireHereHours * 60) - WakeBeforeOutMinutes, MinSleepMinutes, MaxSleepMinutes);
                return new(new Sleep(Id), minutes, minutes);
            }
            for (int minutes = MaxSleepMinutes; minutes >= MinSleepMinutes; minutes /= 2)
                if (CoreAfterF([(new Sleep(Id), Here, minutes)]) > HypothermiaCoreF + ColdSleepBufferF) return new(new Sleep(Id), minutes, minutes);
            return null;
        }

        // ---- food: eat what is in hand, or go and get it ----

        Pursuit ForFood(Priority priority)
        {
            if (EatToHand() is { } eat) return eat;
            if (FoodSource(FoodReq(MealKcal()), priority) is not var (tile, leaf))
                return priority < Priority.Need ? default : Explore() is { } look ? look : Pursuit.Lacking("food, with nowhere to look");
            var step = tile == Here ? Do(leaf) : WalkTo(tile);
            double trip = FromHere().Minutes[tile] + (ToSite(Camp).Reaches(tile) ? ToSite(Camp).Minutes[tile] : 0) + leaf.WorkMinutes + CookMinutes(leaf);
            return step is { } s ? s with { ForecastMinutes = trip, At = tile } : NoStep();
        }

        /// Each tile's quickest leaf (Offers) pays its units over its work: the kept source, else the planner's choice.
        (TileId Tile, Leaf Leaf)? FoodSource(Req req, Priority priority)
        {
            var here = FromHere();
            ToSite(Camp);
            var leaves = new Dictionary<TileId, Leaf?>();
            Leaf? LeafAt(TileId t)
            {
                if (leaves.TryGetValue(t, out var known)) return known;
                Leaf? found = null;
                if (here.Reaches(t))
                    foreach (var leaf in Offers(t, req, req.Amount, here.Minutes[t], site: null))
                        if (found == null || Rate(leaf) > Rate(found)) found = leaf;
                return leaves[t] = found;
            }
            static double Rate(Leaf l) => l.Units / Math.Max(1, l.WorkMinutes);
            bool Passes(TileId t) => Survives(t, LeafAt(t)!.WorkMinutes, ActivityFor(LeafAt(t)!), Camp);
            if (Kept(Stat.Food) is TileId kept && LeafAt(kept) is { } keptLeaf && Passes(kept)) return (kept, keptLeaf);
            var plan = Planner.Choose(world, Id, View, Ask(Stat.Food, priority, req.Amount), t => LeafAt(t) is { } l ? new Source(Rate(l)) : null,
                accept: Passes);
            return plan.At is TileId at ? (at, LeafAt(at)!) : null;
        }

        double MealKcal() => Math.Max(Verbs.MealKcal, Survival.Body.CaloriesCapacityKcal(Body) - Body.CaloriesKcal
            + Math.Clamp((mind.TargetFatKg - Body.FatKg) * Survival.Body.KcalPerKgFat, 0, Survival.Body.MaxCaloriesKcal));

        /// Raw food goes on a fire here that will finish it; while it cooks it eats what else it has, then waits. Raw is eaten only with nothing cooking.
        Next? EatToHand()
        {
            if (FireIdHere is FireId id && StationHere is { } st)
            {
                if (Items.KgByResource(st.Ready).Any(x => FoodResources.Contains(x.Key)) && IfAllowed(new TakeFinished(Id)) is { } take) return take;
                if (BurningHere)
                    foreach (var r in CookRecipes.Where(r => Items.Count(pack, Stations.InputOf(r)) > 0 && Stations.FinishMinute(world, id, r, 0) != null))
                    {
                        var put = new PutOn(Id, r.Id);
                        var refused = Sim.Refusal(world, put);
                        if (refused == null) return AtTheFire(put);
                        if (IsFull(refused) && PrioritiesNow[Stat.Water] < Priority.Need && MeltSet(st) && IfAllowed(new StopWork(Id, MeltRecipe.Id)) is { } stop) return stop;
                    }
            }
            if (ReadyFood(pack, cookable: false) is Resource food) return Eat(food);
            if (PileHere is { } s && ReadyFood(s, cookable: BurningHere) is Resource piled) return Take(piled, Items.Have(s, piled), []);
            if (FireIdHere is FireId pit && StationHere is { } here && here.Working.Where(t => CookRecipes.Any(r => r.Id == t.Recipe)).ToList() is { Count: > 0 } cooking)
            {
                if (WaitFor(EarliestFinish(pit, cooking)) is { } wait) return wait;
                double cookHours = CookRecipes.First(r => r.Id == cooking[0].Recipe).Minutes / 60.0;
                if ((Tend(cookHours) ?? (AtCamp ? NextLeaf(FirePlan(Camp, cookHours), toCamp: false) : null)) is { } fire) return fire;
                if (IfAllowed(new TakeBack(Id, cooking[0].Input, cooking[0].InputKg)) is { } back) return back;
            }
            return ReadyFood(pack, cookable: true) is Resource rawFood ? Eat(rawFood) : null;
        }

        Next Eat(Resource food) =>
            new(new Eat(Id, food, Math.Min(Items.Have(pack, food), MealKcal() / Items.Resources[food].KcalPerKg)), Work.EatMinutes, Work.EatMinutes);

        Resource? ReadyFood(Inventory inv, bool cookable) => Items.KgByResource(inv).Where(x => x.Value > DustKg && FoodResources.Contains(x.Key) && (cookable || !Cookable(x.Key)))
            .Select(x => x.Key).OrderByDescending(r => Items.Resources[r].KcalPerKg).ThenBy(r => r).Cast<Resource?>().FirstOrDefault();

        // ---- the fire's work ----

        FireId? FireIdHere => Fire.PitAt(world, Here);
        StationState? StationHere => FireIdHere is FireId id ? Stations.At(world, id) : null;
        static bool MeltSet(StationState st) => st.Ongoing.Any(o => o.Recipe == MeltRecipe.Id);
        static bool IsFull(Refusal refused) => refused.Missing is MissingState { Lack: Lack.StationRoom };
        long? EarliestFinish(FireId id, IEnumerable<Transformation> pieces) => pieces.Select(t => Stations.Forecast(world, id, t)).Min();

        Next AtTheFire(Activity act) => new(act, Stations.HandlingMinutes, Stations.HandlingMinutes);
        Next? IfAllowed(Activity act) => Sim.Refusal(world, act) == null ? AtTheFire(act) : null;
        Next? TakeWater(StationState st) => Items.Have(st.Ready, Resource.Water) > DustKg ? IfAllowed(new TakeFinished(Id)) : null;

        Next? WaitFor(long? finish)
        {
            if (finish is not long f) return null;
            int minutes = (int)Math.Clamp(f - world.Minute, 1, StintMinutes);
            return new(new Wait(Id), minutes, minutes);
        }

        /// Water waiting is taken, a melt under way waited for (or its fire fed), a fire with room set melting, a full one emptied or waited on.
        Next? MeltHere()
        {
            if (FireIdHere is not FireId id || StationHere is not { } st) return null;
            if (TakeWater(st) is { } take) return take;
            var melt = MeltRecipe;
            if (st.Ongoing.FirstOrDefault(o => o.Recipe == melt.Id) is { } under)
                return WaitFor(Stations.Forecast(world, id, under)) ?? Tend(MeltFireHours);
            if (!BurningHere) return Tend(MeltFireHours);
            if (Stations.FinishMinute(world, id, melt, 0) == null) return Tend(double.PositiveInfinity);
            var put = new PutOn(Id, melt.Id);
            var refused = Sim.Refusal(world, put);
            if (refused == null) return AtTheFire(put);
            return IsFull(refused) ? IfAllowed(new TakeFinished(Id)) ?? WaitFor(EarliestFinish(id, st.Working)) : null;
        }

        /// Minutes to the next litre: what waits or is under way at the fire here, else as a fresh melt would (StationMinutes).
        double MeltMinutes()
        {
            if (AtCamp && FireIdHere is FireId id && StationHere is { } st)
            {
                if (Items.Have(st.Ready, Resource.Water) > DustKg) return 0;
                if (st.Ongoing.FirstOrDefault(o => o.Recipe == MeltRecipe.Id) is { } under && (BurningHere || PitHere!.HasEmbers) && Stations.Forecast(world, id, under) is long f)
                    return f - world.Minute;
            }
            return StationMinutes(MeltRecipe);
        }

        /// The station's forecast at the camp fire here; elsewhere, or a fire that dies first, the recipe's time at full rate.
        double StationMinutes(Recipe r) =>
            AtCamp && FireIdHere is FireId id && (BurningHere || PitHere!.HasEmbers) && Stations.FinishMinute(world, id, r, 0) is long f ? f - world.Minute : r.Minutes;

        double CookMinutes(Leaf leaf)
        {
            Resource? raw = leaf.Via switch { Via.PickUp => (Resource)leaf.Index, Via.Butcher => Resource.RawMeat, _ => null };
            return CookRecipes.FirstOrDefault(c => Stations.InputOf(c) == raw) is { } r ? StationMinutes(r) : 0;
        }

        /// Water no longer wanted: what the melt made is taken, then it is stopped, so it holds no place on the fire.
        Next? StopMelting()
        {
            if (PrioritiesNow[Stat.Water] > Priority.None || StationHere is not { } st || !MeltSet(st)) return null;
            return TakeWater(st) ?? IfAllowed(new StopWork(Id, MeltRecipe.Id));
        }

        // ---- building the tree ----

        static Tree Impossible(string what)
        {
            var plan = new Tree { Impossible = what };
            plan.Missing.Add(what);
            return plan;
        }

        /// Each requirement from the pack, then the site's pile, then the quickest sources, as much as one trip carries.
        Tree Build(TileId site, IReadOnlyList<Req> reqs)
        {
            var plan = new Tree();
            var packLeft = new Dictionary<Resource, double>(Items.KgByResource(pack));
            var pileLeft = new Dictionary<Resource, double>((Here == site ? (PileHere is { } p0 ? Items.KgByResource(p0) : null) : memory.Places.GetValueOrDefault(site)?.Pile) ?? new SortedDictionary<Resource, double>());
            // Logs carried go down at camp before a trip: their room counts, wherever the plan is made from.
            double freeKg = RoomKg + Items.Totals(pack).Where(x => Fire.IsFuel(x.Resource) && !KeptInHand(x.Resource)).Sum(x => x.Kg);
            double tripMinutes = 0, awayWanted = 0, awayPlanned = 0;
            foreach (var req in reqs)
            {
                double left = req.Amount;
                if (!req.Counted)
                    foreach (var (source, piled) in new[] { (packLeft, false), (pileLeft, true) })
                        foreach (var r in req.AnyOf.OrderByDescending(req.PerKg).ThenBy(r => r))
                        {
                            double kg = Math.Min(source.GetValueOrDefault(r), left / req.PerKg(r));
                            if (left <= 1e-6 || kg <= 0) continue;
                            source[r] -= kg;
                            left -= kg * req.PerKg(r);
                            if (piled) plan.Leaves.Add(new Leaf(Via.PickUp, site, (int)r, req, kg * req.PerKg(r), kg, 1));
                        }
                awayWanted += Math.Max(0, left);
                while (left > 1e-6)
                {
                    double roomKg = freeKg - plan.Leaves.Where(l => l.Tile != site).Sum(l => l.Kg);
                    if (roomKg <= FullPackKg)
                    {
                        plan.Missing.Add($"room in the pack for {req.Name}");
                        break;
                    }
                    if (Quickest(req, left, site, plan) is not { } found)
                    {
                        plan.Missing.Add(req.Name);
                        break;
                    }
                    var (leaf, travel) = found;
                    double frac = Math.Min(1, roomKg / Math.Max(leaf.Kg, 1e-9));
                    var taken = leaf with { Units = leaf.Units * frac, Kg = leaf.Kg * frac };
                    int same = plan.Leaves.FindIndex(l => l.Tile == taken.Tile && l.Via == taken.Via && l.Index == taken.Index);
                    if (same >= 0) plan.Leaves[same] = plan.Leaves[same] with { Units = plan.Leaves[same].Units + taken.Units, Kg = plan.Leaves[same].Kg + taken.Kg };
                    else
                    {
                        plan.Leaves.Add(taken);
                        tripMinutes += travel;
                    }
                    left -= taken.Units;
                    awayPlanned += taken.Units;
                    if (frac < 1) break;
                }
            }
            // This trip, as many times over as it takes to bring what is wanted: later trips look alike.
            double trips = awayPlanned > 1e-6 ? Math.Max(1, awayWanted / awayPlanned) : 1;
            plan.ForecastMinutes = (plan.Leaves.Sum(l => l.WorkMinutes) + tripMinutes) * trips;
            return plan;
        }

        /// The one of: fewest minutes a unit, walks and work counted, among sources the survival forecast passes; the plan's At kept.
        (Leaf Leaf, double Travel)? Quickest(Req req, double want, TileId site, Tree plan)
        {
            var here = FromHere();
            var back = ToSite(site);
            if (!back.Reaches(Here)) return null;
            double legHome = back.Minutes[Here], best = double.PositiveInfinity;
            var offers = new List<(Leaf Leaf, double Travel, double PerUnit)>();
            TileId? held = mind.Plan?.At is TileId at && here.Reaches(at) ? at : null;
            foreach (var (t, outMinutes) in here.Minutes.OrderBy(x => x.Value).ThenBy(x => x.Key))
            {
                if ((outMinutes - legHome) / want >= best && (held is not TileId h || here.Minutes[h] < outMinutes)) break;
                double travel = t == site || plan.Leaves.Any(l => l.Tile == t) ? 0 : outMinutes + back.MinutesTo(t, outMinutes) - legHome;
                foreach (var leaf in Offers(t, req, want, (int)outMinutes, site))
                {
                    double net = leaf.Units - req.UsedPerHour * (travel + leaf.WorkMinutes) / 60;
                    if (net <= 1e-6) continue;
                    double perUnit = (travel + leaf.WorkMinutes) / net;
                    offers.Add((leaf, travel, perUnit));
                    best = Math.Min(best, perUnit);
                }
            }
            foreach (var o in offers.OrderBy(o => o.Leaf.Tile == held && o.PerUnit <= KeepSourceFactor * best ? 0 : 1).ThenBy(o => o.PerUnit).ThenBy(o => o.Leaf.Tile))
                if (Survives(o.Leaf.Tile, o.Leaf.WorkMinutes, ActivityFor(o.Leaf), site)) return (o.Leaf, o.Travel);
            return null;
        }

        // What the body refuses here it refuses on any tile: no leaf it would walk to and be refused.
        bool? forages, gathers, butchers;
        bool Able(Activity a, PoolDef? pool) => Crippled.Refusal(world, world.Agent(Id), Work.Needs(a, pool, Species.Human)) == null;
        bool Gathers(PoolDef pool) => pool is ForagePool ? forages ??= Able(new Gather(Id, 0), pool) : gathers ??= Able(new Gather(Id, 0), pool);
        bool Butchers => butchers ??= Able(new Butcher(Id, 0), null);

        IEnumerable<Leaf> Offers(TileId t, Req req, double want, int startIn, TileId? site)
        {
            var seen = memory.Places.GetValueOrDefault(t);
            double unsure = t == Here ? 0 : Memories.PilePrecisionKg / 2;
            var pile = t == Here ? (PileHere is { } p1 ? Items.KgByResource(p1) : null) : seen?.Pile;
            if (t != site && pile != null)
                foreach (var r in req.AnyOf.Where(r => pile.GetValueOrDefault(r) - unsure > DustKg))
                {
                    double kg = Math.Min(pile[r] - unsure, want / req.PerKg(r));
                    yield return new Leaf(Via.PickUp, t, (int)r, req, kg * req.PerKg(r), kg, 1);
                }
            var pools = Pools.At(world, t);
            for (int i = 0; i < pools.Count; i++)
            {
                if (!Gathers(pools[i])) continue;
                var id = new PoolId(t, i);
                if (!Pools.Knows(world, Id, id)) continue;
                int minutes = pools[i] switch
                {
                    ForagePool => MaxStintMinutes,
                    TreePool tree when (t == Here ? Pools.HasTrees(world, id) : seen?.Trees == true) => (int)Math.Ceiling(tree.MinutesToFell * (1 - Pools.ChopProgressPct(world, id)) / Items.ChopFactor(Items.Axe(pack))
                        * Work.BodyTimeFactor(world, agent, t, new Gather(Id, i))) + 1,
                    StockPool s when (t != Here || Pools.HasStock(world, id, pack)) && s.Items.Any(x => req.AnyOf.Contains(x.Resource) && Pools.CanWork(x, pack)) => MaxStintMinutes,
                    _ => 0,
                };
                if (minutes <= 0) continue;
                var kg = Expected(id, req.Focus, minutes, startIn, seen);
                double units = req.AnyOf.Sum(r => kg.GetValueOrDefault(r) * req.PerKg(r)), all = kg.Values.Sum();
                if (units <= 1e-6) continue;
                // A tree comes down whole: what it brings past the want is still brought.
                if (pools[i] is not TreePool && units > want)
                {
                    double part = want / units;
                    (minutes, units, all) = ((int)Math.Max(MinStintMinutes, Math.Ceiling(minutes * part)), want, all * part);
                }
                yield return new Leaf(Via.Gather, t, i, req, units, all, minutes);
            }
            if (req.AnyOf.Contains(Resource.RawMeat) && Butchers)
                foreach (var seenCarcass in seen?.Carcasses ?? [])
                    if (Carcasses.Find(world, t, seenCarcass.Id) is { } c && !Carcasses.IsButchered(c) && Carcasses.MeatKg(c) > 0)
                        yield return new Leaf(Via.Butcher, t, c.Id, req, Math.Min(want, Carcasses.MeatKg(c) * req.PerKg(Resource.RawMeat)),
                            Math.Min(Carcasses.MeatKg(c), want / req.PerKg(Resource.RawMeat)),
                            (int)Math.Ceiling(Carcasses.RemainingMinutes(c, ButcherMode.QuickStrip, Items.HasCuttingTool(pack))
                                * Work.BodyTimeFactor(world, agent, t, new Butcher(Id, c.Id))));
        }

        IReadOnlyDictionary<Resource, double> Expected(PoolId id, ForageFocus focus, int minutes, int startIn, TileMemory? seen)
        {
            if (expected.TryGetValue((id, focus, minutes), out var kept)) return kept;
            double? density = Pools.Def(world, id) is ForagePool def ? seen?.ForageDensity ?? def.Density : null;
            return expected[(id, focus, minutes)] = id.Tile == Here
                ? Work.ExpectedGather(world, agent, new Gather(Id, id.Index, focus), minutes)
                : Work.ExpectedGatherAt(world, agent, id, focus, minutes, startIn, density);
        }

        Activity ActivityFor(Leaf leaf) => leaf.Via switch
        {
            Via.Gather => new Gather(Id, leaf.Index, leaf.For.Focus),
            Via.Butcher => new Butcher(Id, leaf.Index, ButcherMode.QuickStrip),
            _ => new PickUp(Id, (Resource)leaf.Index, leaf.Kg),
        };

        /// The survival filter: the trip ends above the Critical line, or no colder than staying here as long would.
        bool Survives(TileId t, int workMinutes, Activity work, TileId site)
        {
            var legs = WalkLegs(Here, t).Append((work is PickUp ? new Wait(Id) : work, t, workMinutes)).Concat(WalkLegs(t, site)).ToList();
            double tripF = CoreAfterF(legs);
            return tripF > HypothermiaCoreF || tripF >= CoreAfterF([(new Rest(Id), Here, legs.Sum(l => l.Item3))]);
        }

        // ---- doing the plan ----

        /// The nearest leaf away; with none left, or the pack full, back to camp.
        Next? NextLeaf(Tree plan, bool toCamp = true)
        {
            var away = plan.Leaves.Where(l => l.Tile != Camp).ToList();
            if (away.Count > 0 && PutDownFor(plan) is { } putDown) return putDown;
            if (away.Count > 0 && RoomKg <= FullPackKg && !AtCamp) return WalkTo(Camp);
            if (away.Count > 0)
            {
                var here = FromHere();
                var next = away.OrderBy(l => here.MinutesTo(l.Tile, double.PositiveInfinity)).ThenBy(l => l.Tile).First();
                return (next.Tile == Here ? Do(next) : WalkTo(next.Tile)) is { } step ? step with { At = next.Tile } : null;
            }
            if (plan.Leaves.FirstOrDefault(l => l.Tile == Here) is { } atHand) return Do(atHand);
            return toCamp && !AtCamp && (plan.Leaves.Count > 0 || plan.Missing.Count == 0) ? WalkTo(Camp) : null;
        }

        /// A full pack makes room first: the heaviest thing that is not makings, water, food or what the same requirement wants goes down.
        Next? Take(Resource r, double kg, IEnumerable<Resource> wanted)
        {
            var take = new PickUp(Id, r, kg);
            if (Sim.Refusal(world, take)?.Missing is MissingState { Lack: Lack.PackRoomKg }) return MakeRoom(wanted.Append(r).ToHashSet());
            return Sim.Refusal(world, take) == null ? new(take, 1, 1) : null;
        }

        /// The heaviest thing not kept in hand goes down; with none, a strike's makings beyond StrikeReserveKg.
        Next? MakeRoom(ISet<Resource> keep)
        {
            if (HeaviestToPutDown(keep) is Resource r) return new(new PutDown(Id, r, Items.Have(pack, r)), 1, 1);
            var (spare, kg) = Items.KgByResource(pack).Where(x => !keep.Contains(x.Key) && Fire.IsStrikeMaking(x.Key))
                .Select(x => (x.Key, Kg: Math.Max(0, x.Value - StrikeReserveKg)))
                .Select(x => (x.Key, Kg: x.Kg > 0 ? Items.WouldMoveKg(pack, x.Key, x.Kg) : 0))
                .OrderByDescending(x => x.Kg).ThenBy(x => x.Key).FirstOrDefault();
            if (kg > FullPackKg && new PutDown(Id, spare, kg) is var put && Sim.Refusal(world, put) == null) return new(put, 1, 1);
            packFull = true;
            return null;
        }

        bool packFull;

        Pursuit NoStep() => packFull ? Pursuit.Cannot("room in the pack, full of what it keeps") : default;

        Next? Do(Leaf leaf)
        {
            var act = ActivityFor(leaf);
            if (act is PickUp take) return PileHere is { } s && Items.Have(s, take.Resource) > DustKg ? Take(take.Resource, Math.Min(take.Kg, Items.Have(s, take.Resource)), leaf.For.AnyOf) : null;
            if (Sim.Refusal(world, act) is { } refused) return refused.Missing is MissingState { Lack: Lack.PackRoomKg } ? MakeRoom(leaf.For.AnyOf.ToHashSet()) : null;
            int minutes = act is Butcher ? Verbs.UntilDoneMinutes : Math.Clamp(leaf.WorkMinutes, 1, StintMinutes);
            return new(act, minutes, minutes);
        }

        /// What it does when nothing is wanted: keep the camp going through the night.
        Next Keep()
        {
            if (!AtCamp) return WalkTo(Camp) ?? new(new Rest(Id), StintMinutes, StintMinutes);
            if (StopMelting() is { } stop) return stop;
            if (!Weather.IsDaytime(world.Minute))
            {
                if (LightOrTend(HoursToSunrise()) is { } fire) return fire;
                if (SleepHere() is { } sleep) return sleep;
            }
            return new(new Rest(Id), StintMinutes, StintMinutes);
        }

        // ---- moving ----

        RouteMap FromHere() => fromHere ??= Routes(into: null);

        /// The ways in to a site. Standing on it, the ways out stand for them: a forecast, near enough either way.
        RouteMap ToSite(TileId site)
        {
            if (site == Here) return FromHere();
            if (!toSite.TryGetValue(site, out var routes)) toSite[site] = routes = Routes(into: site);
            return routes;
        }

        /// Over ground it has seen (Trip.Routes), as its MemoryView allows.
        RouteMap Routes(TileId? into) => Trip.Routes(world, Id, into: into, refused: n => !View.InRange(n) || View.Threat(n));

        Next? WalkTo(TileId target)
        {
            var routes = FromHere();
            return target != Here && routes.Reaches(target) ? StepTo(routes.FirstStep(target)!.Value) : null;
        }

        /// Pace priced by risk: the least minutes plus priced injury odds.
        Next? StepTo(Direction dir)
        {
            var go = new[] { Pace.Quick, Pace.Careful }.Where(p => p == Pace.Quick || Trip.PaceMatters(world, Id, dir))
                .Select(p => new Travel(Id, dir, p)).Where(t => Sim.Refusal(world, t) == null)
                .OrderBy(t => Trip.Time(world, Id, dir, t.Pace).TotalMinutes + InjuryPriceMinutes * Trip.Risk(world, Id, dir, t.Pace).TotalPct)
                .FirstOrDefault();
            return go != null ? new(go, Verbs.UntilDoneMinutes, Trip.Time(world, Id, dir, go.Pace).TotalMinutes) : null;
        }

        /// By day, to the nearest seen tile beside unseen ground that it would come back from.
        Next? Explore()
        {
            if (!Weather.IsDaytime(world.Minute)) return null;
            var routes = FromHere();
            var edge = routes.Minutes.Keys.Where(t => t != Here && MapGen.Neighbors(t).Any(n => map.InBounds(n) && !(memory.Explored?.Contains(n) ?? false) && Map.Blocked(map, t, n) == null))
                .OrderBy(t => routes.Minutes[t]).ThenBy(t => t).Where(t => Survives(t, 0, new Wait(Id), Camp)).Cast<TileId?>().FirstOrDefault();
            return edge is TileId e ? WalkTo(e) : null;
        }

        // ---- what it knows ----

        MemoryView? view;
        MemoryView View => view ??= new(this, world);

        /// Memory as the planner and its routes read it, near here or camp: no threat here or at camp; here, as it is.
        sealed class MemoryView(Mind m, World world) : WorldView
        {
            List<TileId>? threats;
            public override bool Threat(TileId tile) => tile != m.Here && tile != m.Camp
                && (threats ??= [.. m.Threats()]).Exists(p => MapGen.Manhattan(p, tile) < ThreatTiles);
            public override bool OpenWater(TileId tile) => tile == m.Here ? Work.CanDrinkHere(world, tile) : m.memory.Places.GetValueOrDefault(tile)?.Water == true;
            public override GroundSeen Ground(TileId tile)
            {
                if (tile == m.Here) return default;
                return m.memory.Explored?.At(tile) is { Surface: var s } ? new GroundSeen(s.SnowDepthM, s.Iced ? 0 : s.WaterDepthM) : default;
            }
            public override bool InRange(TileId tile) => MapGen.Manhattan(tile, m.Here) <= SearchTiles || MapGen.Manhattan(tile, m.Camp) <= SearchTiles;
        }

        // ---- danger ----

        IEnumerable<TileId> Threats() => memory.Sightings.Values
            .Where(s => world.Minute - s.Minute <= ThreatMemoryMinutes && Fauna.IsPredator(s.Species)).Select(s => s.Tile);

        TileId? Threat() => Threats().Where(t => MapGen.Manhattan(t, Here) <= ThreatTiles)
            .OrderBy(t => MapGen.Manhattan(t, Here)).ThenBy(t => t).Cast<TileId?>().FirstOrDefault();

        Next? Flee(TileId from) => Enum.GetValues<Direction>()
            .Select(d => (Dir: d, To: Map.Step(Here, d))).Where(x => map.InBounds(x.To) && Map.Blocked(map, Here, x.To) == null)
            .OrderByDescending(x => MapGen.Manhattan(x.To, from)).ThenBy(x => MapGen.Manhattan(x.To, Camp)).ThenBy(x => x.Dir)
            .Select(x => StepTo(x.Dir)).FirstOrDefault(s => s != null);
    }
}
