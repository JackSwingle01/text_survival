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
}

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

    /// An "all of" child: an amount of any of these resources, each kilo worth PerKg units of it. Counted: what is
    /// to hand is already in the amount, so only sources away fill it. UsedPerHour: what the time spent getting it
    /// uses up of it, so a source must bring more than its trip takes.
    sealed record Req(string Name, IReadOnlyList<Resource> AnyOf, Func<Resource, double> PerKg, double Amount, ForageFocus Focus,
        bool Counted = false, double UsedPerHour = 0);

    enum Via { PickUp, Gather, Butcher }

    sealed record Leaf(Via Via, TileId Tile, int Index, Req For, double Units, double Kg, int WorkMinutes);

    sealed class Tree
    {
        public static readonly Tree None = new();
        public List<Leaf> Leaves { get; } = [];
        public List<string> Missing { get; } = [];
        public double ForecastMinutes { get; set; }
    }

    static readonly Resource[] FuelResources = [.. Fire.Props.Keys.Where(r => !Fire.Tinders.Contains(r)).Order()];
    static readonly Resource[] FoodResources = [.. Items.Resources.Where(x => x.Value.Category == ResourceCategory.Food).Select(x => x.Key).Order()];

    /// The fire's recipes as its verbs offer them: those that cook food from a piece put on, and the one that melts snow.
    static IEnumerable<Recipe> CookRecipes => Stations.FireRecipes.Where(r => !Stations.FromNothing(r) && Items.Resources[r.Result.Resource!.Value].Category == ResourceCategory.Food);
    static Recipe MeltRecipe => Stations.FireRecipes.Single(r => Stations.FromNothing(r) && r.Result.Resource == Resource.Water);
    static bool Cookable(Resource r) => CookRecipes.Any(c => Stations.InputOf(c) == r);

    static Req FuelReq(double hours, double usedPerHour) => new("fuel", FuelResources, r => Fire.FuelHours(r, 1), hours, ForageFocus.Fuel, Counted: true, usedPerHour);
    static Req TinderReq(double kg) => new("tinder", Fire.Tinders, _ => 1, kg, ForageFocus.Fuel);
    static Req StickReq(double kg) => new("sticks", [Resource.Stick], _ => 1, kg, ForageFocus.Fuel);
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
        double? fireHereHours;
        double FireHereHours => fireHereHours ??= BurningHere ? Fire.HoursItWillBurn(PitHere!) : 0;
        bool Cold => CorePriority(Body.CoreTempF, mind.Plan?.Stat == Stat.Warmth) >= Priority.Need;

        RouteMap? fromHere;
        readonly Dictionary<TileId, RouteMap> toSite = [];
        readonly Dictionary<(PoolId, ForageFocus, int), IReadOnlyDictionary<Resource, double>> expected = [];
        Dictionary<Stat, Priority>? priorities;
        Dictionary<Stat, Priority> PrioritiesNow => priorities ??= Priorities();

        // ---- the decision ----

        public Next Decide()
        {
            if (Threat() is TileId threat && !BurningHere && Flee(threat) is { } flee) return flee;
            if (mind.Plan is { } worse && PrioritiesNow[worse.Stat] > worse.Priority) mind.Plan = worse with { Priority = PrioritiesNow[worse.Stat] };
            if (mind.Plan is { } held && Interrupt(held) != null) (mind.Plan, priorities) = (null, null);
            // Health has no rank on the ladder: bleeding badly by a fire with nothing Critical, it sleeps, which mends fastest.
            if (HealthPriority(Body) == Priority.Critical && !PrioritiesNow.Values.Contains(Priority.Critical) && AtCamp && BurningHere && SleepHere() is { } mend) return mend;
            // A Need met from what is in hand costs minutes, not a trip: it is met on the spot, before the plan goes on.
            foreach (var (stat, priority) in Ranked().Where(x => x.Priority >= Priority.Need))
                if (InHand(stat) is { } quick) return quick;
            if (mind.Plan is { } kept && Pursue(kept.Stat, kept.Priority) is { } step)
            {
                mind.Plan = kept with { At = step.At };
                return step;
            }
            mind.Plan = null;
            foreach (var (stat, priority) in Ranked())
                if (Pursue(stat, priority) is { } next)
                {
                    mind.Plan = new NpcPlan(stat, priority, world.Minute, (int)Math.Max(MinBudgetMinutes, BudgetFactor * next.ForecastMinutes), next.At);
                    return next;
                }
            return Keep();
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
            Stat.Warmth => AtCamp && Cold ? Light(Tree.None, now: true) ?? (BurningHere ? Stoke(HoursToSunrise()) : null) : null,
            Stat.Water => Sim.Refusal(world, new Drink(Id)) == null ? new(new Drink(Id), Work.DrinkMinutes, Work.DrinkMinutes)
                : PileHere is { } s && Items.Have(s, Resource.Water) > DustKg ? Take(Resource.Water, Items.Have(s, Resource.Water), [])
                : MeltHere(),
            Stat.Food => EatToHand(),
            _ => null,
        };

        Next? Pursue(Stat stat, Priority priority) => stat switch
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
                [Stat.Warmth] = WarmthPriority(CoreAtCampF(), seeing(Stat.Warmth), UsableHoursAt(Camp), TonightHours() - NightSlackHours),
                [Stat.Water] = WaterPriority(Body.HydrationPct, seeing(Stat.Water), waterToHandL, (1440 - Weather.MinuteOfDay(world.Minute)) / 60.0),
                [Stat.Rest] = RestPriority(Body.EnergyPct, seeing(Stat.Rest)),
                [Stat.Food] = FoodPriority(Body, mind.TargetFatKg, seeing(Stat.Food), foodToHand: false),
            };
        }

        double HoursToSunrise() => HoursUntil(Weather.SunriseHour(world.Minute));

        double TonightHours() => Weather.IsDaytime(world.Minute) ? HoursToSunrise() - HoursUntil(Weather.SunsetHour(world.Minute)) : HoursToSunrise();

        double HoursUntil(int hourOfDay) => (hourOfDay * 60 - Weather.MinuteOfDay(world.Minute) + 1440) % 1440 / 60.0;

        /// From away, the fire as last seen beside it and the pile as it left it.
        double UsableHoursAt(TileId site)
        {
            if (Here == site) return FireHere().Hours;
            return FireLeftHoursAt(site) + Fire.Forecast(new FireAsk(null, PitType.Open, WithPack(memory.Places.GetValueOrDefault(site)?.Pile), Starter, 0)).Hours;
        }

        FireForecast? fireHere;
        FireForecast FireHere() => fireHere ??= Fire.Forecast(new FireAsk(PitHere, PitType.Open, WithPile(PileHere), Starter, 0));

        ToolType? Starter => Fire.Starters.Where(t => Items.Tool(pack, t) != null).Cast<ToolType?>().FirstOrDefault();

        Inventory WithPile(Inventory? pile)
        {
            var all = Items.CopyResources(pack);
            if (pile != null) Items.AddAll(all, Items.CopyResources(pile));
            return all;
        }

        /// The pack with a pile as remembered, its weights in the pieces they would be fetched in.
        Inventory WithPack(IReadOnlyDictionary<Resource, double>? pile)
        {
            var all = Items.CopyResources(pack);
            foreach (var (r, kg) in pile ?? new Dictionary<Resource, double>()) Items.AddKg(all, r, kg);
            return all;
        }

        double FireLeftHoursAt(TileId site)
        {
            if (Here == site) return FireHereHours;
            var seen = memory.Places.GetValueOrDefault(site);
            return seen is { FireHours: double h } ? Math.Max(0, h - (world.Minute - (seen.VisitedMinute ?? seen.SeenMinute)) / 60.0) : 0;
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

        Next? ForWarmth(Priority priority)
        {
            var camp = Camp;
            if (mind.Plan?.Stat != Stat.Warmth) mind.Camp = ChooseSite(priority);
            var plan = FirePlan(Camp, HoursWanted(priority));
            var step = AtFire(plan, priority) ?? NextLeaf(plan) ?? (plan.Missing.Count > 0 ? Explore() : null);
            if (step == null) mind.Camp = camp;
            return step is { } s ? s with { ForecastMinutes = plan.ForecastMinutes } : null;
        }

        double HoursWanted(Priority priority) => TonightHours() + (priority == Priority.Want ? SpareFireHours : 0);

        /// The all of a fire: lit if out by the time it is back (tinder for the odds, kindling, what the refusal names), then fuel.
        Tree FirePlan(TileId site, double hoursWanted)
        {
            double backMinutes = ToSite(site).MinutesTo(Here, 0);
            var reqs = new List<Req>();
            if (FireLeftHoursAt(site) * 60 < backMinutes + 1)
            {
                if (Strike() is not { } strike) return Impossible("a fire starter");
                double odds = Math.Max(LeastStrikePct, Fire.StartOdds(world, agent, strike.Tool, strike.Tinder).TotalPct);
                reqs.Add(TinderReq(Verbs.TinderKg * Math.Clamp(StrikesPerFire / odds, MinStrikes, MaxStrikes)));
                reqs.Add(StickReq(Verbs.KindlingKg));
                if (Here == site && Sim.Refusal(world, strike)?.Missing is MissingResource m && !m.AnyOf.Any(r => Fire.Tinders.Contains(r) || r == Resource.Stick))
                    reqs.Add(Of(m));
            }
            // A fire burning while it fetches by night: an hour out must bring more than an hour of fire.
            reqs.Add(FuelReq(Math.Max(0, hoursWanted - UsableHoursAt(site)), !Weather.IsDaytime(world.Minute) && FireLeftHoursAt(site) > 0 ? 1 : 0));
            return Build(site, reqs);
        }

        StartFire? Strike()
        {
            var tool = Fire.Starters.Where(t => Items.Tool(pack, t) != null).Cast<ToolType?>().FirstOrDefault();
            if (tool == null) return null;
            var tinder = Fire.TinderToStrike(pack, Verbs.TinderKg);
            return new StartFire(Id, tool.Value, tinder, Verbs.TinderKg, Math.Min(Verbs.KindlingKg, Items.Have(pack, Resource.Stick)));
        }

        /// A strike it can make whose fire, by the fire forecast, outlasts the strike's own flame.
        bool CanLight => Strike() is { } s && Sim.Refusal(world, s) == null && FireHere().Hours > Fire.ForecastLight(world, s).FlameHours;

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
            if (wanted && Light(plan, now: Cold) is { } light) return light;
            if (BurningHere && wanted && Stoke(HoursWanted(priority)) is { } stoke) return stoke;
            if (BurningHere && Cold) return new(new Rest(Id), StintMinutes, StintMinutes);
            return plan.Leaves.FirstOrDefault(l => l.Tile == Here && l.Via == Via.PickUp) is { } makings ? Do(makings) : null;
        }

        /// The all of's consuming step: struck once this trip's list is in hand or the pack is full, or now.
        Next? Light(Tree plan, bool now)
        {
            if (!AtCamp || BurningHere || !CanLight) return null;
            bool gathering = plan.Leaves.Any(l => l.Tile != Here) && RoomKg > FullPackKg;
            return now || !gathering ? new(Strike()!, 1, 1) : null;
        }

        /// The fuel with most hours a kilo the fire takes now: kindling while it is too cool for logs.
        Next? Stoke(double hoursWanted)
        {
            var pit = PitHere!;
            if (FireHereHours >= hoursWanted) return null;
            foreach (var r in FuelResources.Where(r => Items.Have(pack, r) > DustKg).OrderByDescending(r => Fire.FuelHours(r, 1)).ThenBy(r => r))
            {
                var add = new AddFuel(Id, r, Math.Min(Verbs.FuelKg, Items.Have(pack, r)));
                var refused = Sim.Refusal(world, add);
                if (refused == null) return new(add, 1, 1);
                if (refused.Missing is MissingState { Lack: Lack.FireRoomKg }) return null;
            }
            if (PileHere is not { } pile) return null;
            var take = FuelResources.Where(r => Items.Have(pile, r) > DustKg && Fire.WillCatch(pit, r, Verbs.FuelKg))
                .OrderByDescending(r => Fire.FuelHours(r, 1)).ThenBy(r => r).Cast<Resource?>().FirstOrDefault();
            if (take is not Resource fuel) return null;
            var pickUp = new PickUp(Id, fuel, Math.Min(Items.Have(pile, fuel), Math.Clamp(RoomKg, Verbs.FuelKg, PickUpFuelKg)));
            return Sim.Refusal(world, pickUp) == null ? new(pickUp, 1, 1) : null;
        }

        /// Short of room for the trip: what it is not carrying home goes down, heaviest first; fuel on the pile still counts.
        Next? PutDownFor(Tree plan)
        {
            double wantedKg = plan.Leaves.Where(l => l.Tile != Here).Sum(l => l.Kg);
            if (!AtCamp || wantedKg + FullPackKg < RoomKg) return null;
            var kept = plan.Leaves.Where(l => !l.For.Counted).SelectMany(l => l.For.AnyOf).Concat(Fire.Tinders).Append(Resource.Stick).Append(Resource.Water).Concat(FoodResources).ToHashSet();
            var put = Items.KgByResource(pack).Where(x => x.Value > DustKg && !kept.Contains(x.Key)).OrderByDescending(x => x.Value).ThenBy(x => x.Key).Select(x => (Resource?)x.Key).FirstOrDefault();
            return put is Resource r ? new(new PutDown(Id, r, Items.Have(pack, r)), 1, 1) : null;
        }

        // ---- water: drink to the satisfied line, or carry a day's water ----

        Next? ForWater(Priority priority)
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
            if (meltMinutes < double.PositiveInfinity) return (Light(fire, now: true) ?? NextLeaf(fire)) is { } step ? step with { ForecastMinutes = meltMinutes } : null;
            return priority >= Priority.Need ? Explore() : null;
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

        Next? ForRest(Priority priority)
        {
            if (priority == Priority.Want && Weather.IsDaytime(world.Minute)) return null;
            if (!AtCamp)
            {
                double walk = ToSite(Camp).MinutesTo(Here, double.PositiveInfinity);
                bool canWalk = priority < Priority.Critical && Body.EnergyMinutes > walk + WalkReserveEnergyMinutes;
                FromHere();
                var rest = new Source(Survival.Body.EnergyDeltaPerMinute(sleeping: true, Survival.Body.PersonSleepHoursPerDay));
                var ask = Ask(Stat.Rest, priority, (RestedPct - Body.EnergyPct) * Survival.Body.MaxEnergyMinutes);
                var plan = Planner.Choose(world, Id, View, ask, t => t == Camp || t == Here && !canWalk ? rest : null, only: Camp);
                if (plan.At == Camp && WalkTo(Camp) is { } home) return home with { ForecastMinutes = walk, At = Camp };
                return SleepHere();
            }
            if (!BurningHere && CanLight && UsableHoursAt(Here) > 0) return new(Strike()!, 1, 1);
            if (BurningHere && Stoke(HoursToSunrise()) is { } stoke) return stoke;
            return SleepHere();
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

        Next? ForFood(Priority priority)
        {
            if (EatToHand() is { } eat) return eat;
            if (FoodSource(FoodReq(MealKcal()), priority) is not var (tile, leaf)) return priority >= Priority.Need ? Explore() : null;
            var step = tile == Here ? Do(leaf) : WalkTo(tile);
            double trip = FromHere().MinutesTo(tile, 0) + ToSite(Camp).MinutesTo(tile, 0) + leaf.WorkMinutes + CookMinutes(leaf);
            return step is { } s ? s with { ForecastMinutes = trip, At = tile } : null;
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
                    foreach (var leaf in Offers(t, req, req.Amount, (int)here.MinutesTo(t, 0), site: null))
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
                if (BurningHere && Stoke(MeltFireHours) is { } stoke) return stoke;
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
                return WaitFor(Stations.Forecast(world, id, under)) ?? (BurningHere ? Stoke(MeltFireHours) : null);
            if (!BurningHere) return null;
            if (Stations.FinishMinute(world, id, melt, 0) == null) return Stoke(MeltFireHours);
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
            var plan = new Tree();
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
            double freeKg = RoomKg + Items.Totals(pack).Where(x => IsLog(x.Resource)).Sum(x => x.Kg);
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
                    if (roomKg <= FullPackKg) break;
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

        /// The one of: fewest minutes a unit, walks and work counted, among sources the survival forecast passes.
        (Leaf Leaf, double Travel)? Quickest(Req req, double want, TileId site, Tree plan)
        {
            var here = FromHere();
            var back = ToSite(site);
            double legHome = back.MinutesTo(Here, 0), best = double.PositiveInfinity;
            var offers = new List<(Leaf Leaf, double Travel, double PerUnit)>();
            foreach (var (t, outMinutes) in here.Minutes.OrderBy(x => x.Value).ThenBy(x => x.Key))
            {
                if ((outMinutes - legHome) / want >= best) break;
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
            foreach (var o in offers.OrderBy(o => o.PerUnit).ThenBy(o => o.Leaf.Tile))
                if (Survives(o.Leaf.Tile, o.Leaf.WorkMinutes, ActivityFor(o.Leaf), site)) return (o.Leaf, o.Travel);
            return null;
        }

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
            if (req.AnyOf.Contains(Resource.RawMeat))
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
                return next.Tile == Here ? Do(next) : WalkTo(next.Tile);
            }
            if (plan.Leaves.FirstOrDefault(l => l.Tile == Here) is { } atHand) return Do(atHand);
            return toCamp && !AtCamp && (plan.Leaves.Count > 0 || plan.Missing.Count == 0) ? WalkTo(Camp) : null;
        }

        static bool IsLog(Resource r) => FuelResources.Contains(r) && Fire.Props[r].MinFireTempF > 0;

        /// A full pack makes room first: the heaviest thing that is not makings, water, food or what the same requirement wants goes down.
        Next? Take(Resource r, double kg, IEnumerable<Resource> wanted)
        {
            var take = new PickUp(Id, r, kg);
            if (Sim.Refusal(world, take)?.Missing is MissingState { Lack: Lack.PackRoomKg })
            {
                var keep = Fire.Tinders.Append(Resource.Stick).Append(Resource.Water).Concat(FoodResources).Append(r).Concat(wanted).ToHashSet();
                var put = Items.KgByResource(pack).Where(x => x.Value > DustKg && !keep.Contains(x.Key)).OrderByDescending(x => x.Value).ThenBy(x => x.Key).Select(x => (Resource?)x.Key).FirstOrDefault();
                return put is Resource down ? new(new PutDown(Id, down, Items.Have(pack, down)), 1, 1) : null;
            }
            return Sim.Refusal(world, take) == null ? new(take, 1, 1) : null;
        }

        Next? Do(Leaf leaf)
        {
            var act = ActivityFor(leaf);
            if (act is PickUp take) return PileHere is { } s && Items.Have(s, take.Resource) > DustKg ? Take(take.Resource, Math.Min(take.Kg, Items.Have(s, take.Resource)), leaf.For.AnyOf) : null;
            if (Sim.Refusal(world, act) != null) return null;
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
                if (!BurningHere && CanLight && UsableHoursAt(Here) > 0) return new(Strike()!, 1, 1);
                if (BurningHere && Stoke(HoursToSunrise()) is { } stoke) return stoke;
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
