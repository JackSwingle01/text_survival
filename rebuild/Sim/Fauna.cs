namespace Survival;

/// What a herd is doing, all its members at once (old HerdState). Hunting lasts only as long as the
/// fight; old Alert was a beat before engaging the player, and herds do not engage the player yet.
public enum HerdState { Resting, Grazing, Patrolling, Hunting, Feeding, Fleeing }

/// How a species' herd behaves (old HerdBehaviorType).
public enum HerdBehavior { Prey, Pack, Solitary, Scavenger }

/// What a herd grazes (old AnimalDiet). Carnivores graze nothing.
public enum Diet { Carnivore, Omnivore, Browser, Grazer }

/// A herd's mind (plan decisions 2 and 7): its land, what it is doing and since when, and the
/// hunger and learned fear its members share. Everything a herd knows is here.
public sealed record HerdMind : Mind
{
    public const long Never = -1_000_000;
    /// Tiles it patrols and grazes, its den (or birthplace) first.
    public List<TileId> Territory { get; init; } = [];
    public int TerritoryIndex { get; set; }
    public HerdState State { get; set; }
    /// When it entered State; how long it has been in it is derived from this.
    public long StateSinceMinute { get; set; }
    /// 0 fed, 1 starving.
    public double Hunger { get; set; }
    /// Learned fear 0-1: scales boldness down, and a frightened pack will not hunt.
    public double Fear { get; set; }
    public long LastFightMinute { get; set; } = Never;
    /// Where the threat was, while fleeing.
    public TileId? FleeFrom { get; set; }
}

// Herd activities. Travel, Rest and Wait are the shared ones.

/// Eats what the tile grows, for one minute.
public sealed record Graze(AgentId Who) : Activity(Who);
/// Eats from a carcass on the tile, for one minute.
public sealed record Feed(AgentId Who) : Activity(Who);
/// Goes for a prey herd on the same tile: the fight is fought headless at once (Combat).
public sealed record Hunt(AgentId Who, AgentId Prey) : Activity(Who);

/// Herds: their minds and what they do (port of old Herd, PreyBehavior, PackPredatorBehavior,
/// SolitaryPredatorBehavior, ScavengerBehavior and HerdVigilance). A herd's mind decides one
/// minute at a time, as the old behaviors updated; travel is the shared Travel activity.
// ponytail: herds never come for the player or an NPC yet (old ShouldEngagePlayer, PredatorInteraction's
// stalking, the NPC attacks in PackPredatorBehavior). Player encounters are phase 4 events; Combat.Boldness is ready for them.
public static partial class Fauna
{
    public static HerdBehavior BehaviorOf(Species s) => s switch
    {
        Species.Wolf => HerdBehavior.Pack,
        Species.Bear or Species.CaveBear or Species.SaberTooth => HerdBehavior.Solitary,
        Species.Hyena => HerdBehavior.Scavenger,
        _ => HerdBehavior.Prey,
    };

    /// Old AnimalType.IsPredator: everything that is not prey.
    public static bool IsPredator(Species s) => BehaviorOf(s) != HerdBehavior.Prey;

    public static Diet DietOf(Species s) => s switch
    {
        Species.Bear or Species.CaveBear => Diet.Omnivore,
        Species.Caribou or Species.Megaloceros or Species.Mammoth => Diet.Browser,
        Species.Bison => Diet.Grazer,
        _ => Diet.Carnivore,
    };

    /// Old AnimalDietExtensions.GetConsumedResources: what each diet takes off a tile's forage.
    public static IReadOnlyList<Resource> Eats(Diet d) => d switch
    {
        Diet.Omnivore => OmnivoreFood,
        Diet.Browser => BrowserFood,
        Diet.Grazer => GrazerFood,
        _ => [],
    };
    static readonly Resource[] OmnivoreFood = [Resource.BirchPolypore, Resource.Chaga, Resource.Amadou, Resource.Berries,
        Resource.RoseHip, Resource.JuniperBerry, Resource.Nuts, Resource.Roots, Resource.Honey];
    static readonly Resource[] BrowserFood = [Resource.Usnea, Resource.Kindling, Resource.BirchPolypore]; // old Stick: twigs
    static readonly Resource[] GrazerFood = [Resource.Tinder]; // grass, as dried plant matter

