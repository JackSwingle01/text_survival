namespace Survival;

public sealed record Sensed(AgentId Who, TileId Tile, long Minute);

/// Water and rest on the shared planner (food stays on its state for now): the top stat at Need or above gets a plan.
public static partial class Fauna
{
    const int SenseEveryMinutes = 5, KeepSightingMinutes = 180, PlanAgainEveryMinutes = 120;
    /// Asleep away from its den, a denning animal sleeps lighter (D).
    const double OpenSleepFactor = 0.75;

    sealed class AnimalView(World world, HerdMind mind) : WorldView
    {
        public override bool Threat(TileId tile) => mind.Sightings?.Exists(s => s.Tile == tile) == true;
        public override bool OpenWater(TileId tile) => Work.HasUnfrozenWater(world, tile);
        public override GroundSeen Ground(TileId tile) => Survival.Ground.WaterEstimate(world, tile);
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
        var food = Priorities.FoodPriority(body, SpeciesTable.Of(animal.Species).FatPct * body.WeightKg, seeingTo: false, foodToHand: false);
        var priorities = new StatPriorities(Priority.None, water, food, rest);
        // A plan holds until its stat is seen to or another outranks the priority it was made at (as an NPC's goal does).
        if (mind.Plan is { } plan && priorities[plan.Stat] >= Priority.Need && !Priorities.Ladder.Any(s => Priorities.Outranks(s, priorities[s], plan.Stat, plan.Priority)))
            return (plan.Stat, priorities[plan.Stat]);
        return Priorities.Top(priorities, Priority.Need) is { Stat: not Stat.Food } top ? top : null;
    }

