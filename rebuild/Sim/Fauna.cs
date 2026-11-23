namespace Survival;

/// What an animal is doing. Hunting lasts only as long as the fight.
public enum HerdState { Resting, Grazing, Patrolling, Hunting, Feeding, Fleeing }

/// How a species' herd behaves. Carrion is the folded scavenger flock (Species.Raven): it never
/// hunts and never occupies a carcass, and follows whatever is dying or killing.
public enum HerdBehavior { Prey, Pack, Solitary, Scavenger, Carrion }

/// What a herd grazes. Carnivores graze nothing.
public enum Diet { Carnivore, Omnivore, Browser, Grazer }

/// An animal's mind: its land, what it is doing and since when, its learned fear, and whom it knows.
/// Its hunger is its body's calorie store (Fauna.Hunger), never stored apart from it.
public sealed record HerdMind : Mind
{
    public const long Never = -1_000_000;
    /// Tiles it patrols and grazes, its den (or birthplace) first.
    public List<TileId> Territory { get; init; } = [];
    public int TerritoryIndex { get; set; }
    public HerdState State { get; set; }
    /// When it entered State; how long it has been in it is derived from this.
    public long StateSinceMinute { get; set; }
    /// Learned fear 0-1: scales boldness down, and a frightened pack will not hunt.
    public double Fear { get; set; }
    public long LastEncounterMinute { get; set; } = Never;
    /// Where the threat was, while fleeing.
    public TileId? FleeFrom { get; set; }
    /// Where a starving pack is ranging to, off its land (Fauna.Foray); cleared when it eats.
    public TileId? Foray { get; set; }
    /// When it last ate anything: a meat-eater too long without starves (Fauna.Renew).
    public long LastFedMinute { get; set; }
    /// How well it knows each other animal it knows at all, 0-1: sparse, and kept only by a closed-group
    /// species (Group.Closed), whose kin are the animals it knows (Fauna.IsKin). Null for every other.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SortedDictionary<AgentId, double>? Familiar { get; set; }
}

// Herd activities. Travel, Rest and Wait are the shared ones.

/// Eats what the tile grows, for one minute.
public sealed record Graze(AgentId Who) : Activity(Who);
/// Eats from a carcass on the tile, for one minute.
public sealed record Feed(AgentId Who) : Activity(Who);
/// Goes for a herd or a person on the same tile. A pack's hunt is fought headless at once (the player fights
/// theirs round by round); a person stalks the herd's weakest member with the best weapon in the pack: the
/// player's rounds are then Fight activities, and anyone else's fight is fought headless at once (Combat).
public sealed record Hunt(AgentId Who, AgentId Prey) : Activity(Who);
/// Takes food from the tile's stash, for one minute (the carrion flock at an unwatched camp).
public sealed record Raid(AgentId Who) : Activity(Who);

/// Animals: their minds and what they do. Every animal is its own agent and decides one minute at a time,
/// in id order (Scheduler); a herd is its kin on its tile (Combat.Side). Travel is the shared Travel activity.
public static partial class Fauna
{
    public static HerdBehavior BehaviorOf(Species s) => SpeciesTable.Of(s).Behavior;

    /// Everything that is not prey, but the carrion flock, which hunts nothing and is no one's quarry.
    public static bool IsPredator(Species s) => BehaviorOf(s) is not (HerdBehavior.Prey or HerdBehavior.Carrion);

    /// What lives as prey: grazes, flees, and is hunted by people.
    public static bool IsPrey(Species s) => BehaviorOf(s) == HerdBehavior.Prey;

    /// What a pack hunts and lives on: prey, but not a mammoth, which no pack in the valley brings down.
    /// The one rule for which herds a pack goes for (PreyHere), follows (PreySensed) and counts as its
    /// food (PreyKgNear, ValleyScore). Packs share land with mammoths; let them hunt them and they eat
    /// the valley's mammoths out within two years.
    public static bool IsQuarry(Species s) => IsPrey(s) && !IsMegafauna(s);

    public static Diet DietOf(Species s) => SpeciesTable.Of(s).Diet;

    /// What each diet takes off a tile's forage (Content/species.json).
    public static IReadOnlyList<Resource> Eats(Diet d) => Content.Tables.Diets[d];

