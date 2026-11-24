namespace Survival;

/// An opportunist (ravens, foxes) neither hunts nor holds a carcass, and eats what it finds (Scavenging.cs).
public enum HerdBehavior { Prey, Pack, Solitary, Scavenger, Opportunist }

public enum Diet { Carnivore, Omnivore, Browser, Grazer }

/// Its hunger is its body's calorie store (Fauna.Hunger), never stored apart from it.
public sealed record HerdMind : Mind
{
    public const long Never = -1_000_000;
    /// Where a denning animal (a predator) sleeps best: its rest source, and what people remember as its den. Null for the rest.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TileId? Den { get; set; }
    /// What its mind chose last (Decide), as watchers read it: lying up, grazing, feeding; a copy of its Doing's record.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Activity? Choice { get; set; }
    public long FledMinute { get; set; } = Never;
    /// Learned fear 0-1: scales boldness down, and a frightened pack will not hunt.
    public double Fear { get; set; }
    public long LastEncounterMinute { get; set; } = Never;
    public TileId? FleeFrom { get; set; }
    /// When it last ate (the observer shows it); hunger itself is its body's.
    public long LastFedMinute { get; set; }
    /// Familiarity 0-1 with each animal it knows, kept only by a closed-group species (Fauna.IsKin); null otherwise.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SortedDictionary<AgentId, double>? Familiar { get; set; }
    /// What it is seeing to (Fauna.Needs), kept until an interrupt.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Plan? Plan { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<Sensed>? Sightings { get; set; }
}


public sealed record Graze(AgentId Who) : Activity(Who);
public sealed record Feed(AgentId Who) : Activity(Who);
/// A pack's hunt is fought headless at once; the player's stalk is fought round by round as Fight activities.
public sealed record Hunt(AgentId Who, AgentId Prey) : Activity(Who);
public sealed record Raid(AgentId Who) : Activity(Who);
/// An animal drinks at the best water on its tile (Work.WaterAt).
public sealed record Watering(AgentId Who) : Activity(Who);

/// Every animal is its own agent and decides one minute at a time, in id order; a herd is its kin on its tile.
public static partial class Fauna
{
    public static HerdBehavior BehaviorOf(Species s) => SpeciesTable.Of(s).Behavior;

    public static bool IsPredator(Species s) => BehaviorOf(s) is not (HerdBehavior.Prey or HerdBehavior.Opportunist);

    public static bool IsPrey(Species s) => BehaviorOf(s) == HerdBehavior.Prey;

    /// What a pack hunts: prey, but not a mammoth, or packs eat the valley's mammoths out within two years.
    public static bool IsQuarry(Species s) => IsPrey(s) && !IsMegafauna(s);

    public static Diet DietOf(Species s) => SpeciesTable.Of(s).Diet;

    public static IReadOnlyList<Resource> Eats(Diet d) => Content.Tables.Diets[d];

    const double FearDecayPerMinute = 0.015;
    /// Field metabolism over basal: 1.6-3 times (research §1; Nagy 1999), the low end, since fights and flight charge their own.
    const double FieldMetabolismFactor = 1.6;
    /// Grazed level a kcal eaten takes off a resource (0.0001 a kg-hour at 110 kg and 13 kcal a minute): by intake, not bulk.
    const double GrazedLevelPerKcal = 0.0001 / 60 * 110 / 13;
    /// Long enough for any crossing: a crossing ends itself on arrival.
    const int UntilArrived = int.MaxValue;

    /// A predator dens where it is put unless given a den; nothing else has one.
    public static HerdMind NewMind(World world, Species species, TileId tile, TileId? den = null) =>
        new() { Den = IsPredator(species) ? den ?? tile : null, LastFedMinute = world.Minute };

    public static HerdMind MindOf(Agent herd) =>
        herd.Mind as HerdMind ?? throw new InvalidOperationException($"{herd.Id} is not a herd");

    public static IEnumerable<Agent> AnimalsAt(World world, TileId tile) =>
        Nearby.At(world, tile).Where(a => a.Mind is HerdMind);

    public static bool Fleeing(HerdMind mind) => mind.FleeFrom != null;

    public static bool LyingUp(HerdMind mind) => mind.Choice is Rest;
    public static bool Eating(HerdMind mind) => mind.Choice is Graze or Feed;