    // Old behavior constants, per minute.
    static double HungerPerMinute(HerdBehavior b) => b switch
    {
        HerdBehavior.Prey => 0.003, // about five and a half hours to get hungry
        HerdBehavior.Solitary => 0.0008,
        _ => 0.001,
    };
    const double PreyGrazePerMinute = 0.008, SolitaryForagePerMinute = 0.003, FearDecayPerMinute = 0.015;
    const double GrazingRatePerKgPerHour = 0.0001;
    const int PackPatrolEveryMinutes = 30, PackPatrolTimeoutMinutes = 120;
    const int CarcassSearchRadius = 8, CarcassSearchEveryMinutes = 10;
    /// Long enough for any crossing: a crossing ends itself on arrival.
    const int UntilArrived = int.MaxValue;

    public static HerdMind NewMind(World world, Species species, List<TileId> territory)
    {
        if (territory.Count == 0) throw new ArgumentException("A herd needs at least one tile of territory");
        return new HerdMind
        {
            Territory = territory,
            State = BehaviorOf(species) switch
            {
                HerdBehavior.Pack or HerdBehavior.Scavenger => HerdState.Patrolling,
                HerdBehavior.Solitary => HerdState.Resting,
                _ => HerdState.Grazing,
            },
            StateSinceMinute = world.Minute,
            Hunger = world.Rng("herds").NextDouble() * 0.3, // start slightly hungry
        };
    }

    public static HerdMind MindOf(Agent herd) =>
        herd.Mind as HerdMind ?? throw new InvalidOperationException($"{herd.Id} is not a herd");

    /// The herds on a tile, in id order.
    public static IEnumerable<Agent> HerdsAt(World world, TileId tile) =>
        world.Agents.Values.Where(a => a.Tile == tile && a.Mind is HerdMind);

    static void Become(World world, HerdMind mind, HerdState state)
    {
        mind.State = state;
        mind.StateSinceMinute = world.Minute;
    }

    static long StateMinutes(World world, HerdMind mind) => world.Minute - mind.StateSinceMinute;
    static double MassKg(Agent herd) => herd.Members.Sum(b => b.WeightKg);

    // ---- one minute of a herd's bodies and appetite (Sim.Live calls this instead of Body.Tick) ----

    /// Hunger grows and fear fades; wounds bleed. A member that bleeds out is a carcass where it
    /// fell; a herd with nobody left is gone. Returns "died" then.
    public static string? Tick(World world, Agent herd, List<WorldEvent> events)
    {
        var mind = MindOf(herd);
        // old game: hunger stood still while a herd was walking, so a herd on the move never got hungrier (bug, fixed).
        mind.Hunger = Math.Min(1, mind.Hunger + HungerPerMinute(BehaviorOf(herd.Species)));
        mind.Fear = Math.Max(0, mind.Fear - FearDecayPerMinute);
        bool anyDied = false;
        foreach (var body in herd.Members)
        {
            // Nothing to fade or bleed (skipping it changes nothing), or a headless fight already lived this minute of it.
            if (body.Effects.Count == 0 || world.Minute <= body.WoundsLivedThroughMinute) continue;
            Body.TickWounds(body, world.Rng("body"));
            anyDied |= !Body.IsAlive(body);
        }
        if (!anyDied) return null;
        foreach (var body in herd.Members.Where(b => !Body.IsAlive(b)).ToList())
        {
            Combat.LeaveCarcass(world, herd.Tile, herd.Species, body);
            events.Add(new Killed(world.Minute, herd.Id, herd.Species, body.WeightKg, herd.Tile, By: null));
            herd.Members.Remove(body);
        }
        if (herd.Members.Count > 0) return null;
        world.Agents.Remove(herd.Id);
        return "died";
    }

    // ---- herd activities ----

    public static bool Handles(Activity a) => a is Graze or Feed or Hunt;

    public static string? Refusal(World world, Activity activity)
    {
        var agent = world.Agent(activity.Who);
        if (agent.Mind is not HerdMind) return "only a herd does that";
        if (activity is not Hunt h) return null;
        if (h.Prey == h.Who || !world.Agents.TryGetValue(h.Prey, out var prey) || prey.Tile != agent.Tile || !Sim.IsAlive(prey))
            return "no such prey here";
        if (Combat.EncounterOf(world, h.Prey) != null || Combat.EncounterOf(world, h.Who) != null) return "already fighting";
        return null;
    }