    // Per minute.
    const double FearDecayPerMinute = 0.015;
    /// What a wild animal burns over its basal rate, day in, day out: field metabolism is 1.6-3 times
    /// basal (research §1; Nagy 1999). The low end, since fights and flight charge their own work.
    const double FieldMetabolismFactor = 1.6;
    const double GrazingRatePerKgPerHour = 0.0001;
    const int PackPatrolEveryMinutes = 30, PackPatrolTimeoutMinutes = 120;
    /// Past this hunger a pack ranges wider and faster (DecidePack).
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

    /// The animals on a tile, in id order.
    public static IEnumerable<Agent> AnimalsAt(World world, TileId tile) =>
        Nearby.At(world, tile).Select(id => world.Agents[id]).Where(a => a.Mind is HerdMind);

    static void Become(World world, HerdMind mind, HerdState state)
    {
        mind.State = state;
        mind.StateSinceMinute = world.Minute;
    }

    static long StateMinutes(World world, HerdMind mind) => world.Minute - mind.StateSinceMinute;
    static double MassKg(Agent herd) => herd.Members.Sum(b => b.WeightKg);

    /// How hungry the herd is, 0 fed to 1 starving: how far its living members' calorie stores are
    /// from full (Body.CaloriesCapacityKcal). Derived, never stored: grazing and feeding fill the
    /// stores, and every minute lived and every burst of a fight empties them, so fleeing and
    /// watching cost a herd food.
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

    /// What an animal burns a minute, living: its field metabolism.
    public static double BurnKcalPerMinute(BodyState body) => Body.MetabolismKcalPerDay(body, FieldMetabolismFactor) / 1440;

    /// A minute of an animal's living: its burn, out of its calorie store (then its fat).
    internal static void Burn(BodyState body) => Body.SpendKcal(body, BurnKcalPerMinute(body));

    /// What a member can still eat: its calorie store's room, and the fat it has lost below its kind's
    /// build (a starved wolf gorges, and the surplus goes back on as fat through Body.Eat).
    static double RoomKcal(Agent herd, BodyState b) =>
        Math.Max(0, Body.CaloriesCapacityKcal(b) - b.CaloriesKcal) + Math.Max(0, SpeciesTable.Of(herd.Species).FatPct * b.WeightKg - b.FatKg) * Body.KcalPerKgFat;

    static double RoomKcal(Agent herd) => herd.Members.Where(Body.IsAlive).Sum(b => RoomKcal(herd, b));

    /// What the herd ate (no more than its room), shared among its living members by their room: none eats past full.
    static void Nourish(Agent herd, double kcal)
    {
        double room = RoomKcal(herd);
        if (kcal <= 0 || room <= 0) return;
        kcal = Math.Min(kcal, room);
        foreach (var b in herd.Members.Where(Body.IsAlive)) Body.Eat(b, kcal * RoomKcal(herd, b) / room);
    }

    // ---- one minute of a herd's bodies and appetite (Sim.Live calls this instead of Body.Tick) ----

    /// Every hurt tissue and the blood mend at this rate, whole in about three and a half days.
    public const double MendPctPerMinute = 0.0002;