    /// 0 fed to 1 starving: how far its calorie stores are from full, derived, so every minute lived costs it food.
    public static double Hunger(Agent herd)
    {
        double held = 0, capacity = 0;
        foreach (var b in herd.Members)
        {
            if (!Body.IsAlive(b)) continue;
            held += b.CaloriesKcal;
            capacity += Body.CaloriesCapacityKcal(b);
        }
        return capacity <= 0 ? 0 : Math.Clamp(1 - held / capacity, 0, 1);
    }

    public static double BurnKcalPerMinute(BodyState body) => Body.MetabolismKcalPerDay(body, FieldMetabolismFactor) / 1440;

    static void Burn(BodyState body) => Body.SpendKcal(body, BurnKcalPerMinute(body));

    /// A living animal's minute: its burn, its water, rest and hunger, and mending while fed and nothing is harming it (as a person heals).
    internal static void Live(World world, Agent animal, BodyState body, bool sleeping)
    {
        Burn(body);
        Body.LiveNeeds(body, sleeping, SpeciesTable.Of(animal.Species).SleepHoursPerDay, SleepFactor(animal, animal.Tile), world.Rng("body", animal.Id));
        if (Hurt(body) && Body.IsAlive(body) && Body.LethalRates(body) == (0, 0, 0) && body.CaloriesPct > Body.RegenMinCaloriesPct) Mend(body);
    }

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