    /// Starts a herd activity. A hunt is fought here, headless, and takes as many minutes as it
    /// had rounds: those are returned, to be the activity's length.
    internal static int? Begin(World world, Activity activity, List<WorldEvent> events) =>
        activity is Hunt h ? PackHunt(world, world.Agent(h.Who), world.Agent(h.Prey), events) : null;

    /// One minute of a herd activity, after the herd's Tick.
    internal static string? Minute(World world, Agent herd, Activity activity, List<WorldEvent> events)
    {
        if (activity is Graze) GrazeMinute(world, herd);
        else if (activity is Feed) FeedMinute(world, herd);
        return null;
    }

    /// Old Herd.GrazeHere and ForageFeature.Graze, a minute at a time: grazing eases hunger and
    /// thins what this diet eats off the tile's forage, with diminishing returns.
    static void GrazeMinute(World world, Agent herd)
    {
        var mind = MindOf(herd);
        var behavior = BehaviorOf(herd.Species);
        mind.Hunger = Math.Max(0, mind.Hunger - (behavior == HerdBehavior.Prey ? PreyGrazePerMinute : SolitaryForagePerMinute));
        var diet = DietOf(herd.Species);
        if (ForageHere(world, herd.Tile) is not (PoolId id, ForagePool pool)) return;
        double dietFactor = diet == Diet.Omnivore ? 5.0 : 1.0; // bears are efficient foragers
        double mass = MassKg(herd);
        foreach (var r in Eats(diet).Where(r => pool.Items.Any(i => i.Resource == r)))
        {
            double level = Pools.GrazedLevel(world, id, r);
            if (level >= 0.95) continue;
            Pools.SetGrazed(world, id, r, Math.Min(1.0, level + mass / 60.0 * GrazingRatePerKgPerHour * dietFactor * (1 - level)));
        }
    }

    static (PoolId, ForagePool)? ForageHere(World world, TileId tile) =>
        Pools.At(world, tile) is [ForagePool pool, ..] ? (new PoolId(tile, 0), pool) : null;

    /// Old GetGrazingLevelForDiet: how eaten-out this tile is for the herd, 0-1. Nothing it eats
    /// here at all is fully eaten out.
    public static double GrazedLevelHere(World world, Agent herd)
    {
        var diet = DietOf(herd.Species);
        if (diet == Diet.Carnivore) return 0;
        if (ForageHere(world, herd.Tile) is not (PoolId id, ForagePool pool)) return 1.0;
        var here = Eats(diet).Where(r => pool.Items.Any(i => i.Resource == r)).ToList();
        return here.Count == 0 ? 1.0 : here.Average(r => Pools.GrazedLevel(world, id, r));
    }

    /// Meat on the tile's carcasses, kg.
    public static double MeatHere(World world, TileId tile) =>
        world.Tiles.GetValueOrDefault(tile)?.Carcasses?.Sum(c => c.Remaining.GetValueOrDefault(Resource.RawMeat)) ?? 0;

    /// Old PredatorInteraction's feeding: a meal is 3% of the group's mass, eaten no faster than
    /// 0.1 kg a minute a head, and hunger falls only for what was eaten.
    static void FeedMinute(World world, Agent herd)
    {
        var mind = MindOf(herd);
        double mealKg = Math.Max(1, MassKg(herd) * 0.03);
        double want = Math.Min(mealKg * mind.Hunger, 0.1 * herd.Members.Count), eaten = 0;
        foreach (var c in world.Tiles.GetValueOrDefault(herd.Tile)?.Carcasses ?? [])
        {
            double meat = c.Remaining.GetValueOrDefault(Resource.RawMeat), bite = Math.Min(meat, want - eaten);
            if (bite <= 0) continue;
            c.Remaining[Resource.RawMeat] = meat - bite;
            eaten += bite;
        }
        mind.Hunger = Math.Max(0, mind.Hunger - eaten / mealKg);
    }

    // ---- pack hunts (old PackPredatorBehavior.AttemptNpcHunt, HerdVigilance) ----

