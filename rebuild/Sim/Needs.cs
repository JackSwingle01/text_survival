namespace Survival;

/// A threat (Who) or a herd of quarry (Quarry, Count), kept KeepSightingMinutes, or a carcass, kept until seen bare or gone.
public sealed record Sensed(AgentId? Who, TileId Tile, long Minute, int? Carcass = null,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<Species>))]
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    Species? Quarry = null, int Count = 0);

/// Water, rest and food on the shared planner: the top stat at Need or above gets a plan.
public static partial class Fauna
{
    const string KinLeaving = "its kin are leaving";
    const int SenseEveryMinutes = 5, KeepSightingMinutes = 180, PlanAgainEveryMinutes = 120;
    /// Asleep away from its den, a denning animal sleeps lighter (D).
    const double OpenSleepFactor = 0.75;

    // Sightings are read by loops here and below: these run per tile or per minute, where a lambda costs an object each.
    sealed class AnimalView(World world, HerdMind mind) : WorldView
    {
        public override bool Threat(TileId tile)
        {
            if (mind.Sightings is not { } seen) return false;
            for (int i = 0; i < seen.Count; i++)
                if (seen[i].Who != null && seen[i].Tile == tile) return true;
            return false;
        }
        public override bool OpenWater(TileId tile) => Work.HasUnfrozenWater(world, tile);
        public override GroundSeen Ground(TileId tile)
        {
            var surface = Survival.Ground.Underfoot(world, tile);
            return new GroundSeen(surface.SnowDepthM, surface.DrinkableWaterM);
        }
    }

    /// A forecast: what an animal drinks a minute where it stands (Work.WaterAt, from what it knows).
    public static Source? WaterHere(World world, Agent animal) => Work.WaterAt(new AnimalView(world, MindOf(animal)), animal.Tile, animal.Body);

    /// A forecast: the rest a minute's sleep here buys, in energy minutes (Body.EnergyDeltaPerMinute).
    public static double RestPerMinute(Agent animal, TileId tile) =>
        Body.EnergyDeltaPerMinute(sleeping: true, SpeciesTable.Of(animal.Species).SleepHoursPerDay) * SleepFactor(animal, tile);