    /// The members burn their calories and fear fades; wounds bleed and mend. A member that bleeds out is a carcass
    /// where it fell; a herd with nobody left is gone. Returns "died" then. While any of its members
    /// is in a fight the dead stay where they are (units name members by index): the aftermath takes them.
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
            // Nothing to fade, bleed or refill: skipping it changes nothing.
            if (body.Effects.Count > 0 || body.SprintSpentLevel > 0) Body.TickWounds(body, world.Rng("body", herd.Id));
            if (Body.IsAlive(body)) Burn(body);
            // Every animal mends, predator or prey, tissue included: a lame one walks again.
            if (!Hurt(body)) { body.WoundedBy = null; continue; } // mended: whoever hurt it no longer counts
            anyHurt = true;
            if (Body.IsAlive(body)) Mend(body);
            else anyDied = true;
        }
        if (!anyHurt || Combat.EncounterOf(world, herd.Id) != null) return null;
        if (anyDied)
        {
            foreach (var body in herd.Members.Where(b => !Body.IsAlive(b)).ToList())
            {
                // Bled out: the wounds were somebody's, and the carcass names them.
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

    /// One minute of a living animal's mending: every hurt layer, every organ not destroyed, and the blood.
    internal static void Mend(BodyState body)
    {
        if (body.BloodPct < 1) body.BloodPct = Math.Min(1, body.BloodPct + MendPctPerMinute);
        foreach (var region in body.Regions)
            foreach (var t in region.Layers.Concat(region.Organs.Where(o => o.Condition > 0)))
                if (t.Condition < 1) t.Condition = Math.Min(1, t.Condition + MendPctPerMinute);
    }

    // ---- herd activities ----

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

    /// Starts a herd activity. A hunt is fought here, headless, and takes as many minutes as it
    /// had rounds: those are returned, to be the activity's length.
    internal static int? Begin(World world, Activity activity, List<WorldEvent> events) => activity is not Hunt h ? null
        : world.Agent(h.Who).Mind is PersonMind ? PersonHunt(world, h, events)
        : PackHunt(world, world.Agent(h.Who), world.Agent(h.Prey), events);

    /// Why a person cannot start this stalk: the animal must be here, alive, and in no fight, and nothing
    /// may occupy it (Access, as a confrontation).
    static Refusal? PersonHuntRefusal(World world, Agent hunter, Hunt h)
    {
        if (!world.Agents.TryGetValue(h.Prey, out var prey) || prey.Mind is not HerdMind || prey.Tile != hunter.Tile || !Sim.IsAlive(prey))
            return new("No such herd here.");
        if (Combat.EncounterOf(world, h.Prey) != null) return new("They are already fighting.");
        return Access.Refusal(world, h.Who, LocationFeatures.OfAgent(world, h.Prey), confronting: true);
    }

    /// A person's stalk, with the best working weapon in the pack (Combat.WeaponWorth), at the weakest of the herd.
    /// The player's lasts this one minute of setting out (their Fight rounds are the stalk); anyone
    /// else's is fought out headless and lasts as many minutes as it had rounds.
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

    /// One minute of a herd activity, after the herd's Tick.
    internal static string? Minute(World world, Agent herd, Activity activity, List<WorldEvent> events)
    {
        if (activity is Graze) GrazeMinute(world, herd);
        else if (activity is Feed) FeedMinute(world, herd);
        else if (activity is Raid) RaidMinute(world, herd);
        return null;
    }

    /// A minute's grazing: each member eats its species' intake (to its room), and the herd thins what
    /// this diet eats off the tile's forage, with diminishing returns. Only a grazing herd eats:
    /// fleeing and fighting forgo it.
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
            Body.Eat(b, kcal); // each member its own intake, no more than its room
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

    /// Why the herd cannot graze here, or null: the tile's forage is reached through Access like
    /// anything else. Where nothing grows, grazing is only standing about, which nothing bars.
    static Refusal? GrazeRefusal(World world, Agent herd) =>
        ForageHere(world, herd.Tile) is (PoolId id, _) ? Access.Refusal(world, herd.Id, new LocationFeature(LocationFeatureKind.Pool, id.Tile, id.Index)) : null;

    /// How eaten-out this tile is for the herd, 0-1. Nothing it eats here at all is fully eaten out.
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

    /// Meat on the tile's carcasses an animal could eat, kg (Remains.MeatKg: none on bones).
    public static double MeatHere(World world, TileId tile) =>
        world.Tiles.GetValueOrDefault(tile)?.Carcasses?.Sum(c => Remains.MeatKg(world, tile, c)) ?? 0;

    /// Feeding, in calories: the herd eats what its stores have room for, no faster than 0.1 kg a
    /// minute a head, and is fed only by what it ate. Each carcass is reached through Access (an occupant
    /// bars the meal) and none is eaten once it is bones. The carrion flock picks at a dead
    /// person's body too, once the carcasses are bare (Remains.Pick).
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
            mind.Foray = null; // scavenged or its own kill: it has found food
            mind.LastFedMinute = world.Minute;
        }
    }

    static double MeatKcalPerKg => Items.Resources[Resource.RawMeat].KcalPerKg;

    /// Carcasses here with meat on them that the herd can reach (Access).
    static IEnumerable<Carcass> Edible(World world, Agent herd) =>
        (world.Tiles.GetValueOrDefault(herd.Tile)?.Carcasses ?? []).Where(c => Remains.MeatKg(world, herd.Tile, c) > 0
            && Access.Refusal(world, herd.Id, new LocationFeature(LocationFeatureKind.Carcass, herd.Tile, c.Id)) == null);

    /// Dead people on the herd's tile with flesh on them that it can reach (Access), in id order.
    static IEnumerable<AgentId> PickableBodies(World world, Agent herd) =>
        Nearby.At(world, herd.Tile).Where(id => world.Agents[id] is { Mind: PersonMind } d && id != AgentId.Player && !Sim.IsAlive(d)
            && Remains.StageOf(world, d) == RemainsStage.Flesh && Access.Refusal(world, herd.Id, LocationFeatures.OfAgent(world, id)) == null);

    /// What the mind asks before it chooses Feed.
    static bool CanFeed(World world, Agent herd) =>
        Edible(world, herd).Any() || BehaviorOf(herd.Species) == HerdBehavior.Carrion && PickableBodies(world, herd).Any();

    // ---- pack hunts ----

    /// The hunter's side (Combat.Side, the hunter first) goes for the one animal it chose; vigilance
    /// decides how the fight opens: noticed prey start alert at range, surprised prey are ambushed.
    /// Returns the fight's length in minutes. One animal, as wolves cut one out of a herd: against
    /// three, a pack that meets bison is wiped out in its first week.
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

    /// The most of a pack that goes into one fight.
    const int MaxHunters = 6;

    /// How far off an ambush opens. An approach opens from the grid's edge, whatever distance it is given.
    const int AmbushFromM = 5;

    /// The pack comes for a person on its tile, its boldness toward them carried into the fight. One who
    /// saw it coming meets it ready at the grid's edge; anyone else is ambushed. Returns the fight's minutes.
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

    /// Awake, and their last look about caught this herd: read from their memory.
    static bool SawItComing(World world, Agent person, Agent herd) =>
        Sim.Looks(person, Sim.ActivityOf(person))
        && Memories.LastSeenMinute(person, herd.Id) is long seen && world.Minute - seen <= Memories.GlanceMinutes;

    /// The prey's watchfulness against the pack's stealth: the more eyes in its herd, the likelier it
    /// notices; the bigger the pack, the harder it is to hide (both Combat.Side).
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

    /// What a fight leaves in a herd's mind, from its own side: fear by how it went; a predator that
    /// killed feeds; prey that did not win, and anything that scattered, runs. Whatever it was doing is over.
    internal static void AfterEncounter(World world, AgentId id, CombatResult own, bool killedOne, TileId tile)
    {
        if (!world.Agents.TryGetValue(id, out var herd) || herd.Mind is not HerdMind mind) return;
        mind.LastEncounterMinute = world.Minute;
        bool predator = IsPredator(herd.Species);
        // A kill feeds the pack from the carcass it made.
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

    /// A wounded animal the player saw get away, now its own herd of one: it bolts from where it
    /// was hurt and lives on a scrap of land around it.
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

    /// The herd's next activity (Minds.Decide), one minute's choice. A predator held off a tile with people
    /// (Combat.WouldEnter) waits instead, and tries again when it next walks that way.
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

    /// Whether people on that tile keep this predator off it, they and their fire weighed as in a fight there.
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
                // Drifts across its land, faster where it is eaten out.
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
                // A frightened pack (it just lost a fight) leaves prey alone.
                if (hunger > 0.4 && mind.Fear <= 0.5 && !SideFoughtThisMinute(world, herd) && PreyHere(world, herd, events) is Agent prey)
                    return (new Hunt(herd.Id, prey.Id), 1);
                // A starving pack does not go home empty: it ranges on (Foray).
                if (t > PackPatrolTimeoutMinutes && hunger <= StarvingHunger)
                {
                    Become(world, mind, HerdState.Resting);
                    mind.TerritoryIndex = 0;
                    return Toward(world, herd, mind, mind.Territory[0]) ?? Rest(herd);
                }
                if (hunger < 0.3) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                if (t > 0 && t % (hunger > StarvingHunger ? StarvingPatrolEveryMinutes : PackPatrolEveryMinutes) == 0)
                {
                    // A hungry pack follows the scent of prey it can sense instead of walking its round, so packs
                    // and herds actually meet.
                    if (hunger > 0.6 && TowardNearest(world, herd, PreySensed(world, herd, events)) is { } stalk) return stalk;
                    if (hunger > StarvingHunger && Foray(world, herd, mind) is { } range) return range;
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

    /// A starving pack trots off its land toward a far point within ForayRadiusTiles of its den, and on
    /// to another when it gets there or finds no way, until it picks up a scent; packs kept to their own
    /// few dozen tiles starve beside a valley full of prey. Real wolves range tens of kilometres a day
    /// when hungry. Eating ends it.
    static (Activity, int)? Foray(World world, Agent herd, HerdMind mind)
    {
        for (int tries = 0; tries < 2; tries++)
        {
            if (mind.Foray is not TileId target || target == herd.Tile)
            {
                mind.Foray = ForayTarget(world, herd, mind.Territory[0]);
                if (mind.Foray == null) return null;
                target = mind.Foray.Value;
            }
            if (Toward(world, herd, mind, target) is { } step) return step;
            if (!CanMove(herd)) return null;
            mind.Foray = null; // no way there: somewhere else
        }
        return null;
    }

    /// A random open tile within ForayRadiusTiles of the den, off any cave floor. Null after a few misses.
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
            case HerdState.Grazing: // a bear forages, omnivore that it is
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
                // Carcasses alone draw scavengers.
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

    /// Bolts one tile away from the threat, the way its kin are already running if they are, then settles:
    /// prey and predators rest, scavengers prowl. One tile only: fleeing every minute from the threat's
    /// tile runs a herd to the edge of the map.
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
        var m = Map.Of(world);
        var options = Enum.GetValues<Direction>().Where(d => Map.Blocked(m, herd.Tile, Map.Step(herd.Tile, d)) == null
            && !HeldOff(world, herd, Map.Step(herd.Tile, d), events)).ToList();
        if (options.Count == 0) return null;
        int best = options.Max(d => MapGen.Manhattan(Map.Step(herd.Tile, d), threat));
        options = options.Where(d => MapGen.Manhattan(Map.Step(herd.Tile, d), threat) == best).ToList();
        return options[world.Rng("herds", herd.Id).NextInt(options.Count)];
    }

    static bool Roll(World world, Agent herd, double chance) => world.Rng("herds", herd.Id).NextDouble() < chance;

    static bool CanMove(Agent herd) => Mobile(herd.Body);

    /// One step on toward the tile of its land it is making for; once there, it makes for another
    /// anywhere on its land. Aiming at the next tile in flood-fill order instead keeps a herd shuffling
    /// among the first few tiles.
    static (Activity, int)? PatrolStep(World world, Agent herd, HerdMind mind)
    {
        if (mind.Territory.Count < 2) return null;
        if (herd.Tile == mind.Territory[mind.TerritoryIndex]) mind.TerritoryIndex = world.Rng("herds", herd.Id).NextInt(mind.Territory.Count);
        var walk = Toward(world, herd, mind, mind.Territory[mind.TerritoryIndex]);
        if (walk != null || !CanMove(herd)) return walk;
        // No way there from here (walled off): it makes for somewhere else on its land next time.
        mind.TerritoryIndex = world.Rng("herds", herd.Id).NextInt(mind.Territory.Count);
        return null;
    }

    /// One step toward a tile: the shortest way through its own land when there is one (kept to the
    /// territory so it stays cheap), else the shortest way over open ground (Route). Null when already
    /// there, lame, or there is no way.
    static (Activity, int)? Toward(World world, Agent herd, HerdMind mind, TileId target)
    {
        if (herd.Tile == target || !CanMove(herd)) return null;
        var m = Map.Of(world);
        var land = mind.Territory.ToHashSet();
        var step = land.Contains(target) && land.Contains(herd.Tile) ? FirstStep(m, herd.Tile, target, land) : null;
        step ??= Route(m, herd.Tile, [target]);
        return step is Direction d ? (new Travel(herd.Id, d), UntilArrived) : null;
    }

    /// One step toward whichever of these tiles is nearest by the way there, not as the crow flies.
    static (Activity, int)? TowardNearest(World world, Agent herd, HashSet<TileId> goals)
    {
        if (goals.Count == 0 || goals.Contains(herd.Tile) || !CanMove(herd)) return null;
        return Route(Map.Of(world), herd.Tile, goals) is Direction d ? (new Travel(herd.Id, d), UntilArrived) : null;
    }

    /// Breadth-first through its own land, in Direction order: deterministic.
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

    /// Tiles a route may search before it gives up: a goal walled off from the herd is not worth the whole map.
    const int MaxRouteTiles = 4000;

    /// The first step of a shortest way over open ground to the nearest of the goals (A*, guided by
    /// the distance to the nearest goal), or null when none turns up within MaxRouteTiles. Not a greedy
    /// step to the nearer neighbour, which a mountain between a herd and its target holds still for good;
    /// and not a shortest way inside a box around the two ends, which moves with the herd, so the next
    /// box's way can turn it back and it walks to and fro for days. On one fixed map every step along a
    /// shortest way brings it strictly nearer.
    static Direction? Route(GeneratedMap m, TileId from, IReadOnlyCollection<TileId> goals)
    {
        int Guess(TileId t) => goals.Min(g => MapGen.Manhattan(t, g));
        var cost = new Dictionary<TileId, int> { [from] = 0 };
        var first = new Dictionary<TileId, Direction>();
        var done = new HashSet<TileId>();
        var open = new PriorityQueue<TileId, (int Total, int Left)>();
        open.Enqueue(from, (Guess(from), Guess(from)));
        while (open.TryDequeue(out var at, out _))
        {
            if (!done.Add(at)) continue;
            if (at != from && goals.Contains(at)) return first[at];
            if (done.Count > MaxRouteTiles) return null;
            foreach (var d in Enum.GetValues<Direction>())
            {
                var next = Map.Step(at, d);
                int g = cost[at] + 1;
                if (done.Contains(next) || (cost.TryGetValue(next, out var known) && known <= g) || Map.Blocked(m, at, next) != null) continue;
                cost[next] = g;
                first[next] = at == from ? d : first[at];
                int left = Guess(next);
                open.Enqueue(next, (g + left, left));
            }
        }
        return null;
    }

    /// An animal of its quarry or a person it would come for, on this tile and in no fight: the one it has
    /// the most resolve toward, the first in id order on a tie.
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

    /// Its resolve toward this person, when it reaches the line that lets it onto their tile (Combat.WouldEnter).
    static double? WouldComeFor(World world, Agent predator, Agent a, List<WorldEvent> events)
    {
        if (a.Mind is not PersonMind || !Sim.InTheWorld(world, a)) return null;
        Fire.CatchUpOn(world, a.Tile, events);
        double resolve = Combat.ResolveToward(world, predator, a);
        return resolve >= Combat.PressingEnterResolve ? resolve : null;
    }

    /// How far a species senses prey, in tiles.
    public static int DetectionRangeTiles(Species s) => s is Species.Wolf or Species.SaberTooth ? 12 : 8;

    /// Where its quarry, and the people it would come for, within the predator's senses stand.
    static HashSet<TileId> PreySensed(World world, Agent predator, List<WorldEvent> events)
    {
        return Nearby.Within(world, predator.Tile, DetectionRangeTiles(predator.Species))
            .Where(h => h.Mind is HerdMind ? IsQuarry(h.Species) : Combat.EncounterOf(world, h.Id) == null && WouldComeFor(world, predator, h, events) != null)
            .Select(h => h.Tile).ToHashSet();
    }

    /// The tiles with meat on them within the scavenger's nose, its own left out: found among the
    /// tiles that hold a carcass (Carcasses.TilesWith).
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

    // ---- the player's hunt ----

    /// Distance a stalk opens at.
    public const int HuntStartDistanceM = 34;

    /// The hunter stalks one animal on its tile: the hunter Engaged, the animal and a few of its herd
    /// Unaware (Alert if it is already running). The rounds are the hunter's Fight activities; the kill
    /// leaves a carcass to butcher.
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

    /// How watchful the stalked animals are, from what the stalked one is doing: its state stands for the
    /// activity and its learned fear for nervousness; one factor of the matching pool is picked at random.
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
                _ => Sightings.Moving, // patrolling, hunting: on the move
            };
        return pool[world.Rng("combat", hunter).NextInt(pool.Count)];
    }

    // DetectionModifier values, pool by pool (Content/species.json).
    static readonly SightingPools Sightings = Content.Tables.Sightings;
}

/// How watchful a stalked animal is, by what it is doing: one factor of the pool is picked at random.
public sealed record SightingPools(IReadOnlyList<double> Grazing, IReadOnlyList<double> Resting, IReadOnlyList<double> Wounded,
    IReadOnlyList<double> Alert, IReadOnlyList<double> Moving);