    /// The pack goes for the weakest few; vigilance decides how the fight opens: noticed prey
    /// start alert at range, surprised prey are ambushed. Returns the fight's length in minutes.
    static int PackHunt(World world, Agent pack, Agent prey, List<WorldEvent> events)
    {
        Become(world, MindOf(pack), HerdState.Hunting);
        var targets = Enumerable.Range(0, prey.Members.Count).Where(i => Body.IsAlive(prey.Members[i]))
            .OrderBy(i => Body.Speed(prey.Members[i]) * Body.Vitality(prey.Members[i])).ThenBy(i => Body.Vitality(prey.Members[i]))
            .Take(3).Select(i => new Fighter(prey.Id, i)).ToList();
        var hunters = Enumerable.Range(0, pack.Members.Count).Where(i => Body.IsAlive(pack.Members[i]))
            .Take(6).Select(i => new Fighter(pack.Id, i)).ToList();
        bool noticed = PreyNotices(world, pack, prey);
        var enc = Combat.Create(world, hunters, targets, pack.Tile, noticed ? 15 : 5, Awareness.Engaged, noticed ? Awareness.Alert : Awareness.Unaware);
        Combat.ResolveHeadless(world, enc, events);
        // ponytail: old hungry wolves (hunger > 0.8) chased escaping prey 40% of the time; they patrol on here.
        return Math.Max(1, enc.Rounds);
    }

    /// Old HerdVigilance.PreyNoticesPredator: the prey's watchfulness against the pack's stealth.
    public static bool PreyNotices(World world, Agent predator, Agent prey)
    {
        var preyMind = MindOf(prey);
        double vigilance = 0.5 + Math.Log(prey.Members.Count + 1) * 0.25;
        vigilance *= preyMind.State switch
        {
            HerdState.Resting => 0.6,
            HerdState.Fleeing => 1.3,
            _ => 1.0,
        };
        if (preyMind.State == HerdState.Resting && StateMinutes(world, preyMind) < 30) vigilance *= 1.4; // just stopped running
        var predMind = MindOf(predator);
        double stealth = 0.5 - predator.Members.Count * 0.02;
        if (predMind.Hunger > 0.7) stealth += 0.15;
        if (predMind.Hunger > 0.9) stealth += 0.15;
        stealth *= predator.Species switch
        {
            Species.Wolf => 1.2,
            Species.Bear or Species.CaveBear => 0.7,
            Species.SaberTooth => 1.3,
            Species.Hyena => 0.8,
            _ => 1.0,
        };
        stealth = Math.Max(0.2, stealth);
        var rng = world.Rng("herds");
        return rng.NextDouble() * vigilance > rng.NextDouble() * stealth;
    }

    /// What a fight leaves in a herd's mind, from its own side (old CombatAftermath.UpdateHerds):
    /// fear by how it went; a predator that killed feeds; prey that did not win, and anything that
    /// scattered, runs. Whatever it was doing is over.
    internal static void AfterFight(World world, AgentId id, CombatResult own, bool killedOne, TileId tile)
    {
        if (!world.Agents.TryGetValue(id, out var herd) || herd.Mind is not HerdMind mind) return;
        mind.LastFightMinute = world.Minute;
        bool predator = IsPredator(herd.Species);
        // old game: a kill set the pack's hunger to 0 before it ate, so it fed on nothing and left the carcass whole (judgment: it eats now).
        if (predator && killedOne) Become(world, mind, HerdState.Feeding);
        else if (mind.State == HerdState.Hunting) Become(world, mind, HerdState.Patrolling);
        mind.Fear = Math.Max(mind.Fear, Combat.FearAfter(own));
        bool scattered = own is CombatResult.Fled or CombatResult.AnimalDisengaged;
        if (scattered || (!predator && own != CombatResult.Victory)) StartFleeing(world, mind, tile);
        herd.Doing = null;
    }

    static void StartFleeing(World world, HerdMind mind, TileId from)
    {
        Become(world, mind, HerdState.Fleeing);
        mind.FleeFrom = from;
    }

    /// A wounded animal the player saw get away, now its own herd of one: it bolts from where it
    /// was hurt and lives on a scrap of land around it (old Herd.SplitOffWounded).
    internal static void Escaped(World world, AgentId id, TileId tile)
    {
        var herd = world.Agent(id);
        var mind = MindOf(herd);
        mind.Territory.Clear();
        mind.Territory.Add(tile);
        var m = Map.Of(world);
        foreach (var n in MapGen.Neighbors(tile).Where(n => m.InBounds(n) && Map.Blocked(m, tile, n) == null)) mind.Territory.Add(n);
        mind.TerritoryIndex = 0;
        StartFleeing(world, mind, tile);
        herd.Doing = null;
    }

    // ---- the mind: one minute's choice ----