    static double SleepFactor(Agent animal, TileId tile) => MindOf(animal).Den is TileId den && den != tile ? OpenSleepFactor : 1;

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
        if (mind.Plan is { } plan && priorities[plan.Stat] >= Priority.Need)
        {
            bool outranked = false;
            foreach (var s in Priorities.Ladder) outranked |= Priorities.Outranks(s, priorities[s], plan.Stat, plan.Priority);
            if (!outranked) return (plan.Stat, priorities[plan.Stat]);
        }
        return Priorities.Top(priorities, Priority.Need);
    }

    static (Activity, int)? Needs(World world, Agent animal, HerdMind mind, List<WorldEvent> events)
    {
        bool closed = IsClosedGroup(animal.Species);
        if (Due(world, animal, mind) is not var (stat, priority))
        {
            mind.Plan = null;
            if (!closed) return null;
            if (world.Minute % SenseEveryMinutes == 0) Sense(world, animal, mind);
            return GiveWay(world, animal, mind, events);
        }
        // A raven or fox at a kill a predator holds waits it out over it, or keeps off, unless starving (Scavenging.cs).
        if (stat == Stat.Food && priority < Priority.Critical && HeldKill(world, animal)) return KeepOff(world, animal, mind, events);
        if (stat == Stat.Food && WouldComeForSomeone(world, animal, events)) return null;
        // Alone, a hungry animal goes to kin it can make out first; prey a tile from kin are with them, or they'd crowd it.
        if (stat == Stat.Food && priority < Priority.Critical && !WithKin(world, animal) && Cohere(world, animal, mind) is { } toKin) return toKin;
        bool arrived = false;
        if (mind.Plan is { Route: [var next, ..] } walking && next == animal.Tile)
        {
            walking.Route.RemoveAt(0);
            arrived = walking.Route.Count == 0;
        }
        bool sensed = mind.Plan == null || arrived || world.Minute % SenseEveryMinutes == 0;
        bool foodSeen = sensed && Sense(world, animal, mind);
        if (arrived && stat == Stat.Food) mind.Plan = mind.Plan! with { PerMinute = FoodAt(world, animal, mind, animal.Tile)?.PerMinute ?? 0 };
        if (Interrupt(world, animal, mind, stat, priority, foodSeen, events) is string why)
        {
            if (!sensed) Sense(world, animal, mind);
            // Kin setting out: following them is weighed against staying, not a search of everywhere.
            TileId? theirs = why == KinLeaving && Following(world, animal) is (var way, var lead)
                ? MindOf(lead).Plan is { At: TileId at } && at != animal.Tile ? at : Map.Step(animal.Tile, way) : null;
            if (stat == Stat.Food) Disperse(world, animal, mind);
            mind.Plan = PlanFor(world, animal, mind, new AnimalView(world, mind), stat, priority, theirs);
        }
        var plan = mind.Plan!;
        if (plan.Route.Count > 0) return (new Travel(animal.Id, Way(animal.Tile, plan.Route[0])), UntilArrived);
        if (plan.At == null)
        {
            // Nothing to eat nor roam to: a meat-eater waits for kin coming, else lives on its state, keeping the plan.
            if (stat == Stat.Food && !IsPrey(animal.Species)) return IsClosedGroup(animal.Species) && KinComing(world, animal) ? Wait(animal) : null;
            return GiveWay(world, animal, mind, events) ?? (stat == Stat.Rest ? Rest(animal) : Wait(animal));
        }
        return stat switch
        {
            Stat.Rest => Rest(animal),
            Stat.Food => Eat(world, animal, mind),
            _ => (new Watering(animal.Id), 1),
        };
    }

    /// Below Critical, with no plan holding it, it runs from a threat it senses beside it.
    static (Activity, int)? GiveWay(World world, Agent animal, HerdMind mind, List<WorldEvent> events)
    {
        bool closed = IsClosedGroup(animal.Species);
        if (!CanMove(animal) || Threatened(animal, mind, closed ? world.Minute - SenseEveryMinutes : long.MinValue) is not TileId threat) return null;
        // It steps a tile off a threat beside it; prey with one on their tile keep their way (Flee).
        if ((!closed && threat == animal.Tile) || FleeDirection(world, animal, threat, events) is not Direction away)
        {
            StartFleeing(world, mind, threat);
            return Flee(world, animal, mind, events);
        }
        StartFleeing(world, mind, animal.Tile);
        return (new Travel(animal.Id, away), UntilArrived);
    }

    /// At its source: the hunt if the quarry here pays best, else the meat if a carcass pays better than grazing, else grazing.
    static (Activity, int) Eat(World world, Agent animal, HerdMind mind)
    {
        double graze = GrazeHere(world, animal)?.PerMinute ?? 0, carcass = MeatAt(world, animal, mind, animal.Tile);
        if (HuntAt(world, animal, mind, animal.Tile, Hunting(world, animal)) is var hunt && hunt > Math.Max(graze, carcass))
            return QuarryHere(world, animal) is Agent prey && Refusal(world, new Hunt(animal.Id, prey.Id)) == null && !SideFoughtThisMinute(world, animal)
                ? (new Hunt(animal.Id, prey.Id), 1) : Wait(animal);
        bool meat = carcass > graze;
        return meat ? (new Feed(animal.Id), 1) : GrazeRefusal(world, animal) == null ? (new Graze(animal.Id), 1) : Wait(animal);
    }

    static double FeedKcalPerMinute => FeedKgPerMinute * MeatKcalPerKg;

    /// A forecast: the kcal a minute a carcass pays a meat-eater on the tile, here from what it can reach, elsewhere from what it has sensed.
    static double MeatAt(World world, Agent animal, HerdMind mind, TileId tile)
    {
        if (IsPrey(animal.Species)) return 0;
        if (tile == animal.Tile) return CanFeed(world, animal) && !HeldKill(world, animal) ? FeedKcalPerMinute : 0;
        if (WalkerKeepsOff(world, animal, tile)) return 0;
        if (mind.Sightings is not { } seen) return 0;
        for (int i = 0; i < seen.Count; i++)
            if (seen[i].Carcass != null && seen[i].Tile == tile) return FeedKcalPerMinute;
        return 0;
    }

    static Source? FoodAt(World world, Agent animal, HerdMind mind, TileId tile, HuntingParty? hunting = null)
    {
        double rate = Math.Max(MeatAt(world, animal, mind, tile), GrazeAt(world, animal, tile)?.PerMinute ?? 0);
        // Quarry away from its tile is a source only when its pack is ready to go together.
        if (mind.Sightings != null && Combat.Hunts(animal.Species) && (hunting ?? Hunting(world, animal)) is var party && (party.Together || tile == animal.Tile))
            rate = Math.Max(rate, HuntAt(world, animal, mind, tile, party));
        return rate > 0 ? new Source(rate) : null;
    }

    /// Who would hunt with it (Hunters), the room it has to eat into a kill, and whether its pack would set out with it (PackReady).
    readonly record struct HuntingParty(int Hunters, double RoomKcal, bool Together = true);

    /// The gate on a hunt (HuntPays): a kill chance floor at any hunger, and the injury chance it risks, fed to starving (Desperation).
    const double KillChanceFloor = 0.10, CalmInjuryTolerance = 0.15, DesperateInjuryTolerance = 0.75;

    static HuntingParty Hunting(World world, Agent animal) => new(Hunters(world, animal).Count, Math.Max(1, RoomKcal(animal, animal.Body)));

    /// 0 at or below its species' hungryAt to 1 at or above its starvingAt (temperament), from its own hunger; read, never kept.
    public static double Desperation(Agent animal)
    {
        var t = SpeciesTable.Of(animal.Species).Temperament;
        double hunger = Hunger(animal);
        if (hunger <= t.HungryAt) return 0;
        return hunger >= t.StarvingAt ? 1 : (hunger - t.HungryAt) / (t.StarvingAt - t.HungryAt);
    }

    /// Whether a hunt with these chances (Combat.KillChance, Combat.InjuryChance) is a food source to the animal.
    public static bool HuntPays(Agent animal, double killChance, double injuryChance) =>
        killChance >= KillChanceFloor
        && injuryChance <= CalmInjuryTolerance + (DesperateInjuryTolerance - CalmInjuryTolerance) * Desperation(animal);

    static double GatedKillChance(Agent animal, int hunters, Species quarry, int herd) =>
        Combat.KillChance(animal.Species, hunters, quarry, herd) is var kill
        && HuntPays(animal, kill, Combat.InjuryChance(animal.Species, hunters, quarry, herd)) ? kill : 0;

    /// A forecast: the kcal a minute a hunt on the tile pays it, from the herds it has sensed there (HuntAt, its hunters as they are).
    public static double HuntSource(World world, Agent animal, TileId tile) => HuntAt(world, animal, MindOf(animal), tile, Hunting(world, animal));

    /// A forecast: the best gated kill chance (GatedKillChance) of the herds sensed there x the feeding rate x its share; none while afraid.
    static double HuntAt(World world, Agent animal, HerdMind mind, TileId tile, HuntingParty hunting)
    {
        if (mind.Fear > Tags.AfraidLevel || mind.Sightings is not { } seen) return 0;
        double best = 0;
        for (int i = 0; i < seen.Count; i++)
        {
            if (seen[i] is not { Quarry: Species quarry } s || s.Tile != tile) continue;
            double chance = GatedKillChance(animal, hunting.Hunters, quarry, s.Count);
            best = Math.Max(best, chance * FeedKcalPerMinute * Math.Min(1, MeatKcalOf(quarry) / (hunting.Hunters * hunting.RoomKcal)));
        }
        return best;
    }

    /// The meat a kill of the species leaves (Carcasses.Create's rule, on the species' build).
    static double MeatKcalOf(Species s) => Carcasses.MeatKgOf(SpeciesTable.Of(s).WeightKg * SpeciesTable.Of(s).MusclePct) * MeatKcalPerKg;

    /// The quarry on its tile it has the best kill chance against of those that pass the gate (HuntPays), the first in id order on a tie.
    static Agent? QuarryHere(World world, Agent predator)
    {
        int hunters = Hunters(world, predator).Count;
        Agent? best = null;
        double bestChance = 0;
        foreach (var a in Nearby.At(world, predator.Tile))
        {
            if (a.Mind is not HerdMind || !IsQuarry(a.Species) || !Sim.IsAlive(a) || Combat.EncounterOf(world, a.Id) != null) continue;
            double chance = GatedKillChance(predator, hunters, a.Species, Combat.Side(world, a).Count);
            if (chance > bestChance) (best, bestChance) = (a, chance);
        }
        return best;
    }

    /// Whether a predator that comes for people (ComeForPeople) senses one it would come for now.
    static bool WouldComeForSomeone(World world, Agent predator, List<WorldEvent> events)
    {
        if (BehaviorOf(predator.Species) != HerdBehavior.Pack || Hunger(predator) <= ComesForPeopleHunger || MindOf(predator).Fear > Tags.AfraidLevel) return false;
        return PeopleSensed(world, predator, events).Count > 0;
    }

    /// Its species' sight on open ground in clear weather (Sight.OpenGroundTiles x perception): how far a roam goes.
    static int RoamTiles(Species s) => Math.Max(1, (int)(Sight.OpenGroundTiles * SpeciesTable.Of(s).PerceptionFactor));

    /// A meat-eater that hunts and lives on nothing else roams when it senses nothing to eat.
    static bool Roams(Agent animal) => Combat.Hunts(animal.Species) && SpeciesTable.Of(animal.Species).GrazeKcalPerMinute <= 0;

    /// A pack sets out for food as one: every one beside it wants food and sees to nothing else, none is on its way to it; starving, it goes.
    static bool PackReady(World world, Agent animal, Priority priority)
    {
        if (priority == Priority.Critical || !IsClosedGroup(animal.Species)) return true;
        foreach (var k in KinBeside(world, animal))
            if (!FoodDue(k) || MindOf(k).Plan is { Stat: not Stat.Food }) return false;
        return !KinComing(world, animal);
    }

    /// Its kin standing on its tile or the 8 around it, in id order: a pack spread over a few tiles is together (as KinAt).
    static List<Agent> KinBeside(World world, Agent animal)
    {
        var kin = new List<Agent>();
        foreach (var k in Nearby.Within(world, animal.Tile, 2))
            if (IsKin(animal, k) && Math.Abs(k.Tile.X - animal.Tile.X) <= 1 && Math.Abs(k.Tile.Y - animal.Tile.Y) <= 1 && !Sim.Held(world, k.Id)) kin.Add(k);
        return kin;
    }

    static bool KinComing(World world, Agent animal)
    {
        foreach (var k in Nearby.Within(world, animal.Tile, RoamTiles(animal.Species)))
            if (IsKin(animal, k) && k.Tile != animal.Tile && MindOf(k).Plan is { At: TileId at } && at == animal.Tile) return true;
        return false;
    }

    /// Where a packmate beside it (KinBeside) is making for on a food plan (a hunt, a carcass or a roam), the first in id order.
    static TileId? PackmatesTrip(World world, Agent animal)
    {
        foreach (var k in KinBeside(world, animal))
            if (MindOf(k).Plan is { Stat: Stat.Food, At: TileId at, Route.Count: > 0 } && at != animal.Tile) return at;
        return null;
    }

    /// A roam (a food plan paying nothing): where a packmate beside it goes, else a seeded open tile at its sight's edge, clear of threats.
    static Plan? Roam(World world, Agent animal, WorldView view, Priority priority)
    {
        TileId? target = PackmatesTrip(world, animal);
        // The first of its pack beside it in id order leads a roam; the rest follow it (Cohere) rather than pick their own.
        if (target == null && IsClosedGroup(animal.Species) && KinBeside(world, animal).Exists(k => k.Id.CompareTo(animal.Id) < 0)) return null;
        if (target == null)
        {
            var m = Map.Of(world);
            var rng = world.Rng("herds", animal.Id);
            int r = RoamTiles(animal.Species);
            for (int i = 0; i < 8 && target == null; i++)
            {
                int dx = rng.NextInt(2 * r + 1) - r, dy = (r - Math.Abs(dx)) * (rng.NextInt(2) == 0 ? 1 : -1);
                var t = new TileId(animal.Tile.X + dx, animal.Tile.Y + dy);
                if (m.InBounds(t) && m.TerrainAt(t).IsPassable() && m.CaveAt(t) == Cave.None && !view.Threat(t)) target = t;
            }
        }
        return target is TileId to ? Planner.Toward(world, animal.Id, view, Stat.Food, priority, to) : null;
    }

    /// A young wolf (its side's highest id) leaves a big hungry pack when the kill chance alone for the whole kill ÷ (1 + cohesion)
    /// beats its pack's for its share (GatedKillChance); it and they are strangers from then on.
    static void Disperse(World world, Agent animal, HerdMind mind)
    {
        if (!IsClosedGroup(animal.Species) || mind.Sightings == null || !Combat.Hunts(animal.Species)) return;
        double roomKcal = Math.Max(1, RoomKcal(animal, animal.Body));
        var side = Combat.Side(world, animal);
        if (side.Count < 2 || side.MaxBy(a => a.Id)!.Id != animal.Id || !side.TrueForAll(FoodDue)) return;
        int pack = Math.Min(MaxHunters, side.Count);
        double cohesion = SpeciesTable.Of(animal.Species).Cohesion;
        foreach (var s in mind.Sightings)
        {
            if (s.Quarry is not Species quarry) continue;
            double meat = MeatKcalOf(quarry);
            double with = GatedKillChance(animal, pack, quarry, s.Count) * Math.Min(1, meat / (pack * roomKcal));
            double alone = GatedKillChance(animal, 1, quarry, s.Count) * Math.Min(1, meat / roomKcal) / (1 + cohesion);
            if (alone <= with) continue;
            Fought(world, [animal.Id], side.Skip(1).Select(a => a.Id));
            return;
        }
    }

    static Direction Way(TileId from, TileId to)
    {
        foreach (var d in Map.Directions) if (Map.Step(from, d) == to) return d;
        throw new ArgumentException($"{to} is not a step from {from}");
    }


    /// The five interrupts (and a plan's own end), or null while it holds; staying put is weighed again every two hours, or on food sensed.
    static string? Interrupt(World world, Agent animal, HerdMind mind, Stat stat, Priority priority, bool foodSeen, List<WorldEvent> events)
    {
        if (mind.Plan is not { } plan) return "no plan";
        if (world.ReplanEveryDecision) return "planning every decision";
        if (plan.Stat != stat || priority > plan.Priority) return "a stat rose above the plan's";
        if (foodSeen && stat == Stat.Food && plan.PerMinute <= 0) return "food sensed";
        if (stat == Stat.Food && plan.PerMinute <= 0 && IsClosedGroup(animal.Species) && PackmatesTrip(world, animal) is TileId trip && trip != plan.At)
            return "a packmate set out";
        if (plan.At == null) return world.Minute % PlanAgainEveryMinutes == 0 ? "time to look again" : null;
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
        var hunting = stat == Stat.Food && Combat.Hunts(animal.Species) ? Hunting(world, animal) with { Together = PackReady(world, animal, priority) } : default;
        var ask = new Asking(stat, priority, amount, earn, row.Cohesion, t => KinAt(world, animal, t), best);
        var den = mind.Den;
        Func<TileId, Source?> sourceAt = stat switch
        {
            Stat.Water => t => Work.WaterAt(view, t, body),
            Stat.Food => t => FoodAt(world, animal, mind, t, hunting),
            _ => t => t == animal.Tile || t == den ? new Source(RestPerMinute(animal, t)) : null,
        };
        // Rest is here or at a den; food elsewhere is grazing, or a carcass or quarry sensed (else a meat-eater roams).
        bool routeAway = CanMove(animal) && stat switch
        {
            Stat.Water => true,
            Stat.Food => row.GrazeKcalPerMinute > 0 || mind.Sightings?.Exists(s => s.Carcass != null || s.Quarry != null) == true,
            _ => den != null && den != animal.Tile,
        };
        // Hunting together: a pack animal whose food is due joins the food trip a packmate on its tile has set out on.
        if (stat == Stat.Food && IsClosedGroup(animal.Species) && PackmatesTrip(world, animal) is TileId joining
            && Planner.Toward(world, animal.Id, view, stat, priority, joining) is { } join)
            return join with { PerMinute = FoodAt(world, animal, mind, joining, hunting)?.PerMinute ?? 0 };
        var plan = Planner.Choose(world, animal.Id, view, ask, sourceAt, routeAway, only);
        return plan.At == null && stat == Stat.Food && CanMove(animal) && Roams(animal) && hunting.Together ? Roam(world, animal, view, priority) ?? plan : plan;
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
                    var k = standing[i];
                    if (k.Id == animal.Id || !IsKin(animal, k)) continue;
                    var at = k.Crossing is { } c ? Map.Step(c.From, c.Dir) : k.Tile;
                    if (Math.Abs(at.X - tile.X) <= 1 && Math.Abs(at.Y - tile.Y) <= 1) return true;
                }
            }
        return false;
    }

    /// Prey sense predators and people, a pack its rivals, hunters quarry, meat-eaters carcasses (Senses); true: food anew.
    static bool Sense(World world, Agent animal, HerdMind mind)
    {
        mind.Sightings?.RemoveAll(s => s.Carcass == null && world.Minute - s.Minute > KeepSightingMinutes);
        var behavior = BehaviorOf(animal.Species);
        bool prey = behavior == HerdBehavior.Prey, foodSeen = false;
        bool rivals = IsClosedGroup(animal.Species), hunts = !prey && Combat.Hunts(animal.Species);
        var senses = Senses.Now(world, animal);
        if (!prey)
        {
            mind.Sightings?.RemoveAll(s => s.Carcass is int id && senses.Sees(world, s.Tile) && !HasMeat(world, s.Tile, id));
            double reach = Math.Max(senses.SightBudgetTiles, Scent.ReachTiles(1, senses.SmellFactor, 2));
            foreach (var t in Carcasses.TilesWith(world))
            {
                if (MapGen.Manhattan(t, animal.Tile) > reach) continue;
                bool? seen = null;
                foreach (var c in world.Tiles[t].Carcasses ?? [])
                {
                    if (!senses.Smells(t, Carcasses.Scent(world, c)) && !(seen ??= senses.Sees(world, t))) continue;
                    if (Remains.MeatKg(world, t, c) <= 0 || mind.Sightings?.Exists(s => s.Carcass == c.Id) == true) continue;
                    (mind.Sightings ??= []).Add(new Sensed(null, t, world.Minute, c.Id));
                    foodSeen = true;
                }
            }
        }
        if (!prey && !rivals && !hunts) return Done();
        List<Sensed>? herds = null;
        foreach (var other in Nearby.Within(world, animal.Tile, senses.AnimalScanTiles))
            if (other.Mind is not PersonMind) Consider(other);
        if (prey)
            foreach (var person in Nearby.People(world)) Consider(person);
        if (hunts)
        {
            foreach (var h in herds ?? [])
                if (mind.Sightings?.Exists(s => s.Quarry == h.Quarry && s.Tile == h.Tile) != true) foodSeen = true;
            mind.Sightings?.RemoveAll(s => s.Quarry is Species q && senses.Finds(world, s.Tile, SpeciesTable.Of(q).ScentLevel));
            if (herds != null) (mind.Sightings ??= []).AddRange(herds);
        }
        return Done();

        void Consider(Agent other)
        {
            bool threat = (prey || rivals) && IsThreatTo(world, animal, other), quarry = hunts && IsQuarry(other.Species) && other.Mind is HerdMind && Sim.IsAlive(other);
            if (!threat && !quarry || !senses.Finds(world, other.Tile, Scent.Of(world, other))) return;
            if (threat)
            {
                mind.Sightings ??= [];
                mind.Sightings.RemoveAll(s => s.Who == other.Id);
                mind.Sightings.Add(new Sensed(other.Id, other.Tile, world.Minute));
                return;
            }
            herds ??= [];
            int at = herds.FindIndex(h => h.Tile == other.Tile && h.Quarry == other.Species);
            if (at < 0) herds.Add(new Sensed(null, other.Tile, world.Minute, Quarry: other.Species, Count: 1));
            else herds[at] = herds[at] with { Count = herds[at].Count + 1 };
        }

        bool Done()
        {
            if (mind.Sightings?.Count == 0) mind.Sightings = null;
            return foodSeen;
        }
    }

    static bool HasMeat(World world, TileId tile, int carcass) =>
        world.Tiles.GetValueOrDefault(tile)?.Carcasses?.Find(c => c.Id == carcass) is { } c && Remains.MeatKg(world, tile, c) > 0;

    /// To prey, a predator of another species or a person; to a closed group, a stranger of its own species (a rival).
    static bool IsThreatTo(World world, Agent animal, Agent other) => other.Id != animal.Id && Sim.IsAlive(other)
        && (other.Mind is HerdMind
            ? IsPrey(animal.Species) ? IsPredator(other.Species) && other.Species != animal.Species
                : other.Species == animal.Species && IsClosedGroup(animal.Species) && !IsKin(animal, other)
            : IsPrey(animal.Species) && other.Mind is PersonMind && Sim.InTheWorld(world, other));

    static bool ThreatNear(HerdMind mind, TileId tile)
    {
        if (mind.Sightings is not { } seen) return false;
        for (int i = 0; i < seen.Count; i++)
            if (seen[i].Who != null && MapGen.Manhattan(seen[i].Tile, tile) <= 1) return true;
        return false;
    }

    static TileId? Threatened(Agent animal, HerdMind mind, long since = long.MinValue) =>
        mind.Sightings?.Where(s => s.Who != null && s.Minute >= since && MapGen.Manhattan(s.Tile, animal.Tile) <= 1).Select(s => (TileId?)s.Tile).FirstOrDefault();
}