    static (Activity, int)? Needs(World world, Agent animal, HerdMind mind, List<WorldEvent> events)
    {
        if (Due(world, animal, mind) is not var (stat, priority))
        {
            mind.Plan = null;
            return null;
        }
        bool sensed = mind.Plan == null || world.Minute % SenseEveryMinutes == 0;
        if (sensed) Sense(world, animal, mind);
        if (mind.Plan is { Route: [var next, ..] } walking && next == animal.Tile) walking.Route.RemoveAt(0);
        if (Interrupt(world, animal, mind, stat, priority, events) != null)
        {
            if (!sensed) Sense(world, animal, mind); // a plan is made on what it senses now
            mind.Plan = PlanFor(world, animal, mind, new AnimalView(world, mind), stat, priority);
        }
        var plan = mind.Plan!;
        if (plan.Route.Count > 0) return (new Travel(animal.Id, Way(animal.Tile, plan.Route[0])), UntilArrived);
        if (plan.At == null)
        {
            if (Threatened(animal, mind) is TileId from)
            {
                StartFleeing(world, mind, from);
                return Flee(world, animal, mind, BehaviorOf(animal.Species), events);
            }
            return stat == Stat.Rest ? Sleep(world, animal, mind) : Wait(animal);
        }
        return stat == Stat.Rest ? Sleep(world, animal, mind) : (new Watering(animal.Id), 1);
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
    static string? Interrupt(World world, Agent animal, HerdMind mind, Stat stat, Priority priority, List<WorldEvent> events)
    {
        if (mind.Plan is not { } plan) return "no plan";
        if (world.ReplanEveryDecision) return "planning every decision";
        if (plan.Stat != stat || priority > plan.Priority) return "a stat rose above the plan's";
        if (plan.At == null) return world.Minute % PlanAgainEveryMinutes == 0 ? "time to look again" : null;
        if (Following(world, animal) != null) return "its kin are leaving";
        if (priority < Priority.Critical && plan.Route.Append(plan.At.Value).Any(t => ThreatNear(mind, t))) return "a threat on the way";
        if (plan.Route.Count == 0)
            return animal.Tile != plan.At ? "off its way" : stat == Stat.Water && WaterHere(world, animal) == null ? "the source is gone" : null;
        var next = plan.Route[0];
        return MapGen.Manhattan(animal.Tile, next) != 1 || !CanMove(animal) || Traversal.Blocked(world, animal, animal.Tile, next) != null
            || HeldOff(world, animal, next, events) ? "the next step is refused" : null;
    }

    static Plan PlanFor(World world, Agent animal, HerdMind mind, WorldView view, Stat stat, Priority priority)
    {
        var body = animal.Body;
        var row = SpeciesTable.Of(animal.Species);
        double earn = row.GrazeKcalPerMinute > 0 ? row.GrazeKcalPerMinute : BurnKcalPerMinute(body);
        double amount = stat == Stat.Water
            ? Math.Max(1, (Priorities.WaterAgainPct - body.HydrationPct) * Body.MaxHydrationMlOf(body))
            : Math.Max(1, (Priorities.RestedPct - body.EnergyPct) * Body.MaxEnergyMinutes);
        var ask = new Asking(stat, priority, amount, earn, row.Cohesion, t => KinAt(world, animal, t));
        var den = Den(animal, mind);
        Func<TileId, Source?> sourceAt = stat == Stat.Water ? t => Work.WaterAt(view, t, body)
            : t => t == animal.Tile || t == den ? new Source(RestPerMinute(animal, t)) : null;
        // Rest is where it stands, or its den: only a denning animal has anywhere to go for it.
        bool routeAway = CanMove(animal) && (stat == Stat.Water || den != null && den != animal.Tile);
        return Planner.Choose(world, animal.Id, view, ask, sourceAt, routeAway);
    }

    /// Kin on the tile, or crossing to it (read on every tile a plan weighs, so counted, not listed).
    static bool KinAt(World world, Agent animal, TileId tile)
    {
        if (KinStanding(world, animal, tile, tile)) return true;
        foreach (var d in Map.Directions)
            if (KinStanding(world, animal, Map.Step(tile, d), tile)) return true;
        return false;
    }

    static bool KinStanding(World world, Agent animal, TileId on, TileId tile)
    {
        foreach (var id in Nearby.At(world, on))
        {
            var k = world.Agents[id];
            if (k.Id != animal.Id && IsKin(animal, k) && (k.Tile == tile && !Leaving(k, tile) || k.Crossing is { } c && Map.Step(c.From, c.Dir) == tile))
                return true;
        }
        return false;
    }

    /// Prey sense predators of other kinds and people, with a person's sight scaled by the species' perception.
    static void Sense(World world, Agent animal, HerdMind mind)
    {
        mind.Sightings?.RemoveAll(s => world.Minute - s.Minute > KeepSightingMinutes);
        if (BehaviorOf(animal.Species) == HerdBehavior.Prey)
        {
            double budget = Sight.BudgetTiles(world, animal.Tile, Body.GetCapacities(animal.Body).Sight) * SpeciesTable.Of(animal.Species).PerceptionFactor;
            foreach (var other in Nearby.Within(world, animal.Tile, (int)Math.Ceiling(budget)))
            {
                if (!IsThreatTo(world, animal, other) || !Sight.Sees(world, animal.Tile, other.Tile, budget)) continue;
                mind.Sightings ??= [];
                mind.Sightings.RemoveAll(s => s.Who == other.Id);
                mind.Sightings.Add(new Sensed(other.Id, other.Tile, world.Minute));
            }
        }
        if (mind.Sightings?.Count == 0) mind.Sightings = null;
    }

    static bool IsThreatTo(World world, Agent animal, Agent other) => other.Id != animal.Id && Sim.IsAlive(other)
        && (other.Mind is HerdMind ? IsPredator(other.Species) && other.Species != animal.Species : other.Mind is PersonMind && Sim.InTheWorld(world, other));

    static bool ThreatNear(HerdMind mind, TileId tile) => mind.Sightings?.Exists(s => MapGen.Manhattan(s.Tile, tile) <= 1) == true;

    static TileId? Threatened(Agent animal, HerdMind mind) =>
        mind.Sightings?.Where(s => MapGen.Manhattan(s.Tile, animal.Tile) <= 1).Select(s => (TileId?)s.Tile).FirstOrDefault();
}