    /// The herd's next activity (Minds.Decide): the old per-minute behavior update, as a choice.
    public static (Activity Activity, int Minutes) Decide(World world, Agent herd)
    {
        var mind = MindOf(herd);
        var behavior = BehaviorOf(herd.Species);
        if (mind.State == HerdState.Fleeing) return Flee(world, herd, mind, behavior);
        return behavior switch
        {
            HerdBehavior.Prey => DecidePrey(world, herd, mind),
            HerdBehavior.Pack => DecidePack(world, herd, mind),
            HerdBehavior.Solitary => DecideSolitary(world, herd, mind),
            _ => DecideScavenger(world, herd, mind),
        };
    }

    static (Activity, int) Rest(Agent herd) => (new Rest(herd.Id), 1);
    static (Activity, int) Wait(Agent herd) => (new Wait(herd.Id), 1);

    static (Activity, int) DecidePrey(World world, Agent herd, HerdMind mind)
    {
        switch (mind.State)
        {
            case HerdState.Resting:
                if (mind.Hunger > 0.5) Become(world, mind, HerdState.Grazing);
                return Rest(herd);
            case HerdState.Grazing:
                if (mind.Hunger < 0.2) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                // Drifts across its land, faster where it is eaten out.
                if (Roll(world, 0.01 + 0.01 * GrazedLevelHere(world, herd)) && PatrolStep(world, herd, mind) is { } walk) return walk;
                return (new Graze(herd.Id), 1);
            default:
                Become(world, mind, HerdState.Resting);
                return Rest(herd);
        }
    }

    static (Activity, int) DecidePack(World world, Agent herd, HerdMind mind)
    {
        long t = StateMinutes(world, mind);
        switch (mind.State)
        {
            case HerdState.Resting:
                if (mind.Hunger > 0.6) Become(world, mind, HerdState.Patrolling);
                return Rest(herd);
            case HerdState.Patrolling:
                // A frightened pack (it just lost a fight) leaves prey alone.
                if (mind.Hunger > 0.4 && mind.Fear <= 0.5 && PreyHere(world, herd) is Agent prey) return (new Hunt(herd.Id, prey.Id), 1);
                if (t > PackPatrolTimeoutMinutes)
                {
                    Become(world, mind, HerdState.Resting);
                    mind.TerritoryIndex = 0;
                    return Toward(world, herd, mind, mind.Territory[0]) ?? Rest(herd);
                }
                if (mind.Hunger < 0.3) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                if (t > 0 && t % PackPatrolEveryMinutes == 0)
                {
                    // Judgment (not in the old game): a hungry pack follows the scent of prey it can sense
                    // (old BaseDetectionRange) instead of walking its round, so packs and herds actually meet.
                    if (mind.Hunger > 0.6 && NearestPrey(world, herd) is TileId scent && Toward(world, herd, mind, scent) is { } stalk) return stalk;
                    if (PatrolStep(world, herd, mind) is { } walk) return walk;
                }
                return Wait(herd);
            case HerdState.Feeding:
                if (t > 60 || MeatHere(world, herd.Tile) <= 0) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                return (new Feed(herd.Id), 1);
            default:
                Become(world, mind, HerdState.Patrolling);
                return Wait(herd);
        }
    }

    static (Activity, int) DecideSolitary(World world, Agent herd, HerdMind mind)
    {
        long t = StateMinutes(world, mind);
        switch (mind.State)
        {
            case HerdState.Resting:
                if (mind.Hunger > 0.5) Become(world, mind, HerdState.Grazing);
                return Rest(herd);
            case HerdState.Grazing: // a bear forages, omnivore that it is
                if (mind.Hunger < 0.3) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                if (Roll(world, 0.01 + 0.01 * GrazedLevelHere(world, herd)) && PatrolStep(world, herd, mind) is { } walk) return walk;
                return (new Graze(herd.Id), 1);
            case HerdState.Patrolling:
                if (t > 60) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                if (Roll(world, 0.01 + 0.01 * GrazedLevelHere(world, herd)) && PatrolStep(world, herd, mind) is { } prowl) return prowl;
                return Wait(herd);
            case HerdState.Feeding:
                if (t > 90 || MeatHere(world, herd.Tile) <= 0) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                return (new Feed(herd.Id), 1);
            default:
                Become(world, mind, HerdState.Patrolling);
                return Wait(herd);
        }
    }

