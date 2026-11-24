namespace Survival;

/// What an animal has sensed: a threat (Who), kept KeepSightingMinutes, or a carcass, kept until it sees it bare or gone.
public sealed record Sensed(AgentId? Who, TileId Tile, long Minute, int? Carcass = null);

/// Water, rest and food on the shared planner: the top stat at Need or above gets a plan.
public static partial class Fauna
{
    const string KinLeaving = "its kin are leaving";
    const int SenseEveryMinutes = 5, KeepSightingMinutes = 180, PlanAgainEveryMinutes = 120;
    /// Asleep away from its den, a denning animal sleeps lighter (D).
    const double OpenSleepFactor = 0.75;

    sealed class AnimalView(World world, HerdMind mind) : WorldView
    {
        public override bool Threat(TileId tile) => mind.Sightings?.Exists(s => s.Who != null && s.Tile == tile) == true;
        public override bool OpenWater(TileId tile) => Work.HasUnfrozenWater(world, tile);
        public override GroundSeen Ground(TileId tile)
        {
            var surface = Survival.Ground.Underfoot(world, tile);
            return new GroundSeen(surface.SnowDepthM, surface.DrinkableWaterM);
        }
    }

    /// A forecast: what an animal drinks a minute where it stands (Work.WaterAt, from what it knows).
    public static Source? WaterHere(World world, Agent animal) => Work.WaterAt(new AnimalView(world, MindOf(animal)), animal.Tile, animal.Body);

    static TileId? Den(Agent animal, HerdMind mind) => IsPredator(animal.Species) && mind.Territory.Count > 0 ? mind.Territory[0] : null;

    /// A forecast: the rest a minute's sleep here buys, in energy minutes (Body.EnergyDeltaPerMinute).
    public static double RestPerMinute(Agent animal, TileId tile) =>
        Body.EnergyDeltaPerMinute(sleeping: true, SpeciesTable.Of(animal.Species).SleepHoursPerDay) * SleepFactor(animal, tile);

    static double SleepFactor(Agent animal, TileId tile) => Den(animal, MindOf(animal)) is TileId den && den != tile ? OpenSleepFactor : 1;

    /// Outside its active hours it would rather rest: from below rested, until full.
    static (Stat Stat, Priority Priority)? Due(World world, Agent animal, HerdMind mind)
    {
        var body = animal.Body;
        var water = Priorities.WaterPriority(body.HydrationPct, mind.Plan?.Stat == Stat.Water, double.PositiveInfinity, 0);
        bool resting = mind.Plan?.Stat == Stat.Rest;
        var rest = Priorities.RestPriority(body.EnergyPct, resting);
        if (!ActiveNow(world, animal.Species) && body.EnergyPct < (resting ? 1 : Priorities.RestedPct) && rest < Priority.Need) rest = Priority.Need;
        var food = Priorities.AnimalFoodPriority(body, SpeciesTable.Of(animal.Species).FatPct, seeingTo: mind.Plan?.Stat == Stat.Food);
        var priorities = new StatPriorities(Priority.None, water, food, rest);
        // A plan holds until its stat is seen to or another outranks the priority it was made at (as an NPC's goal does).
        if (mind.Plan is { } plan && priorities[plan.Stat] >= Priority.Need && !Priorities.Ladder.Any(s => Priorities.Outranks(s, priorities[s], plan.Stat, plan.Priority)))
            return (plan.Stat, priorities[plan.Stat]);
        return Priorities.Top(priorities, Priority.Need);
    }

