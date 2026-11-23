namespace Survival;

public enum HerdState { Resting, Grazing, Patrolling, Hunting, Feeding, Fleeing }

/// Carrion never hunts nor occupies a carcass, and follows whatever is dying or killing.
public enum HerdBehavior { Prey, Pack, Solitary, Scavenger, Carrion }

public enum Diet { Carnivore, Omnivore, Browser, Grazer }

/// Its hunger is its body's calorie store (Fauna.Hunger), never stored apart from it.
public sealed record HerdMind : Mind
{
    public const long Never = -1_000_000;
    public List<TileId> Territory { get; init; } = [];
    public int TerritoryIndex { get; set; }
    public HerdState State { get; set; }
    public long StateSinceMinute { get; set; }
    /// Learned fear 0-1: scales boldness down, and a frightened pack will not hunt.
    public double Fear { get; set; }
    public long LastEncounterMinute { get; set; } = Never;
    public TileId? FleeFrom { get; set; }
    /// Where a hungry pack is making for off its land: a far point when starving, or a lost scent (Stalk). Cleared when it eats.
    public TileId? Foray { get; set; }
    /// A meat-eater too long without starves (Fauna.Renew).
    public long LastFedMinute { get; set; }
    /// Familiarity 0-1 with each animal it knows, kept only by a closed-group species (Fauna.IsKin); null otherwise.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SortedDictionary<AgentId, double>? Familiar { get; set; }
}


public sealed record Graze(AgentId Who) : Activity(Who);
public sealed record Feed(AgentId Who) : Activity(Who);
/// A pack's hunt is fought headless at once; the player's stalk is fought round by round as Fight activities.
public sealed record Hunt(AgentId Who, AgentId Prey) : Activity(Who);
public sealed record Raid(AgentId Who) : Activity(Who);

/// Every animal is its own agent and decides one minute at a time, in id order; a herd is its kin on its tile.
public static partial class Fauna
{
    public static HerdBehavior BehaviorOf(Species s) => SpeciesTable.Of(s).Behavior;

    public static bool IsPredator(Species s) => BehaviorOf(s) is not (HerdBehavior.Prey or HerdBehavior.Carrion);

    public static bool IsPrey(Species s) => BehaviorOf(s) == HerdBehavior.Prey;

    /// What a pack hunts: prey, but not a mammoth. Packs share land with mammoths; let them hunt them and they
    /// eat the valley's mammoths out within two years.
    public static bool IsQuarry(Species s) => IsPrey(s) && !IsMegafauna(s);

    public static Diet DietOf(Species s) => SpeciesTable.Of(s).Diet;

    public static IReadOnlyList<Resource> Eats(Diet d) => Content.Tables.Diets[d];

    const double FearDecayPerMinute = 0.015;
    /// What a wild animal burns over its basal rate, day in, day out: field metabolism is 1.6-3 times
    /// basal (research §1; Nagy 1999). The low end, since fights and flight charge their own work.
    const double FieldMetabolismFactor = 1.6;
    const double GrazingRatePerKgPerHour = 0.0001;
    const int PackPatrolEveryMinutes = 30, PackPatrolTimeoutMinutes = 120;
    const double StarvingHunger = 0.8;
    const int StarvingPatrolEveryMinutes = 5, ForayRadiusTiles = 40;
    const int CarcassSearchRadius = 8, CarcassSearchEveryMinutes = 10;
    /// Long enough for any crossing: a crossing ends itself on arrival.
    const int UntilArrived = int.MaxValue;