    static (Activity, int) DecideScavenger(World world, Agent herd, HerdMind mind)
    {
        long t = StateMinutes(world, mind);
        switch (mind.State)
        {
            case HerdState.Resting:
                if (mind.Hunger > 0.4) Become(world, mind, HerdState.Patrolling);
                return Rest(herd);
            case HerdState.Patrolling:
                if (MeatHere(world, herd.Tile) > 0) { Become(world, mind, HerdState.Feeding); return (new Feed(herd.Id), 1); }
                if (t % CarcassSearchEveryMinutes == 0 && NearestMeat(world, herd.Tile) is TileId food && Toward(world, herd, mind, food) is { } go) return go;
                if (mind.Hunger < 0.3 && t > 60) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                // ponytail: old hyenas also shadowed feeding predators and wolf packs; carcasses alone draw them here.
                if (Roll(world, 0.04) && PatrolStep(world, herd, mind) is { } walk) return walk;
                return Wait(herd);
            case HerdState.Feeding:
                if (HerdsAt(world, herd.Tile).Any(h => h.Id != herd.Id && h.Species is Species.Wolf or Species.Bear or Species.CaveBear or Species.SaberTooth))
                {
                    StartFleeing(world, mind, herd.Tile); // something bigger wants the kill
                    return Flee(world, herd, mind, HerdBehavior.Scavenger);
                }
                if (MeatHere(world, herd.Tile) <= 0 || t > 45 || mind.Hunger < 0.1) { Become(world, mind, HerdState.Patrolling); return Wait(herd); }
                return (new Feed(herd.Id), 1);
            default:
                Become(world, mind, HerdState.Patrolling);
                return Wait(herd);
        }
    }

    /// Bolts one tile away from the threat, then settles: prey and predators rest, scavengers prowl.
    /// old game: a fleeing pack or bear fled from the player's tile every minute until the map stopped it,
    /// so a pack that ran from a fight ran to the edge of the world (bug, fixed: everything flees one tile).
    static (Activity, int) Flee(World world, Agent herd, HerdMind mind, HerdBehavior behavior)
    {
        if (mind.FleeFrom is TileId from && herd.Tile == from && CanMove(herd) && FleeDirection(world, herd, from) is Direction away)
            return (new Travel(herd.Id, away), UntilArrived);
        mind.FleeFrom = null;
        Become(world, mind, behavior == HerdBehavior.Scavenger ? HerdState.Patrolling : HerdState.Resting);
        return Rest(herd);
    }

    static Direction? FleeDirection(World world, Agent herd, TileId threat)
    {
        var m = Map.Of(world);
        var options = Enum.GetValues<Direction>().Where(d => Map.Blocked(m, herd.Tile, Map.Step(herd.Tile, d)) == null).ToList();
        if (options.Count == 0) return null;
        int best = options.Max(d => MapGen.Manhattan(Map.Step(herd.Tile, d), threat));
        options = options.Where(d => MapGen.Manhattan(Map.Step(herd.Tile, d), threat) == best).ToList();
        return options[world.Rng("herds").NextInt(options.Count)];
    }

    static bool Roll(World world, double chance) => world.Rng("herds").NextDouble() < chance;

    static bool CanMove(Agent herd) => herd.Members.Min(b => Body.GetCapacities(b).Moving) > 0.1;

    /// One step on toward the tile of its land it is making for (old TryPatrolTerritory); once
    /// there, it makes for another anywhere on its land.
    /// old game: each step aimed at the next tile in flood-fill order and moved the aim on at once, so a
    /// herd shuffled among the first few tiles and a pack never left its den's doorstep (bug, fixed).
    static (Activity, int)? PatrolStep(World world, Agent herd, HerdMind mind)
    {
        if (mind.Territory.Count < 2) return null;
        if (herd.Tile == mind.Territory[mind.TerritoryIndex]) mind.TerritoryIndex = world.Rng("herds").NextInt(mind.Territory.Count);
        return Toward(world, herd, mind, mind.Territory[mind.TerritoryIndex]);
    }

    /// One step toward a tile: the shortest way through its own land when there is one (old
    /// Navigation.FindRoute, kept to the territory so it stays cheap), else whichever open
    /// neighbour is nearer. Null when already there or nothing is open.
    static (Activity, int)? Toward(World world, Agent herd, HerdMind mind, TileId target)
    {
        if (herd.Tile == target || !CanMove(herd)) return null;
        var m = Map.Of(world);
        var land = mind.Territory.ToHashSet();
        if (land.Contains(target) && land.Contains(herd.Tile) && FirstStep(m, herd.Tile, target, land) is Direction routed)
            return (new Travel(herd.Id, routed), UntilArrived);
        Direction? greedy = null;
        int bestDistance = MapGen.Manhattan(herd.Tile, target);
        foreach (var d in Enum.GetValues<Direction>())
        {
            var next = Map.Step(herd.Tile, d);
            if (Map.Blocked(m, herd.Tile, next) != null || MapGen.Manhattan(next, target) >= bestDistance) continue;
            greedy = d;
            bestDistance = MapGen.Manhattan(next, target);
        }
        return greedy is Direction g ? (new Travel(herd.Id, g), UntilArrived) : null;
    }