    static (Activity, int)? Needs(World world, Agent animal, HerdMind mind, List<WorldEvent> events)
    {
        if (Due(world, animal, mind) is not var (stat, priority))
        {
            mind.Plan = null;
            return null;
        }
        // A raven or fox at a kill a predator holds waits it out over it, or keeps off (Scavenging.cs).
        if (stat == Stat.Food && HeldKill(world, animal)) return KeepOff(world, animal, mind, events);
        // Alone, a hungry animal goes to kin it can make out first; its food plan's cohesion would keep it there.
        if (stat == Stat.Food && priority < Priority.Critical && SideCount(world, animal) == 1 && Cohere(world, animal, mind) is { } toKin) return toKin;
        bool sensed = mind.Plan == null || world.Minute % SenseEveryMinutes == 0;
        bool carcassSeen = sensed && Sense(world, animal, mind);
        if (mind.Plan is { Route: [var next, ..] } walking && next == animal.Tile)
        {
            walking.Route.RemoveAt(0);
            if (walking.Route.Count == 0 && stat == Stat.Food) mind.Plan = walking with { PerMinute = FoodAt(world, animal, mind, animal.Tile)?.PerMinute ?? 0 };
        }
        if (Interrupt(world, animal, mind, stat, priority, carcassSeen, events) is string why)
        {
            if (!sensed) Sense(world, animal, mind); // a plan is made on what it senses now
            // Kin setting out: following them is weighed against staying, not a search of everywhere.
            TileId? theirs = why == KinLeaving && Following(world, animal) is (var way, var lead)
                ? MindOf(lead).Plan is { At: TileId at } && at != animal.Tile ? at : Map.Step(animal.Tile, way) : null;
            mind.Plan = PlanFor(world, animal, mind, new AnimalView(world, mind), stat, priority, theirs);
        }
        var plan = mind.Plan!;
        if (plan.Route.Count > 0) return (new Travel(animal.Id, Way(animal.Tile, plan.Route[0])), UntilArrived);
        if (plan.At == null)
        {
            // Nothing to eat in reach: a meat-eater hunts on its state, keeping the plan so it does not look again every minute.
            if (stat == Stat.Food && !IsPrey(animal.Species)) return null;
            if (Threatened(animal, mind) is TileId from)
            {
                StartFleeing(world, mind, from);
                return Flee(world, animal, mind, BehaviorOf(animal.Species), events);
            }
            return stat == Stat.Rest ? Sleep(world, animal, mind) : Wait(animal);
        }
        return stat switch
        {
            Stat.Rest => Sleep(world, animal, mind),
            Stat.Food => Eat(world, animal, mind),
            _ => (new Watering(animal.Id), 1),
        };
    }

    /// At its source: the meat if a carcass here pays better than grazing, else grazing.
    static (Activity, int) Eat(World world, Agent animal, HerdMind mind)
    {
        bool meat = MeatAt(world, animal, mind, animal.Tile) > (GrazeHere(world, animal)?.PerMinute ?? 0);
        var state = meat ? HerdState.Feeding : HerdState.Grazing;
        if (mind.State != state) Become(world, mind, state);
        return meat ? (new Feed(animal.Id), 1) : GrazeRefusal(world, animal) == null ? (new Graze(animal.Id), 1) : Wait(animal);
    }

    static double FeedKcalPerMinute => FeedKgPerMinute * MeatKcalPerKg;

    /// A forecast: the kcal a minute a carcass pays a meat-eater on the tile, here from what it can reach, elsewhere from what it has sensed.
    static double MeatAt(World world, Agent animal, HerdMind mind, TileId tile)
    {
        if (IsPrey(animal.Species)) return 0;
        if (tile == animal.Tile) return CanFeed(world, animal) && !HeldKill(world, animal) ? FeedKcalPerMinute : 0;
        return mind.Sightings?.Exists(s => s.Carcass != null && s.Tile == tile) == true ? FeedKcalPerMinute : 0;
    }

    static Source? FoodAt(World world, Agent animal, HerdMind mind, TileId tile)
    {
        double rate = Math.Max(MeatAt(world, animal, mind, tile), GrazeAt(world, animal, tile)?.PerMinute ?? 0);
        return rate > 0 ? new Source(rate) : null;
    }

    static Direction Way(TileId from, TileId to)
    {
        foreach (var d in Map.Directions) if (Map.Step(from, d) == to) return d;
        throw new ArgumentException($"{to} is not a step from {from}");
    }

    static (Activity, int) Sleep(World world, Agent animal, HerdMind mind)
    {
        if (mind.State != HerdState.Resting) Become(world, mind, HerdState.Resting);
        return Rest(animal);
    }