    /// Asleep while its activity is Rest. In a fight the dead stay listed (units name members by index) for the aftermath.
    public static string? Tick(World world, Agent herd, List<WorldEvent> events, Activity? doing = null)
    {
        var mind = MindOf(herd);
        mind.Fear = Math.Max(0, mind.Fear - FearDecayPerMinute);
        Acquainting(world, herd);
        bool anyDied = false, anyHurt = false;
        foreach (var body in herd.Members)
        {
            if (world.Minute <= body.WoundsLivedThroughMinute) continue;
            body.WoundsLivedThroughMinute = world.Minute;
            if (body.Effects.Count > 0 || body.SprintSpentLevel > 0) Body.TickWounds(body, world.Rng("body", herd.Id));
            if (Body.IsAlive(body)) Live(world, herd, body, doing is Rest);
            // Dead of thirst its brain is gone, not mending: dead whether or not it reads as hurt.
            if (!Body.IsAlive(body)) anyDied = true;
            else if (!Hurt(body)) body.WoundedBy = null;
            else anyHurt = true;
        }
        if (!anyHurt && !anyDied || Combat.EncounterOf(world, herd.Id) != null) return null;
        if (anyDied)
        {
            foreach (var body in herd.Members.Where(b => !Body.IsAlive(b)).ToList())
            {
                var cause = body.WoundedBy is Cause w ? new Cause(w.Who, $"wounds from {w.What}") : body.DeathCause ?? new Cause(null, "its wounds");
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


    public static bool Handles(Activity a) => a is Graze or Feed or Hunt or Raid or Watering;

    public static Refusal? Refusal(World world, Activity activity)
    {
        var agent = world.Agent(activity.Who);
        if (activity is Hunt hunt && agent.Mind is PersonMind) return PersonHuntRefusal(world, agent, hunt);
        if (agent.Mind is not HerdMind) return new("only a herd does that");
        if (activity is Graze) return GrazeRefusal(world, agent);
        if (activity is Feed) return CanFeed(world, agent) ? null : new("nothing here it can reach to eat");
        if (activity is Raid) return RaidRefusal(world, agent);
        if (activity is Watering) return WaterHere(world, agent) == null ? new("no water here") : null;
        if (activity is not Hunt h) return null;
        if (h.Prey == h.Who || !world.Agents.TryGetValue(h.Prey, out var prey) || prey.Tile != agent.Tile || !Sim.IsAlive(prey))
            return new("no such prey here");
        if (Access.Refusal(world, h.Who, LocationFeatures.OfAgent(world, h.Prey), confronting: true) is { } barred) return barred;
        if (Combat.EncounterOf(world, h.Prey) != null || Combat.EncounterOf(world, h.Who) != null) return new("already fighting");
        return null;
    }

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
        else if (activity is Watering && WaterHere(world, herd) is { } water) Work.DrinkFrom(herd.Body, water);
        return null;
    }

    /// What it takes, and the pool grazed down by what it took (GrazedLevelPerKcal, GrazeAt).
    static void GrazeMinute(World world, Agent animal)
    {
        if (GrazeHere(world, animal) is not { } graze) return;
        var body = animal.Body;
        double kcal = Math.Min(graze.PerMinute, RoomKcal(animal, body));
        if (kcal <= 0) return;
        Body.Eat(body, kcal);
        MindOf(animal).LastFedMinute = world.Minute;
        var diet = DietOf(animal.Species);
        if (ForageHere(world, animal.Tile) is not (PoolId id, ForagePool pool)) return;
        double dietFactor = diet == Diet.Omnivore ? 5.0 : 1.0;
        foreach (var r in Eats(diet).Where(r => pool.Items.Any(i => i.Resource == r)))
        {
            double level = Pools.GrazedLevel(world, id, r);
            if (level >= 0.95) continue;
            Pools.SetGrazed(world, id, r, Math.Min(1.0, level + kcal * GrazedLevelPerKcal * dietFactor * (1 - level)));
        }
    }

    /// A forecast: the kcal a minute's grazing pays it where it stands (GrazeAt).
    public static Source? GrazeHere(World world, Agent animal) => GrazeAt(world, animal, animal.Tile);

    /// Its full rate by what is left of its diet's forage, times min(1, capacity / load); null where nothing it eats grows.
    /// A meat-eater's grazing is the small game it lives on between kills, on any ground at an average tile's capacity.
    static Source? GrazeAt(World world, Agent animal, TileId tile)
    {
        double rate = SpeciesTable.Of(animal.Species).GrazeKcalPerMinute;
        if (rate <= 0) return null;
        var eats = Eats(DietOf(animal.Species));
        double left = 1, density = 0;
        ForagePool? pool = null;
        if (eats.Count > 0)
        {
            if (ForageHere(world, tile) is not (PoolId id, ForagePool p)) return null;
            pool = p;
            // Read on every tile a food plan weighs, so counted, not listed, and indexed (an interface's enumerator is boxed).
            double sum = 0;
            int count = 0;
            for (int i = 0; i < eats.Count; i++)
            {
                if (!Grows(p, eats[i])) continue;
                sum += 1 - Pools.GrazedLevel(world, id, eats[i]);
                count++;
            }
            if (count == 0) return null;
            left = sum / count;
            density = p.Density;
        }
        if (left <= 0) return null;
        double capacity = TileCapacity(pool == null ? 1 : density / MeanForageDensity(world), left);
        return new Source(rate * left * Math.Min(1, capacity / Load(world, animal, tile, eats, pool)));
    }

    static bool Grows(ForagePool pool, Resource r)
    {
        for (int i = 0; i < pool.Items.Count; i++) if (pool.Items[i].Resource == r) return true;
        return false;
    }

    /// The intake a tile of average forage holds: twenty caribou's (20 x 13 kcal a minute), about three mammoths'.
    const double AverageTileKcalPerMinute = 260;

    /// The intake a tile holds: its richness against the valley's mean, times what is left of what this grazer eats there.
    static double TileCapacity(double richness, double left) => AverageTileKcalPerMinute * richness * left;

    /// The mean forage density over the valley's open land, ungrazed. Fixed at generation, so worked out once and never saved.
    static double MeanForageDensity(World world)
    {
        if (world.MeanForageDensityCache is { } mean) return mean;
        double sum = 0;
        int count = 0;
        foreach (var t in OpenLand(world))
            if (ForageHere(world, t) is (_, ForagePool p)) { sum += p.Density; count++; }
        return (world.MeanForageDensityCache = count > 0 ? sum / count : 1).Value;
    }

    /// A grazer's intake as load on a tile: its species' rate scaled by its weight against the species', to the 3/4 power.
    static double Intake(Agent a)
    {
        var row = SpeciesTable.Of(a.Species);
        return row.GrazeKcalPerMinute * Math.Pow(a.Body.WeightKg / row.WeightKg, 0.75);
    }

    /// Its intake and that of the living on and stepping onto the tile that graze what it would there.
    static double Load(World world, Agent animal, TileId tile, IReadOnlyList<Resource> eats, ForagePool? pool)
    {
        Span<bool?> sharesByDiet = stackalloc bool?[DietCount];
        double load = Intake(animal) + LoadOn(world, animal, tile, tile, eats, pool, sharesByDiet);
        foreach (var d in Map.Directions) load += LoadOn(world, animal, Map.Step(tile, d), tile, eats, pool, sharesByDiet);
        return load;
    }

    static readonly int DietCount = Enum.GetValues<Diet>().Length;

    static double LoadOn(World world, Agent animal, TileId on, TileId tile, IReadOnlyList<Resource> eats, ForagePool? pool, Span<bool?> sharesByDiet)
    {
        var here = Nearby.At(world, on);
        double load = 0;
        for (int i = 0; i < here.Count; i++)
        {
            var other = here[i];
            if (other.Id == animal.Id || other.Mind is not HerdMind) continue;
            var row = SpeciesTable.Of(other.Species);
            if (row.GrazeKcalPerMinute <= 0
                || !(other.Crossing is { } c ? Map.Step(c.From, c.Dir) == tile : other.Tile == tile) || !Sim.IsAlive(other)) continue;
            if (sharesByDiet[(int)row.Diet] ??= Shares(eats, Eats(row.Diet), pool)) load += Intake(other);
        }
        return load;
    }

    static bool Shares(IReadOnlyList<Resource> eats, IReadOnlyList<Resource> theirs, ForagePool? pool)
    {
        if (eats.Count == 0 || theirs.Count == 0) return eats.Count == theirs.Count;
        if (pool == null) return false;
        for (int i = 0; i < eats.Count; i++)
            if (Grows(pool, eats[i]))
                for (int j = 0; j < theirs.Count; j++) if (theirs[j] == eats[i]) return true;
        return false;
    }

    static (PoolId, ForagePool)? ForageHere(World world, TileId tile) =>
        Pools.At(world, tile) is [ForagePool pool, ..] ? (new PoolId(tile, 0), pool) : null;

    static Refusal? GrazeRefusal(World world, Agent herd)
    {
        if (GrazeHere(world, herd) == null) return new("nothing here it eats");
        return ForageHere(world, herd.Tile) is (PoolId id, _) && Eats(DietOf(herd.Species)).Count > 0
            ? Access.Refusal(world, herd.Id, new LocationFeature(LocationFeatureKind.Pool, id.Tile, id.Index)) : null;
    }

    /// Defending a kill: the one hunted, then each of its side holding it bold enough toward the hunter, up to MaxHunters, that boldness its morale.
    const double EvenBoldness = 0.5;

    static List<Fighter> Defenders(World world, Agent animal, Agent hunter)
    {
        var defenders = new List<Fighter>();
        foreach (var a in Combat.Side(world, animal))
        {
            if (a.Id != animal.Id && (defenders.Count >= MaxHunters || !HoldsAKill(world, a))) continue;
            double bold = Combat.BoldnessToward(world, a, hunter);
            if (a.Id == animal.Id || bold >= EvenBoldness) defenders.Add(new Fighter(a.Id) { BoldnessDelta = bold - EvenBoldness });
        }
        return defenders;
    }

    /// Defending a kill: a predator attending (Tags.Attending) a carcass with meat on its tile.
    public static bool HoldsAKill(World world, Agent animal) =>
        animal.Mind is HerdMind mind && IsPredator(animal.Species) && MeatHere(world, animal.Tile) > 0 && Tags.Attending(world, animal, mind);

    public static double MeatHere(World world, TileId tile) =>
        world.Tiles.GetValueOrDefault(tile)?.Carcasses?.Sum(c => Remains.MeatKg(world, tile, c)) ?? 0;

    const double FeedKgPerMinute = 0.1;

    /// No faster than FeedKgPerMinute a head; an opportunist picks at a dead person's body once the carcasses are bare.
    static void FeedMinute(World world, Agent herd)
    {
        var mind = MindOf(herd);
        double want = Math.Min(RoomKcal(herd) / MeatKcalPerKg, FeedKgPerMinute * herd.Members.Count), eaten = 0;
        foreach (var c in Edible(world, herd))
            if (want > eaten) eaten += Carcasses.EatMeat(c, want - eaten);
        if (want > eaten && BehaviorOf(herd.Species) == HerdBehavior.Opportunist)
            foreach (var body in PickableBodies(world, herd))
                if (want > eaten) eaten += Remains.Pick(world, body, want - eaten);
        Nourish(herd, eaten * MeatKcalPerKg);
        if (eaten > 0) mind.LastFedMinute = world.Minute;
    }

    static double MeatKcalPerKg => Items.Resources[Resource.RawMeat].KcalPerKg;

    static IEnumerable<Carcass> Edible(World world, Agent herd) =>
        (world.Tiles.GetValueOrDefault(herd.Tile)?.Carcasses ?? []).Where(c => Remains.MeatKg(world, herd.Tile, c) > 0
            && Access.Refusal(world, herd.Id, new LocationFeature(LocationFeatureKind.Carcass, herd.Tile, c.Id)) == null);

    static IEnumerable<AgentId> PickableBodies(World world, Agent herd) =>
        Nearby.At(world, herd.Tile).Where(d => d.Mind is PersonMind && d.Id != AgentId.Player && !Sim.IsAlive(d)
            && Remains.StageOf(world, d) == RemainsStage.Flesh && Access.Refusal(world, herd.Id, LocationFeatures.OfAgent(world, d.Id)) == null).Select(d => d.Id);

    static bool CanFeed(World world, Agent herd) =>
        Edible(world, herd).Any() || BehaviorOf(herd.Species) == HerdBehavior.Opportunist && PickableBodies(world, herd).Any();


    /// The hunters against the quarry's lead and a few of its herd (Combat.SendSide); noticed prey start alert at range.
    internal static int PackHunt(World world, Agent pack, Agent prey, List<WorldEvent> events)
    {
        if (prey.Mind is PersonMind) return ComeFor(world, pack, prey, events);
        var hunters = Hunters(world, pack).Select(a => new Fighter(a.Id)).ToList();
        bool noticed = PreyNotices(world, pack, prey);
        var enc = Combat.Create(world, hunters, Combat.SendSide(world, prey.Id), pack.Tile, noticed ? 15 : 5, Awareness.Engaged,
            noticed ? Awareness.Alert : Awareness.Unaware, events: events);
        Combat.ResolveHeadless(world, enc, events);
        return Math.Max(1, enc.Rounds);
    }

    /// Hunting together: the one that starts it, then each of its side whose own food priority agrees and that is not afraid.
    internal static List<Agent> Hunters(World world, Agent lead)
    {
        var hunters = new List<Agent> { lead };
        foreach (var a in Combat.Side(world, lead).Skip(1))
            if (hunters.Count < MaxHunters && FoodDue(a) && MindOf(a).Fear <= Tags.AfraidLevel) hunters.Add(a);
        return hunters;
    }

    static bool FoodDue(Agent a) => Priorities.AnimalFoodPriority(a.Body, SpeciesTable.Of(a.Species).FatPct,
        seeingTo: MindOf(a).Plan?.Stat == Stat.Food) >= Priority.Need;

    public const int MaxHunters = 6;
    const double OffHoursVigilanceFactor = 0.7;

    /// In its species' active hours (species.json activity), read through the one night rule.
    public static bool ActiveNow(World world, Species s) => SpeciesTable.Of(s).Activity switch
    {
        ActivityPattern.Diurnal => !Weather.IsNight(world.Minute),
        ActivityPattern.Nocturnal => Weather.IsNight(world.Minute),
        _ => Weather.IsTwilight(world.Minute),
    };

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
        vigilance *= Fleeing(preyMind) ? 1.3 : LyingUp(preyMind) ? 0.6 : 1.0;
        if (!Fleeing(preyMind) && LyingUp(preyMind) && world.Minute - preyMind.FledMinute < JustRanMinutes) vigilance *= 1.4;
        if (!ActiveNow(world, prey.Species)) vigilance *= OffHoursVigilanceFactor;
        double stealth = 0.5 - Combat.Side(world, predator).Count * 0.02, hunger = Hunger(predator);
        if (hunger > 0.7) stealth += 0.15;
        if (hunger > 0.9) stealth += 0.15;
        stealth *= SpeciesTable.Of(predator.Species).StealthFactor;
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
        mind.Fear = Math.Max(mind.Fear, Combat.FearAfter(own));
        bool scattered = own is CombatResult.Fled or CombatResult.AnimalDisengaged;
        if (scattered || (!predator && own != CombatResult.Victory)) StartFleeing(world, mind, tile);
        herd.Doing = null;
    }

    const int JustRanMinutes = 30;

    static void StartFleeing(World world, HerdMind mind, TileId from)
    {
        mind.FleeFrom = from;
        mind.FledMinute = world.Minute;
    }

    /// A wounded animal the player saw get away bolts from where it was hurt.
    internal static void Escaped(World world, AgentId id, TileId tile)
    {
        var herd = world.Agent(id);
        StartFleeing(world, MindOf(herd), tile);
        herd.Doing = null;
    }


    /// One mind for every animal: running, else what is due on the planner (Needs), else its kin, its role's rules, and idling.
    /// A predator held off a tile with people (Combat.WouldEnter) waits instead.
    public static (Activity Activity, int Minutes) Decide(World world, Agent herd, List<WorldEvent> events)
    {
        var mind = MindOf(herd);
        bool fleeing = Fleeing(mind);
        var choice = fleeing ? Flee(world, herd, mind, events)
            : Needs(world, herd, mind, events) ?? Cohere(world, herd, mind) ?? ComeForPeople(world, herd, mind, events)
                ?? Scavenge(world, herd, mind, events) ?? Idle(world, herd, mind);
        if (choice.Item1 is Travel t && !fleeing && HeldOff(world, herd, Map.Step(herd.Tile, t.Dir), events)) choice = Wait(herd);
        mind.Choice = choice.Item1 with { };
        return choice;
    }

    static bool HeldOff(World world, Agent herd, TileId to, List<WorldEvent> events)
    {
        if (!IsPredator(herd.Species) || !Nearby.At(world, to).Any(a => a.Mind is PersonMind && Sim.InTheWorld(world, a))) return false;
        Fire.CatchUpOn(world, to, events);
        return !Combat.WouldEnter(world, herd, to);
    }

    static (Activity, int) Rest(Agent herd) => (new Rest(herd.Id), 1);
    static (Activity, int) Wait(Agent herd) => (new Wait(herd.Id), 1);

    /// Idle, it starts grazing above one hunger and stops below the next; meat on its tile it eats down to sated.
    const double StartsEatingHunger = 0.5, StopsEatingHunger = 0.2, SatedHunger = 0.1;

    /// Nothing due: the meat on its tile while it has room, its own tile's grazing once hungry enough, else up in its hours and lying up out of them.
    static (Activity, int) Idle(World world, Agent animal, HerdMind mind)
    {
        double hunger = Hunger(animal);
        if (!IsPrey(animal.Species) && hunger > SatedHunger && CanFeed(world, animal) && !HeldKill(world, animal)) return (new Feed(animal.Id), 1);
        if (hunger > (mind.Choice is Graze ? StopsEatingHunger : StartsEatingHunger) && GrazeRefusal(world, animal) == null) return (new Graze(animal.Id), 1);
        return ActiveNow(world, animal.Species) ? Wait(animal) : Rest(animal);
    }

    const double ComesForPeopleHunger = 0.4, StalksHunger = 0.6, StarvingHunger = 0.8;
    const int StalkEveryMinutes = 30, StarvingStalkEveryMinutes = 5;

    /// A pack comes for a person it would come for on its tile; hungrier, it goes after one it senses, on its stalking clock.
    static (Activity, int)? ComeForPeople(World world, Agent pack, HerdMind mind, List<WorldEvent> events)
    {
        if (BehaviorOf(pack.Species) != HerdBehavior.Pack || mind.Fear > Tags.AfraidLevel) return null;
        double hunger = Hunger(pack);
        if (hunger <= ComesForPeopleHunger || !ActiveNow(world, pack.Species) && hunger <= StalksHunger) return null;
        if (!SideFoughtThisMinute(world, pack) && PersonHere(world, pack, events) is Agent person) return (new Hunt(pack.Id, person.Id), 1);
        if (hunger <= StalksHunger) return null;
        return world.Minute % (hunger > StarvingHunger ? StarvingStalkEveryMinutes : StalkEveryMinutes) == 0 ? Stalk(world, pack, events) : null;
    }

    /// One tile only, the way its kin are already running: fleeing every minute from the threat's tile runs a herd to the map's edge.
    static (Activity, int) Flee(World world, Agent herd, HerdMind mind, List<WorldEvent> events)
    {
        if (mind.FleeFrom is TileId from && herd.Tile == from && CanMove(herd) && (Following(world, herd)?.Way ?? FleeDirection(world, herd, from, events)) is Direction away)
            return (new Travel(herd.Id, away), UntilArrived);
        mind.FleeFrom = null;
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

    /// Quickest to reach (Trip.Routes), not nearest as the crow flies; ties go to the lower tile.
    static (Activity, int)? TowardNearest(World world, Agent herd, HashSet<TileId> goals) =>
        NearestRoute(world, herd, goals) is { } r ? (new Travel(herd.Id, r.First), UntilArrived) : null;

    /// A hungry pack goes after a person it senses and would come for, weighed afresh each time.
    static (Activity, int)? Stalk(World world, Agent herd, List<WorldEvent> events) =>
        NearestRoute(world, herd, PeopleSensed(world, herd, events)) is { } r ? (new Travel(herd.Id, r.First), UntilArrived) : null;

    static (Direction First, TileId Goal)? NearestRoute(World world, Agent herd, HashSet<TileId> goals)
    {
        if (goals.Count == 0 || goals.Contains(herd.Tile) || !CanMove(herd)) return null;
        TileId? reached = null;
        var mind = MindOf(herd);
        // A pack keeps off the ground beside rivals it senses, unless that leaves it no way at all.
        bool wary = IsClosedGroup(herd.Species) && mind.Sightings != null;
        var routes = Trip.Routes(world, herd.Id, MaxRouteMinutes, refused: wary ? t => ThreatNear(mind, t) : null, until: Reached);
        if (reached == null && wary) routes = Trip.Routes(world, herd.Id, MaxRouteMinutes, until: Reached);
        return reached is TileId goal ? (routes.FirstStep(goal)!.Value, goal) : null;

        bool Reached(TileId t, int _)
        {
            if (!goals.Contains(t)) return false;
            reached = t;
            return true;
        }
    }

    /// How far a route looks before it gives up: a goal walled off from the herd is not worth the whole map.
    const int MaxRouteMinutes = 1440;

    /// The person here it has the most resolve toward and would come for, the first in id order on a tie.
    static Agent? PersonHere(World world, Agent predator, List<WorldEvent> events)
    {
        Fire.CatchUpOn(world, predator.Tile, events);
        Agent? best = null;
        double bestResolve = double.NegativeInfinity;
        foreach (var a in Nearby.At(world, predator.Tile))
        {
            if (a.Id == predator.Id || Combat.EncounterOf(world, a.Id) != null || WouldComeFor(world, predator, a, events) is not double resolve) continue;
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

    public static int DetectionRangeTiles(Species s) => SpeciesTable.Of(s).DetectionRangeTiles;

    static HashSet<TileId> PeopleSensed(World world, Agent predator, List<WorldEvent> events) =>
        Nearby.Within(world, predator.Tile, DetectionRangeTiles(predator.Species))
            .Where(h => h.Mind is PersonMind && Combat.EncounterOf(world, h.Id) == null && WouldComeFor(world, predator, h, events) != null)
            .Select(h => h.Tile).ToHashSet();

    static HashSet<TileId> MeatSensed(World world, TileId from, int radius)
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
        var awareness = Fleeing(mind) ? Awareness.Alert : Awareness.Unaware;
        double watchfulness = SightingDetectionFactor(world, hunter, animal);
        var animals = HoldsAKill(world, prey) ? Defenders(world, prey, hunterAgent) : Combat.SendSide(world, animal);
        animals = animals.Select(f => f with { DetectionFactor = watchfulness }).ToList();
        return Combat.Create(world, [new Fighter(hunter) { Weapon = weapon }], animals,
            prey.Tile, HuntStartDistanceM, Awareness.Engaged, awareness, events: events);
    }

    /// What it last chose stands for the activity and its learned fear for nervousness; one factor of the matching pool is picked at random.
    public static double SightingDetectionFactor(World world, AgentId hunter, AgentId animal)
    {
        var agent = world.Agent(animal);
        var mind = (HerdMind)agent.Mind;
        var pool = Combat.WoundLevel(agent.Body) > 0.05 ? Sightings.Wounded
            : Fleeing(mind) || mind.Fear > 0.7 ? Sightings.Alert
            : Eating(mind) ? Sightings.Grazing
            : LyingUp(mind) ? Sightings.Resting
            : Sightings.Moving;
        return pool[world.Rng("combat", hunter).NextInt(pool.Count)];
    }

    static readonly SightingPools Sightings = Content.Tables.Sightings;
}

public sealed record SightingPools(IReadOnlyList<double> Grazing, IReadOnlyList<double> Resting, IReadOnlyList<double> Wounded,
    IReadOnlyList<double> Alert, IReadOnlyList<double> Moving);