    static Direction? FirstStep(GeneratedMap m, TileId from, TileId to, HashSet<TileId> land)
    {
        var firstDir = new Dictionary<TileId, Direction>();
        var queue = new Queue<TileId>();
        queue.Enqueue(from);
        while (queue.TryDequeue(out var at))
            foreach (var d in Enum.GetValues<Direction>())
            {
                var next = Map.Step(at, d);
                if (next == from || firstDir.ContainsKey(next) || !land.Contains(next) || Map.Blocked(m, at, next) != null) continue;
                firstDir[next] = at == from ? d : firstDir[at];
                if (next == to) return firstDir[next];
                queue.Enqueue(next);
            }
        return null;
    }

    /// A prey herd on this tile, not already in a fight.
    static Agent? PreyHere(World world, Agent predator) =>
        HerdsAt(world, predator.Tile).FirstOrDefault(h => h.Id != predator.Id && !IsPredator(h.Species) && Sim.IsAlive(h)
            && Combat.EncounterOf(world, h.Id) == null);

    /// Old AnimalType.BaseDetectionRange, in tiles.
    public static int DetectionRangeTiles(Species s) => s is Species.Wolf or Species.SaberTooth ? 12 : 8;

    /// Where the nearest prey herd within the predator's senses stands, if any.
    static TileId? NearestPrey(World world, Agent predator)
    {
        int range = DetectionRangeTiles(predator.Species);
        return world.Agents.Values
            .Where(h => h.Mind is HerdMind && !IsPredator(h.Species) && MapGen.Manhattan(h.Tile, predator.Tile) <= range)
            .OrderBy(h => MapGen.Manhattan(h.Tile, predator.Tile)).Select(h => (TileId?)h.Tile).FirstOrDefault();
    }

    /// The nearest tile with meat on it within the scavenger's nose (old CarcassSearchRadius).
    static TileId? NearestMeat(World world, TileId from)
    {
        TileId? best = null;
        int bestDistance = int.MaxValue;
        for (int dx = -CarcassSearchRadius; dx <= CarcassSearchRadius; dx++)
            for (int dy = -CarcassSearchRadius; dy <= CarcassSearchRadius; dy++)
            {
                var t = new TileId(from.X + dx, from.Y + dy);
                int d = Math.Abs(dx) + Math.Abs(dy);
                if (d == 0 || d > CarcassSearchRadius || d >= bestDistance) continue;
                if (world.Tiles.TryGetValue(t, out var s) && s.Carcasses != null && MeatHere(world, t) > 0) { best = t; bestDistance = d; }
            }
        return best;
    }

    // ---- the player's hunt ----

    /// Distance a stalk opens at (old HUNT_START_DISTANCE_M).
    public const int HuntStartDistanceM = 34;

    /// The hunter stalks one member of a herd on its tile (old CombatOrchestrator.RunHunt): the
    /// hunter Engaged, the animal and a few herd mates Unaware (Alert if they are already running).
    /// The rounds are the hunter's Fight activities; the kill leaves a carcass to butcher.
    public static Encounter BeginHunt(World world, AgentId hunter, AgentId herd, int member, Gear? weapon = null)
    {
        var prey = world.Agent(herd);
        if (prey.Mind is not HerdMind mind) throw new ArgumentException($"{herd} is not a herd");
        var hunterAgent = world.Agent(hunter);
        if (hunterAgent.Tile != prey.Tile) throw new ArgumentException($"{herd} is on {prey.Tile}, not {hunterAgent.Tile}");
        var awareness = mind.State == HerdState.Fleeing ? Awareness.Alert : Awareness.Unaware;
        return Combat.Create(world, [new Fighter(hunter) { Weapon = weapon }], Combat.SendMembers(world, herd, member),
            prey.Tile, HuntStartDistanceM, Awareness.Engaged, awareness);
    }
}