    /// The five interrupts (and a plan's own end), or null while the plan holds; staying put for want of a plan is weighed again every two hours.
    static string? Interrupt(World world, Agent animal, HerdMind mind, Stat stat, Priority priority, bool carcassSeen, List<WorldEvent> events)
    {
        if (mind.Plan is not { } plan) return "no plan";
        if (world.ReplanEveryDecision) return "planning every decision";
        if (plan.Stat != stat || priority > plan.Priority) return "a stat rose above the plan's";
        if (plan.At == null) return world.Minute % PlanAgainEveryMinutes == 0 ? "time to look again" : carcassSeen && stat == Stat.Food ? "a carcass sensed" : null;
        if (Following(world, animal) is (_, var lead) && (lead.Crossing is { ElapsedMinutes: <= 1 } || lead.LastLeft?.Minute >= world.Minute - 1))
            return KinLeaving;
        // At its source it minds a threat on the tile itself, as the choice does; on the way, one on or beside its path.
        if (priority < Priority.Critical && (plan.Route.Count == 0 ? new AnimalView(world, mind).Threat(plan.At.Value)
            : plan.Route.Append(plan.At.Value).Any(t => ThreatNear(mind, t)))) return "a threat on the way";
        if (plan.Route.Count == 0)
        {
            if (animal.Tile != plan.At) return "off its way";
            if (stat == Stat.Water) return WaterHere(world, animal) == null ? "the source is gone" : null;
            if (stat != Stat.Food) return null;
            // Grazed down or crowded, it weighs going on: leaving falls out of the choice (spec "Grazing").
            double rate = FoodAt(world, animal, mind, animal.Tile)?.PerMinute ?? 0;
            return rate <= 0 ? "the source is gone" : rate * Planner.StayFactor < plan.PerMinute ? "the source has thinned" : null;
        }
        var next = plan.Route[0];
        return MapGen.Manhattan(animal.Tile, next) != 1 || !CanMove(animal) || Traversal.Blocked(world, animal, animal.Tile, next) != null
            || HeldOff(world, animal, next, events) ? "the next step is refused" : null;
    }

    static Plan PlanFor(World world, Agent animal, HerdMind mind, WorldView view, Stat stat, Priority priority, TileId? only = null)
    {
        var body = animal.Body;
        var row = SpeciesTable.Of(animal.Species);
        double earn = row.GrazeKcalPerMinute > 0 ? row.GrazeKcalPerMinute : BurnKcalPerMinute(body);
        double amount = Math.Max(1, stat switch
        {
            Stat.Water => (Priorities.WaterAgainPct - body.HydrationPct) * Body.MaxHydrationMlOf(body),
            Stat.Food => RoomKcal(animal, body),
            _ => (Priorities.RestedPct - body.EnergyPct) * Body.MaxEnergyMinutes,
        });
        double best = stat == Stat.Food ? Math.Max(row.GrazeKcalPerMinute, IsPrey(animal.Species) ? 0 : FeedKcalPerMinute) : double.PositiveInfinity;
        var ask = new Asking(stat, priority, amount, earn, row.Cohesion, t => KinAt(world, animal, t), best);
        var den = Den(animal, mind);
        Func<TileId, Source?> sourceAt = stat switch
        {
            Stat.Water => t => Work.WaterAt(view, t, body),
            Stat.Food => t => FoodAt(world, animal, mind, t),
            _ => t => t == animal.Tile || t == den ? new Source(RestPerMinute(animal, t)) : null,
        };
        // Rest is here or at a den; food elsewhere is grazing or a sensed carcass (a meat-eater that sensed none looks nowhere).
        bool routeAway = CanMove(animal) && stat switch
        {
            Stat.Water => true,
            Stat.Food => row.GrazeKcalPerMinute > 0 || mind.Sightings?.Exists(s => s.Carcass != null) == true,
            _ => den != null && den != animal.Tile,
        };
        return Planner.Choose(world, animal.Id, view, ask, sourceAt, routeAway, only);
    }