    public static HerdMind NewMind(World world, AgentId id, Species species, List<TileId> territory)
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
            LastFedMinute = world.Minute,
        };
    }

    public static HerdMind MindOf(Agent herd) =>
        herd.Mind as HerdMind ?? throw new InvalidOperationException($"{herd.Id} is not a herd");

    public static IEnumerable<Agent> AnimalsAt(World world, TileId tile) =>
        Nearby.At(world, tile).Select(id => world.Agents[id]).Where(a => a.Mind is HerdMind);

    static void Become(World world, HerdMind mind, HerdState state)
    {
        mind.State = state;
        mind.StateSinceMinute = world.Minute;
    }

    static long StateMinutes(World world, HerdMind mind) => world.Minute - mind.StateSinceMinute;
    static double MassKg(Agent herd) => herd.Members.Sum(b => b.WeightKg);

    /// 0 fed to 1 starving: how far its calorie stores are from full. Derived, never stored, so every minute
    /// lived and every burst of a fight cost a herd food, fleeing and watching included.
    public static double Hunger(Agent herd)
    {
        double held = 0, capacity = 0;
        foreach (var b in herd.Members)
        {
            if (!Body.IsAlive(b)) continue; // the dead in a fight stay listed until its aftermath
            held += b.CaloriesKcal;
            capacity += Body.CaloriesCapacityKcal(b);
        }
        return capacity <= 0 ? 0 : Math.Clamp(1 - held / capacity, 0, 1);
    }

    public static double BurnKcalPerMinute(BodyState body) => Body.MetabolismKcalPerDay(body, FieldMetabolismFactor) / 1440;

    internal static void Burn(BodyState body) => Body.SpendKcal(body, BurnKcalPerMinute(body));

    /// Its store's room plus the fat lost below its kind's build: a starved wolf gorges, and the surplus goes back on as fat.
    static double RoomKcal(Agent herd, BodyState b) =>
        Math.Max(0, Body.CaloriesCapacityKcal(b) - b.CaloriesKcal) + Math.Max(0, SpeciesTable.Of(herd.Species).FatPct * b.WeightKg - b.FatKg) * Body.KcalPerKgFat;

    static double RoomKcal(Agent herd) => herd.Members.Where(Body.IsAlive).Sum(b => RoomKcal(herd, b));

    static void Nourish(Agent herd, double kcal)
    {
        double room = RoomKcal(herd);
        if (kcal <= 0 || room <= 0) return;
        kcal = Math.Min(kcal, room);
        foreach (var b in herd.Members.Where(Body.IsAlive)) Body.Eat(b, kcal * RoomKcal(herd, b) / room);
    }


    /// Every hurt tissue and the blood mend at this rate, whole in about three and a half days.
    public const double MendPctPerMinute = 0.0002;

    /// While any member is in a fight the dead stay listed (units name members by index): the aftermath takes them.
    public static string? Tick(World world, Agent herd, List<WorldEvent> events)
    {
        var mind = MindOf(herd);
        mind.Fear = Math.Max(0, mind.Fear - FearDecayPerMinute);
        bool anyDied = false, anyHurt = false;
        foreach (var body in herd.Members)
        {
            // A headless fight already lived this minute of it.
            if (world.Minute <= body.WoundsLivedThroughMinute) continue;
            body.WoundsLivedThroughMinute = world.Minute;
            if (body.Effects.Count > 0 || body.SprintSpentLevel > 0) Body.TickWounds(body, world.Rng("body", herd.Id));
            if (Body.IsAlive(body)) Burn(body);
            // Every animal mends, predator or prey, tissue included: a lame one walks again.
            if (!Hurt(body)) { body.WoundedBy = null; continue; }
            anyHurt = true;
            if (Body.IsAlive(body)) Mend(body);
            else anyDied = true;
        }
        if (!anyHurt || Combat.EncounterOf(world, herd.Id) != null) return null;
        if (anyDied)
        {
            foreach (var body in herd.Members.Where(b => !Body.IsAlive(b)).ToList())
            {
                var cause = body.WoundedBy is Cause w ? new Cause(w.Who, $"wounds from {w.What}") : new Cause(null, "its wounds");
                Combat.LeaveCarcass(world, herd.Tile, herd.Species, body, cause);
                events.Add(new Killed(world.Minute, herd.Id, herd.Species, body.WeightKg, herd.Tile, By: cause.Who));
                herd.Members.Remove(body);
            }
            if (herd.Members.Count == 0)
            {
                Agents.Remove(world, herd.Id);
                return "died";
            }
        }
        return null;
    }

    static bool Mobile(BodyState body) => Body.GetCapacities(body).Moving > 0.1;

    static bool Hurt(BodyState body) => !Body.IsMended(body);

    internal static void Mend(BodyState body)
    {
        if (body.BloodPct < 1) body.BloodPct = Math.Min(1, body.BloodPct + MendPctPerMinute);
        foreach (var region in body.Regions)
            foreach (var t in region.Layers.Concat(region.Organs.Where(o => o.Condition > 0)))
                if (t.Condition < 1) t.Condition = Math.Min(1, t.Condition + MendPctPerMinute);
    }


    public static bool Handles(Activity a) => a is Graze or Feed or Hunt or Raid;

    public static Refusal? Refusal(World world, Activity activity)
    {
        var agent = world.Agent(activity.Who);
        if (activity is Hunt hunt && agent.Mind is PersonMind) return PersonHuntRefusal(world, agent, hunt);
        if (agent.Mind is not HerdMind) return new("only a herd does that");
        if (activity is Graze) return GrazeRefusal(world, agent);
        if (activity is Feed) return CanFeed(world, agent) ? null : new("nothing here it can reach to eat");
        if (activity is Raid) return RaidRefusal(world, agent);
        if (activity is not Hunt h) return null;
        if (h.Prey == h.Who || !world.Agents.TryGetValue(h.Prey, out var prey) || prey.Tile != agent.Tile || !Sim.IsAlive(prey))
            return new("no such prey here");
        if (Access.Refusal(world, h.Who, LocationFeatures.OfAgent(world, h.Prey), confronting: true) is { } barred) return barred;
        if (Combat.EncounterOf(world, h.Prey) != null || Combat.EncounterOf(world, h.Who) != null) return new("already fighting");
        return null;
    }

    /// A hunt is fought here, headless, and returns its rounds as the activity's minutes.
    internal static int? Begin(World world, Activity activity, List<WorldEvent> events) => activity is not Hunt h ? null
        : world.Agent(h.Who).Mind is PersonMind ? PersonHunt(world, h, events)
        : PackHunt(world, world.Agent(h.Who), world.Agent(h.Prey), events);

    static Refusal? PersonHuntRefusal(World world, Agent hunter, Hunt h)
    {
        if (!world.Agents.TryGetValue(h.Prey, out var prey) || prey.Mind is not HerdMind || prey.Tile != hunter.Tile || !Sim.IsAlive(prey))
            return new("No such herd here.");
        if (Combat.EncounterOf(world, h.Prey) != null) return new("They are already fighting.");
        return Access.Refusal(world, h.Who, LocationFeatures.OfAgent(world, h.Prey), confronting: true);
    }

    /// The player's stalk lasts this minute of setting out (their Fight rounds are the stalk); anyone else's is fought headless.
    static int PersonHunt(World world, Hunt h, List<WorldEvent> events)
    {
        var hunter = world.Agent(h.Who);
        var enc = BeginHunt(world, h.Who, Weakest(world, world.Agent(h.Prey)).Id, Combat.BestWeapon(hunter), events);
        if (h.Who == AgentId.Player) return 1;
        Combat.ResolveHeadless(world, enc, events);
        return Math.Max(1, enc.Rounds);
    }

    /// The animal of this one's herd a hunter singles out: the slowest and least alive, the lower id on a tie.
    static Agent Weakest(World world, Agent prey) => Combat.Side(world, prey)
        .OrderBy(a => Body.Speed(a.Body) * Body.Vitality(a.Body)).ThenBy(a => Body.Vitality(a.Body)).ThenBy(a => a.Id).First();

    internal static string? Minute(World world, Agent herd, Activity activity, List<WorldEvent> events)
    {
        if (activity is Graze) GrazeMinute(world, herd);
        else if (activity is Feed) FeedMinute(world, herd);
        else if (activity is Raid) RaidMinute(world, herd);
        return null;
    }

    // An eaten-out tile still feeds in full. Research §5.5 would scale the intake by the forage left,
    // which leaves herds wandering far more.
    static void GrazeMinute(World world, Agent herd)
    {
        var mind = MindOf(herd);
        double intake = SpeciesTable.Of(herd.Species).GrazeKcalPerMinute;
        bool ate = false;
        foreach (var b in herd.Members.Where(Body.IsAlive))
        {
            double kcal = Math.Min(intake, RoomKcal(herd, b));
            if (kcal <= 0) continue;
            Body.Eat(b, kcal);
            ate = true;
        }
        if (ate) mind.LastFedMinute = world.Minute; // Renewal's starvation reads it: only food counts
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

    /// Where nothing grows, grazing is only standing about, which nothing bars.
    static Refusal? GrazeRefusal(World world, Agent herd) =>
        ForageHere(world, herd.Tile) is (PoolId id, _) ? Access.Refusal(world, herd.Id, new LocationFeature(LocationFeatureKind.Pool, id.Tile, id.Index)) : null;

    public static double GrazedLevelHere(World world, Agent herd)
    {
        var diet = DietOf(herd.Species);
        if (diet == Diet.Carnivore) return 0;
        if (ForageHere(world, herd.Tile) is not (PoolId id, ForagePool pool)) return 1.0;
        // Read by every grazing animal every minute, so counted, not listed.
        double sum = 0;
        int count = 0;
        foreach (var r in Eats(diet))
        {
            bool grows = false;
            foreach (var i in pool.Items) grows |= i.Resource == r;
            if (!grows) continue;
            sum += Pools.GrazedLevel(world, id, r);
            count++;
        }
        return count == 0 ? 1.0 : sum / count;
    }

    public static double MeatHere(World world, TileId tile) =>
        world.Tiles.GetValueOrDefault(tile)?.Carcasses?.Sum(c => Remains.MeatKg(world, tile, c)) ?? 0;

    /// No faster than 0.1 kg a minute a head; the carrion flock picks at a dead person's body once the carcasses are bare.
    static void FeedMinute(World world, Agent herd)
    {
        var mind = MindOf(herd);
        double want = Math.Min(RoomKcal(herd) / MeatKcalPerKg, 0.1 * herd.Members.Count), eaten = 0;
        foreach (var c in Edible(world, herd))
            if (want > eaten) eaten += Carcasses.EatMeat(c, want - eaten);
        if (want > eaten && BehaviorOf(herd.Species) == HerdBehavior.Carrion)
            foreach (var body in PickableBodies(world, herd))
                if (want > eaten) eaten += Remains.Pick(world, body, want - eaten);
        Nourish(herd, eaten * MeatKcalPerKg);
        if (eaten > 0)
        {
            mind.Foray = null;
            mind.LastFedMinute = world.Minute;
        }
    }

    static double MeatKcalPerKg => Items.Resources[Resource.RawMeat].KcalPerKg;

    static IEnumerable<Carcass> Edible(World world, Agent herd) =>
        (world.Tiles.GetValueOrDefault(herd.Tile)?.Carcasses ?? []).Where(c => Remains.MeatKg(world, herd.Tile, c) > 0
            && Access.Refusal(world, herd.Id, new LocationFeature(LocationFeatureKind.Carcass, herd.Tile, c.Id)) == null);

    static IEnumerable<AgentId> PickableBodies(World world, Agent herd) =>
        Nearby.At(world, herd.Tile).Where(id => world.Agents[id] is { Mind: PersonMind } d && id != AgentId.Player && !Sim.IsAlive(d)
            && Remains.StageOf(world, d) == RemainsStage.Flesh && Access.Refusal(world, herd.Id, LocationFeatures.OfAgent(world, id)) == null);

    static bool CanFeed(World world, Agent herd) =>
        Edible(world, herd).Any() || BehaviorOf(herd.Species) == HerdBehavior.Carrion && PickableBodies(world, herd).Any();


    /// One animal, as wolves cut one out of a herd: against three, a pack that meets bison is wiped out in its first
    /// week. Vigilance decides the opening: noticed prey start alert at range, surprised prey are ambushed.
    static int PackHunt(World world, Agent pack, Agent prey, List<WorldEvent> events)
    {
        Become(world, MindOf(pack), HerdState.Hunting);
        if (prey.Mind is PersonMind) return ComeFor(world, pack, prey, events);
        var targets = new List<Fighter> { new(prey.Id) };
        var hunters = Combat.Side(world, pack).Take(MaxHunters).Select(a => new Fighter(a.Id)).ToList();
        bool noticed = PreyNotices(world, pack, prey);
        var enc = Combat.Create(world, hunters, targets, pack.Tile, noticed ? 15 : 5, Awareness.Engaged, noticed ? Awareness.Alert : Awareness.Unaware, events: events);
        Combat.ResolveHeadless(world, enc, events);
        // Escaping prey is not chased: the pack patrols on.
        return Math.Max(1, enc.Rounds);
    }

    const int MaxHunters = 6;

    /// An approach opens from the grid's edge, whatever distance it is given.
    const int AmbushFromM = 5;

    /// One who saw the pack coming meets it ready at the grid's edge; anyone else is ambushed.
    static int ComeFor(World world, Agent pack, Agent person, List<WorldEvent> events)
    {
        double delta = Combat.BoldnessToward(world, pack, person) - 0.5;
        var hunters = Combat.Side(world, pack).Take(MaxHunters).Select(a => new Fighter(a.Id) { BoldnessDelta = delta }).ToList();
        bool ready = SawItComing(world, person, pack);
        var enc = Combat.Create(world, [new Fighter(person.Id) { Weapon = Combat.BestWeapon(person) }], hunters, person.Tile, AmbushFromM,
            ready ? Awareness.Engaged : Awareness.Unaware, Awareness.Engaged, ready ? Opening.Approach : Opening.Ambush, events);
        if (person.Id == AgentId.Player) return 1;
        Combat.ResolveHeadless(world, enc, events);
        return Math.Max(1, enc.Rounds);
    }

    static bool SawItComing(World world, Agent person, Agent herd) =>
        Sim.Looks(person, Sim.ActivityOf(person))
        && Memories.LastSeenMinute(person, herd.Id) is long seen && world.Minute - seen <= Memories.GlanceMinutes;

    /// More eyes in its herd notice more; a bigger pack is harder to hide (both Combat.Side).
    public static bool PreyNotices(World world, Agent predator, Agent prey)
    {
        var preyMind = MindOf(prey);
        double vigilance = 0.5 + Math.Log(Combat.Side(world, prey).Count + 1) * 0.25;
        vigilance *= preyMind.State switch
        {
            HerdState.Resting => 0.6,
            HerdState.Fleeing => 1.3,
            _ => 1.0,
        };
        if (preyMind.State == HerdState.Resting && StateMinutes(world, preyMind) < 30) vigilance *= 1.4; // just stopped running
        double stealth = 0.5 - Combat.Side(world, predator).Count * 0.02, hunger = Hunger(predator);
        if (hunger > 0.7) stealth += 0.15;
        if (hunger > 0.9) stealth += 0.15;
        stealth *= predator.Species switch
        {
            Species.Wolf => 1.2,
            Species.Bear or Species.CaveBear => 0.7,
            Species.SaberTooth => 1.3,
            Species.Hyena => 0.8,
            _ => 1.0,
        };
        stealth = Math.Max(0.2, stealth);
        var rng = world.Rng("herds", prey.Id);
        return rng.NextDouble() * vigilance > rng.NextDouble() * stealth;
    }

    /// Fear by how it went; a predator that killed feeds; prey that did not win, and anything that scattered, runs.
    internal static void AfterEncounter(World world, AgentId id, CombatResult own, bool killedOne, TileId tile)
    {
        if (!world.Agents.TryGetValue(id, out var herd) || herd.Mind is not HerdMind mind) return;
        mind.LastEncounterMinute = world.Minute;
        bool predator = IsPredator(herd.Species);
        if (predator && killedOne)
        {
            Become(world, mind, HerdState.Feeding);
            mind.Foray = null;
        }
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

    /// A wounded animal the player saw get away bolts alone, on a scrap of land around where it was hurt.
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


    /// A predator held off a tile with people (Combat.WouldEnter) waits instead, and tries again when it next walks that way.
    public static (Activity Activity, int Minutes) Decide(World world, Agent herd, List<WorldEvent> events)
    {
        var mind = MindOf(herd);
        var behavior = BehaviorOf(herd.Species);
        var choice = mind.State == HerdState.Fleeing ? Flee(world, herd, mind, behavior, events) : Cohere(world, herd, mind) ?? behavior switch
        {
            HerdBehavior.Prey => DecidePrey(world, herd, mind),
            HerdBehavior.Pack => DecidePack(world, herd, mind, events),
            HerdBehavior.Solitary => DecideSolitary(world, herd, mind),
            HerdBehavior.Scavenger => DecideScavenger(world, herd, mind, events),
            HerdBehavior.Carrion => DecideCarrion(world, herd, mind),
        };
        if (choice.Item1 is not Travel t || mind.State == HerdState.Fleeing || !HeldOff(world, herd, Map.Step(herd.Tile, t.Dir), events)) return choice;
        mind.Foray = null; // a starving pack held off one way ranges another, rather than waiting on a fire that is kept fed
        return Wait(herd);
    }

    static bool HeldOff(World world, Agent herd, TileId to, List<WorldEvent> events)
    {
        if (!IsPredator(herd.Species) || !Nearby.At(world, to).Any(id => world.Agents[id] is { Mind: PersonMind } a && Sim.InTheWorld(world, a))) return false;
        Fire.CatchUpOn(world, to, events);
        return !Combat.WouldEnter(world, herd, to);
    }

    static (Activity, int) Rest(Agent herd) => (new Rest(herd.Id), 1);
    static (Activity, int) Wait(Agent herd) => (new Wait(herd.Id), 1);

    static (Activity, int) DecidePrey(World world, Agent herd, HerdMind mind)
    {
        double hunger = Hunger(herd);
        switch (mind.State)
        {
            case HerdState.Resting:
                if (hunger > 0.5) Become(world, mind, HerdState.Grazing);
                return Rest(herd);
            case HerdState.Grazing:
                if (hunger < 0.2) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                if (Drift(world, herd, mind, 0.01 + 0.01 * GrazedLevelHere(world, herd)) is { } walk) return walk;
                return GrazeRefusal(world, herd) == null ? (new Graze(herd.Id), 1) : Wait(herd);
            default:
                Become(world, mind, HerdState.Resting);
                return Rest(herd);
        }
    }

    static (Activity, int) DecidePack(World world, Agent herd, HerdMind mind, List<WorldEvent> events)
    {
        double hunger = Hunger(herd);
        long t = StateMinutes(world, mind);
        switch (mind.State)
        {
            case HerdState.Resting:
                if (hunger > 0.6) Become(world, mind, HerdState.Patrolling);
                return Rest(herd);
            case HerdState.Patrolling:
                // A pack goes back to the meat on its tile before it hunts again, so a kill feeds it for days.
                if (CanFeed(world, herd)) { Become(world, mind, HerdState.Feeding); return (new Feed(herd.Id), 1); }
                if (hunger > 0.4 && mind.Fear <= 0.5 && !SideFoughtThisMinute(world, herd) && PreyHere(world, herd, events) is Agent prey)
                    return (new Hunt(herd.Id, prey.Id), 1);
                if (t > PackPatrolTimeoutMinutes && hunger <= StarvingHunger)
                {
                    Become(world, mind, HerdState.Resting);
                    mind.TerritoryIndex = 0;
                    return TowardNearest(world, herd, [mind.Territory[0]]) ?? Rest(herd);
                }
                if (hunger < 0.3) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                if (t > 0 && t % (hunger > StarvingHunger ? StarvingPatrolEveryMinutes : PackPatrolEveryMinutes) == 0)
                {
                    // A hungry pack follows a scent instead of walking its round, so packs and herds actually meet.
                    if (hunger > 0.6 && Stalk(world, herd, mind, events) is { } stalk) return stalk;
                    if ((hunger > StarvingHunger || (hunger > 0.6 && mind.Foray != null)) && Foray(world, herd, mind) is { } range) return range;
                    if (PatrolStep(world, herd, mind) is { } walk) return walk;
                }
                return Wait(herd);
            case HerdState.Feeding:
                if (t > 60 || !CanFeed(world, herd)) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                return (new Feed(herd.Id), 1);
            default:
                Become(world, mind, HerdState.Patrolling);
                return Wait(herd);
        }
    }

    /// A starving pack ranges toward far points within ForayRadiusTiles of its den until it picks up a scent:
    /// kept to their own few dozen tiles, packs starve beside a valley full of prey.
    static (Activity, int)? Foray(World world, Agent herd, HerdMind mind)
    {
        for (int tries = 0; tries < 2; tries++)
        {
            if (mind.Foray is not TileId target || target == herd.Tile)
            {
                // A lost scent followed to its end: only a starving pack ranges on from there.
                if (Hunger(herd) <= StarvingHunger) { mind.Foray = null; return null; }
                mind.Foray = ForayTarget(world, herd, mind.Territory[0]);
                if (mind.Foray == null) return null;
                target = mind.Foray.Value;
            }
            if (TowardNearest(world, herd, [target]) is { } step) return step;
            if (!CanMove(herd)) return null;
            mind.Foray = null;
        }
        return null;
    }

    static TileId? ForayTarget(World world, Agent herd, TileId den)
    {
        var m = Map.Of(world);
        var rng = world.Rng("herds", herd.Id);
        for (int i = 0; i < 8; i++)
        {
            var t = new TileId(den.X + rng.NextInt(2 * ForayRadiusTiles + 1) - ForayRadiusTiles, den.Y + rng.NextInt(2 * ForayRadiusTiles + 1) - ForayRadiusTiles);
            if (m.InBounds(t) && m.TerrainAt(t).IsPassable() && m.CaveAt(t) == Cave.None && t != den) return t;
        }
        return null;
    }

    static (Activity, int) DecideSolitary(World world, Agent herd, HerdMind mind)
    {
        double hunger = Hunger(herd);
        long t = StateMinutes(world, mind);
        switch (mind.State)
        {
            case HerdState.Resting:
                if (hunger > 0.5) Become(world, mind, HerdState.Grazing);
                return Rest(herd);
            case HerdState.Grazing:
                if (hunger < 0.3) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                if (Drift(world, herd, mind, 0.01 + 0.01 * GrazedLevelHere(world, herd)) is { } walk) return walk;
                return GrazeRefusal(world, herd) == null ? (new Graze(herd.Id), 1) : Wait(herd);
            case HerdState.Patrolling:
                if (t > 60) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                if (Drift(world, herd, mind, 0.01 + 0.01 * GrazedLevelHere(world, herd)) is { } prowl) return prowl;
                return Wait(herd);
            case HerdState.Feeding:
                if (t > 90 || !CanFeed(world, herd)) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                return (new Feed(herd.Id), 1);
            default:
                Become(world, mind, HerdState.Patrolling);
                return Wait(herd);
        }
    }

    static (Activity, int) DecideScavenger(World world, Agent herd, HerdMind mind, List<WorldEvent> events)
    {
        double hunger = Hunger(herd);
        long t = StateMinutes(world, mind);
        switch (mind.State)
        {
            case HerdState.Resting:
                if (hunger > 0.4) Become(world, mind, HerdState.Patrolling);
                return Rest(herd);
            case HerdState.Patrolling:
                if (CanFeed(world, herd)) { Become(world, mind, HerdState.Feeding); return (new Feed(herd.Id), 1); }
                if (t % CarcassSearchEveryMinutes == 0 && TowardNearest(world, herd, MeatSensed(world, herd.Tile)) is { } go) return go;
                if (hunger < 0.3 && t > 60) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                if (Drift(world, herd, mind, 0.04) is { } walk) return walk;
                return Wait(herd);
            case HerdState.Feeding:
                if (AnimalsAt(world, herd.Tile).Any(h => h.Id != herd.Id && h.Species is Species.Wolf or Species.Bear or Species.CaveBear or Species.SaberTooth))
                {
                    StartFleeing(world, mind, herd.Tile); // something bigger wants the kill
                    return Flee(world, herd, mind, HerdBehavior.Scavenger, events);
                }
                // Sated, it lies up beside what is left rather than patrolling straight back onto it to feed again.
                if (hunger < 0.1) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                if (!CanFeed(world, herd) || t > 45) { Become(world, mind, HerdState.Patrolling); return Wait(herd); }
                return (new Feed(herd.Id), 1);
            default:
                Become(world, mind, HerdState.Patrolling);
                return Wait(herd);
        }
    }

    /// One tile only, the way its kin are already running: fleeing every minute from the threat's tile runs a herd to the map's edge.
    static (Activity, int) Flee(World world, Agent herd, HerdMind mind, HerdBehavior behavior, List<WorldEvent> events)
    {
        if (mind.FleeFrom is TileId from && herd.Tile == from && CanMove(herd) && (Following(world, herd)?.Way ?? FleeDirection(world, herd, from, events)) is Direction away)
            return (new Travel(herd.Id, away), UntilArrived);
        mind.FleeFrom = null;
        Become(world, mind, behavior is HerdBehavior.Scavenger or HerdBehavior.Carrion ? HerdState.Patrolling : HerdState.Resting);
        return Rest(herd);
    }

    static Direction? FleeDirection(World world, Agent herd, TileId threat, List<WorldEvent> events)
    {
        var options = Enum.GetValues<Direction>().Where(d => Traversal.Blocked(world, herd, herd.Tile, Map.Step(herd.Tile, d)) == null
            && !HeldOff(world, herd, Map.Step(herd.Tile, d), events)).ToList();
        if (options.Count == 0) return null;
        int best = options.Max(d => MapGen.Manhattan(Map.Step(herd.Tile, d), threat));
        options = options.Where(d => MapGen.Manhattan(Map.Step(herd.Tile, d), threat) == best).ToList();
        return options[world.Rng("herds", herd.Id).NextInt(options.Count)];
    }

    static bool Roll(World world, Agent herd, double chance) => world.Rng("herds", herd.Id).NextDouble() < chance;

    static bool CanMove(Agent herd) => Mobile(herd.Body);

    /// Aiming at a random tile of its land, not the next in flood-fill order, which keeps a herd shuffling among the first few.
    static (Activity, int)? PatrolStep(World world, Agent herd, HerdMind mind)
    {
        if (mind.Territory.Count < 2) return null;
        if (herd.Tile == mind.Territory[mind.TerritoryIndex]) mind.TerritoryIndex = world.Rng("herds", herd.Id).NextInt(mind.Territory.Count);
        var walk = TowardNearest(world, herd, [mind.Territory[mind.TerritoryIndex]]);
        if (walk != null || !CanMove(herd)) return walk;
        mind.TerritoryIndex = world.Rng("herds", herd.Id).NextInt(mind.Territory.Count);
        return null;
    }

    /// Quickest to reach (Trip.Routes), not nearest as the crow flies; ties go to the lower tile.
    static (Activity, int)? TowardNearest(World world, Agent herd, HashSet<TileId> goals) =>
        NearestRoute(world, herd, goals) is { } r ? (new Travel(herd.Id, r.First), UntilArrived) : null;

    /// A hungry pack goes after what it senses, keeping an animal's scent as its foray; a person is weighed afresh.
    static (Activity, int)? Stalk(World world, Agent herd, HerdMind mind, List<WorldEvent> events)
    {
        if (NearestRoute(world, herd, PreySensed(world, herd, events)) is not { } r) return null;
        if (Nearby.At(world, r.Goal).Any(id => world.Agents[id] is { Mind: HerdMind } a && a.Tile == r.Goal && IsQuarry(a.Species))) mind.Foray = r.Goal;
        return (new Travel(herd.Id, r.First), UntilArrived);
    }

    static (Direction First, TileId Goal)? NearestRoute(World world, Agent herd, HashSet<TileId> goals)
    {
        if (goals.Count == 0 || goals.Contains(herd.Tile) || !CanMove(herd)) return null;
        TileId? reached = null;
        var routes = Trip.Routes(world, herd.Id, MaxRouteMinutes, until: (t, _) =>
        {
            if (!goals.Contains(t)) return false;
            reached = t;
            return true;
        });
        return reached is TileId goal ? (routes.FirstStep(goal)!.Value, goal) : null;
    }

    /// How far a route looks before it gives up: a goal walled off from the herd is not worth the whole map.
    const int MaxRouteMinutes = 1440;

    /// The one it has the most resolve toward, the first in id order on a tie.
    static Agent? PreyHere(World world, Agent predator, List<WorldEvent> events)
    {
        Fire.CatchUpOn(world, predator.Tile, events); // every resolve here reads the tile's flames
        Agent? best = null;
        double bestResolve = double.NegativeInfinity;
        foreach (var id in Nearby.At(world, predator.Tile))
        {
            var a = world.Agents[id];
            if (a.Id == predator.Id || !Sim.IsAlive(a) || Combat.EncounterOf(world, a.Id) != null) continue;
            double resolve;
            if (a.Mind is HerdMind && IsQuarry(a.Species)) resolve = Combat.ResolveToward(world, predator, a);
            else if (WouldComeFor(world, predator, a, events) is double r) resolve = r;
            else continue;
            if (resolve > bestResolve) (best, bestResolve) = (a, resolve);
        }
        return best;
    }

    static double? WouldComeFor(World world, Agent predator, Agent a, List<WorldEvent> events)
    {
        if (a.Mind is not PersonMind || !Sim.InTheWorld(world, a)) return null;
        Fire.CatchUpOn(world, a.Tile, events);
        double resolve = Combat.ResolveToward(world, predator, a);
        return resolve >= Combat.PressingEnterResolve ? resolve : null;
    }

    public static int DetectionRangeTiles(Species s) => s is Species.Wolf or Species.SaberTooth ? 12 : 8;

    static HashSet<TileId> PreySensed(World world, Agent predator, List<WorldEvent> events)
    {
        return Nearby.Within(world, predator.Tile, DetectionRangeTiles(predator.Species))
            .Where(h => h.Mind is HerdMind ? IsQuarry(h.Species) : Combat.EncounterOf(world, h.Id) == null && WouldComeFor(world, predator, h, events) != null)
            .Select(h => h.Tile).ToHashSet();
    }

    static HashSet<TileId> MeatSensed(World world, TileId from, int radius = CarcassSearchRadius)
    {
        var found = new HashSet<TileId>();
        foreach (var t in Carcasses.TilesWith(world))
        {
            int d = MapGen.Manhattan(t, from);
            if (d > 0 && d <= radius && MeatHere(world, t) > 0) found.Add(t);
        }
        return found;
    }


    public const int HuntStartDistanceM = 34;

    /// The hunter Engaged, the animal and a few of its herd Unaware (Alert if already running).
    public static Encounter BeginHunt(World world, AgentId hunter, AgentId animal, Gear? weapon = null, List<WorldEvent>? events = null)
    {
        var prey = world.Agent(animal);
        if (prey.Mind is not HerdMind mind) throw new ArgumentException($"{animal} is not an animal");
        var hunterAgent = world.Agent(hunter);
        if (hunterAgent.Tile != prey.Tile) throw new ArgumentException($"{animal} is on {prey.Tile}, not {hunterAgent.Tile}");
        var awareness = mind.State == HerdState.Fleeing ? Awareness.Alert : Awareness.Unaware;
        double watchfulness = SightingDetectionFactor(world, hunter, animal);
        var animals = Combat.SendSide(world, animal).Select(f => f with { DetectionFactor = watchfulness }).ToList();
        return Combat.Create(world, [new Fighter(hunter) { Weapon = weapon }], animals,
            prey.Tile, HuntStartDistanceM, Awareness.Engaged, awareness, events: events);
    }

    /// Its state stands for the activity and its learned fear for nervousness; one factor of the matching pool is picked at random.
    public static double SightingDetectionFactor(World world, AgentId hunter, AgentId animal)
    {
        var agent = world.Agent(animal);
        var mind = (HerdMind)agent.Mind;
        var pool = Combat.WoundLevel(agent.Body) > 0.05 ? Sightings.Wounded
            : mind.State == HerdState.Fleeing || mind.Fear > 0.7 ? Sightings.Alert
            : mind.State switch
            {
                HerdState.Grazing or HerdState.Feeding => Sightings.Grazing,
                HerdState.Resting => Sightings.Resting,
                _ => Sightings.Moving,
            };
        return pool[world.Rng("combat", hunter).NextInt(pool.Count)];
    }

    static readonly SightingPools Sightings = Content.Tables.Sightings;
}

public sealed record SightingPools(IReadOnlyList<double> Grazing, IReadOnlyList<double> Resting, IReadOnlyList<double> Wounded,
    IReadOnlyList<double> Alert, IReadOnlyList<double> Moving);