    /// Kin standing on the tile or one of the 8 around it, or crossing onto one of them: a herd
    /// spread over a few tiles while it grazes stays one herd. One crossing stands on the tile it left, so two rings are read.
    static bool KinAt(World world, Agent animal, TileId tile)
    {
        for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
            {
                var standing = Nearby.At(world, new TileId(tile.X + dx, tile.Y + dy));
                for (int i = 0; i < standing.Count; i++)
                {
                    var k = world.Agents[standing[i]];
                    if (k.Id == animal.Id || !IsKin(animal, k)) continue;
                    var at = k.Crossing is { } c ? Map.Step(c.From, c.Dir) : k.Tile;
                    if (Math.Abs(at.X - tile.X) <= 1 && Math.Abs(at.Y - tile.Y) <= 1) return true;
                }
            }
        return false;
    }

    /// Prey sense predators and people, meat-eaters carcasses (kept till seen gone), by sight x perception; true: a carcass anew.
    static bool Sense(World world, Agent animal, HerdMind mind)
    {
        mind.Sightings?.RemoveAll(s => s.Who != null && world.Minute - s.Minute > KeepSightingMinutes);
        var behavior = BehaviorOf(animal.Species);
        bool prey = behavior == HerdBehavior.Prey, meat = !prey, carcassSeen = false;
        if (!prey && !meat)
        {
            if (mind.Sightings?.Count == 0) mind.Sightings = null;
            return false;
        }
        double budget = Sight.BudgetTiles(world, animal.Tile, Body.GetCapacities(animal.Body).Sight) * SpeciesTable.Of(animal.Species).PerceptionFactor;
        bool Sees(TileId t) => t == animal.Tile || MapGen.Manhattan(t, animal.Tile) <= budget && Sight.Sees(world, animal.Tile, t, budget);
        if (meat)
        {
            mind.Sightings?.RemoveAll(s => s.Carcass is int id && Sees(s.Tile) && !HasMeat(world, s.Tile, id));
            foreach (var t in Carcasses.TilesWith(world))
            {
                if (MapGen.Manhattan(t, animal.Tile) > budget || !Sees(t)) continue;
                foreach (var c in world.Tiles[t].Carcasses ?? [])
                {
                    if (Remains.MeatKg(world, t, c) <= 0 || mind.Sightings?.Exists(s => s.Carcass == c.Id) == true) continue;
                    (mind.Sightings ??= []).Add(new Sensed(null, t, world.Minute, c.Id));
                    carcassSeen = true;
                }
            }
        }
        if (prey)
        {
            foreach (var other in Nearby.Within(world, animal.Tile, (int)Math.Ceiling(budget)))
            {
                if (!IsThreatTo(world, animal, other) || !Sight.Sees(world, animal.Tile, other.Tile, budget)) continue;
                mind.Sightings ??= [];
                mind.Sightings.RemoveAll(s => s.Who == other.Id);
                mind.Sightings.Add(new Sensed(other.Id, other.Tile, world.Minute));
            }
        }
        if (mind.Sightings?.Count == 0) mind.Sightings = null;
        return carcassSeen;
    }

    static bool HasMeat(World world, TileId tile, int carcass) =>
        world.Tiles.GetValueOrDefault(tile)?.Carcasses?.Find(c => c.Id == carcass) is { } c && Remains.MeatKg(world, tile, c) > 0;

    static bool IsThreatTo(World world, Agent animal, Agent other) => other.Id != animal.Id && Sim.IsAlive(other)
        && (other.Mind is HerdMind ? IsPredator(other.Species) && other.Species != animal.Species : other.Mind is PersonMind && Sim.InTheWorld(world, other));

    static bool ThreatNear(HerdMind mind, TileId tile) => mind.Sightings?.Exists(s => s.Who != null && MapGen.Manhattan(s.Tile, tile) <= 1) == true;

    static TileId? Threatened(Agent animal, HerdMind mind) =>
        mind.Sightings?.Where(s => s.Who != null && MapGen.Manhattan(s.Tile, animal.Tile) <= 1).Select(s => (TileId?)s.Tile).FirstOrDefault();
}
