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
    /// Where a starving pack is ranging to, off its land (Fauna.Foray); cleared when it eats.
    public TileId? Foray { get; set; }
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
// Herds never come for the player or an NPC yet (old ShouldEngagePlayer, PredatorInteraction's stalking,
// the NPC attacks in PackPredatorBehavior). Combat.Boldness is ready for when they do.
public static partial class Fauna
{
    public static HerdBehavior BehaviorOf(Species s) => SpeciesTable.Of(s).Behavior;

    /// Old AnimalType.IsPredator: everything that is not prey.
    public static bool IsPredator(Species s) => BehaviorOf(s) != HerdBehavior.Prey;

    public static Diet DietOf(Species s) => SpeciesTable.Of(s).Diet;

    /// Old AnimalDietExtensions.GetConsumedResources: what each diet takes off a tile's forage (Content/species.json).
    public static IReadOnlyList<Resource> Eats(Diet d) => Content.Tables.Diets[d];

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
    /// Judgment, not in the old game: past this hunger a pack ranges wider and faster (DecidePack).
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
            Hunger = world.Birth("herds", id).NextDouble() * 0.3, // start slightly hungry
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

    /// Old PreyBehavior: a wounded herd's WoundSeverity fell 0.0002 a minute, whole in about three
    /// and a half days. Here it is every hurt tissue and the blood that mend at that rate, so the
    /// wound level (Combat.WoundLevel) falls exactly as the old severity did.
    public const double MendPctPerMinute = 0.0002;

    /// Hunger grows and fear fades; wounds bleed and mend. A member that bleeds out is a carcass
    /// where it fell; a herd with nobody left is gone. Returns "died" then. While any of its members
    /// is in a fight the dead stay where they are (units name members by index): the aftermath takes them.
    public static string? Tick(World world, Agent herd, List<WorldEvent> events)
    {
        var mind = MindOf(herd);
        // old game: hunger stood still while a herd was walking, so a herd on the move never got hungrier (bug, fixed).
        mind.Hunger = Math.Min(1, mind.Hunger + HungerPerMinute(BehaviorOf(herd.Species)));
        mind.Fear = Math.Max(0, mind.Fear - FearDecayPerMinute);
        bool anyDied = false, anyHurt = false;
        foreach (var body in herd.Members)
        {
            // A headless fight already lived this minute of it.
            if (world.Minute <= body.WoundsLivedThroughMinute) continue;
            body.WoundsLivedThroughMinute = world.Minute;
            if (body.Effects.Count > 0) Body.TickWounds(body, world.Rng("body", herd.Id)); // nothing to fade or bleed: skipping it changes nothing
            // old game: only prey herds split off wounded healed, and only their severity number; a predator's wounds and every
            // body's tissue stayed as they were, so one lame member pinned its herd for good (bug, fixed).
            if (!Hurt(body)) continue;
            anyHurt = true;
            if (Body.IsAlive(body)) Mend(body);
            else anyDied = true;
        }
        if (!anyHurt || Combat.EncounterOf(world, herd.Id) != null) return null;
        if (anyDied)
        {
            foreach (var body in herd.Members.Where(b => !Body.IsAlive(b)).ToList())
            {
                Combat.LeaveCarcass(world, herd.Tile, herd.Species, body, new Cause(null, "its wounds"));
                events.Add(new Killed(world.Minute, herd.Id, herd.Species, body.WeightKg, herd.Tile, By: null));
                herd.Members.Remove(body);
            }
            if (herd.Members.Count == 0)
            {
                Agents.Remove(world, herd.Id);
                return "died";
            }
        }
        LeaveBehindTheLame(world, herd);
        return null;
    }

    /// A member too lame to walk (Traversal.Refusal's threshold) drops out of its herd as a herd of
    /// one, where it stands, to mend or die; the rest go on at the pace of whoever can still walk.
    /// It has lived this minute with them, so it starts on the next. Not in the old game, where a
    /// herd moved at its first member's pace however hurt the others were (judgment: realistic, and
    /// one lame member can no longer hold a herd still).
    // A member that mends never rejoins; it lives on as its own herd on a copy of their land.
    static void LeaveBehindTheLame(World world, Agent herd)
    {
        for (int i = herd.Members.Count - 1; i >= 0 && herd.Members.Count > 1; i--)
            if (Hurt(herd.Members[i]) && !Mobile(herd.Members[i])) Agents.SplitOff(world, herd.Id, i);
    }

    static bool Mobile(BodyState body) => Body.GetCapacities(body).Moving > 0.1;

    static bool Hurt(BodyState body) => body.BloodPct < 1 || body.Regions.Any(r => r.Condition < 1 || r.Organs.Any(o => o.Condition is > 0 and < 1));

    /// One minute of a living animal's mending: every hurt layer, every organ not destroyed, and the blood.
    internal static void Mend(BodyState body)
    {
        if (body.BloodPct < 1) body.BloodPct = Math.Min(1, body.BloodPct + MendPctPerMinute);
        foreach (var region in body.Regions)
            foreach (var t in region.Layers.Concat(region.Organs.Where(o => o.Condition > 0)))
                if (t.Condition < 1) t.Condition = Math.Min(1, t.Condition + MendPctPerMinute);
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
        if (Access.Refusal(world, h.Who, Things.OfAgent(world, h.Prey), confronting: true) is string barred) return barred;
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

    /// Meat on the tile's carcasses an animal could eat, kg (Carcasses.MeatKg).
    public static double MeatHere(World world, TileId tile) =>
        world.Tiles.GetValueOrDefault(tile)?.Carcasses?.Sum(Carcasses.MeatKg) ?? 0;

    /// Old PredatorInteraction's feeding: a meal is 3% of the group's mass, eaten no faster than
    /// 0.1 kg a minute a head, and hunger falls only for what was eaten.
    static void FeedMinute(World world, Agent herd)
    {
        var mind = MindOf(herd);
        double mealKg = Math.Max(1, MassKg(herd) * 0.03);
        double want = Math.Min(mealKg * mind.Hunger, 0.1 * herd.Members.Count), eaten = 0;
        foreach (var c in world.Tiles.GetValueOrDefault(herd.Tile)?.Carcasses ?? [])
            if (want > eaten) eaten += Carcasses.EatMeat(c, want - eaten);
        mind.Hunger = Math.Max(0, mind.Hunger - eaten / mealKg);
        if (eaten > 0) mind.Foray = null; // scavenged or its own kill: it has found food
    }

    // ---- pack hunts (old PackPredatorBehavior.AttemptNpcHunt, HerdVigilance) ----

    /// The pack singles out the weakest; vigilance decides how the fight opens: noticed prey
    /// start alert at range, surprised prey are ambushed. Returns the fight's length in minutes.
    /// Old game: the pack went for the weakest three at once. Judgment: one, as wolves cut one animal
    /// out of a herd. Against three, a pack that ranged far enough to meet bison killed two or three of
    /// them and was wiped out in its first week; against one it lives and the valley's prey thin slowly.
    static int PackHunt(World world, Agent pack, Agent prey, List<WorldEvent> events)
    {
        Become(world, MindOf(pack), HerdState.Hunting);
        var targets = Enumerable.Range(0, prey.Members.Count).Where(i => Body.IsAlive(prey.Members[i]))
            .OrderBy(i => Body.Speed(prey.Members[i]) * Body.Vitality(prey.Members[i])).ThenBy(i => Body.Vitality(prey.Members[i]))
            .Take(1).Select(i => new Fighter(prey.Id, i)).ToList();
        var hunters = Enumerable.Range(0, pack.Members.Count).Where(i => Body.IsAlive(pack.Members[i]))
            .Take(6).Select(i => new Fighter(pack.Id, i)).ToList();
        bool noticed = PreyNotices(world, pack, prey);
        var enc = Combat.Create(world, hunters, targets, pack.Tile, noticed ? 15 : 5, Awareness.Engaged, noticed ? Awareness.Alert : Awareness.Unaware);
        Combat.ResolveHeadless(world, enc, events);
        // Old hungry wolves (hunger > 0.8) chased escaping prey 40% of the time; they patrol on here.
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
        var rng = world.Rng("herds", prey.Id);
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
                if (Roll(world, herd, 0.01 + 0.01 * GrazedLevelHere(world, herd)) && PatrolStep(world, herd, mind) is { } walk) return walk;
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
                // Judgment (not in the old game, whose pack fed an hour and left the rest): a pack goes
                // back to the meat on its tile before it hunts again, so a kill feeds it for days, not one meal.
                if (MeatHere(world, herd.Tile) > 0) { Become(world, mind, HerdState.Feeding); return (new Feed(herd.Id), 1); }
                // A frightened pack (it just lost a fight) leaves prey alone.
                if (mind.Hunger > 0.4 && mind.Fear <= 0.5 && PreyHere(world, herd) is Agent prey) return (new Hunt(herd.Id, prey.Id), 1);
                // A starving pack does not go home empty: it ranges on (judgment, below).
                if (t > PackPatrolTimeoutMinutes && mind.Hunger <= StarvingHunger)
                {
                    Become(world, mind, HerdState.Resting);
                    mind.TerritoryIndex = 0;
                    return Toward(world, herd, mind, mind.Territory[0]) ?? Rest(herd);
                }
                if (mind.Hunger < 0.3) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                if (t > 0 && t % (mind.Hunger > StarvingHunger ? StarvingPatrolEveryMinutes : PackPatrolEveryMinutes) == 0)
                {
                    // Judgment (not in the old game): a hungry pack follows the scent of prey it can sense
                    // (old BaseDetectionRange) instead of walking its round, so packs and herds actually meet.
                    if (mind.Hunger > 0.6 && TowardNearest(world, herd, PreySensed(world, herd)) is { } stalk) return stalk;
                    if (mind.Hunger > StarvingHunger && Foray(world, herd, mind) is { } range) return range;
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

    /// Judgment, not in the old game (whose packs walked only their own few dozen tiles and starved
    /// beside a valley full of prey): a starving pack trots off its land toward a far point within
    /// ForayRadiusTiles of its den, and on to another when it gets there or finds no way, until it
    /// picks up a scent. Real wolves range tens of kilometres a day when hungry. Eating ends it.
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
        long t = StateMinutes(world, mind);
        switch (mind.State)
        {
            case HerdState.Resting:
                if (mind.Hunger > 0.5) Become(world, mind, HerdState.Grazing);
                return Rest(herd);
            case HerdState.Grazing: // a bear forages, omnivore that it is
                if (mind.Hunger < 0.3) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                if (Roll(world, herd, 0.01 + 0.01 * GrazedLevelHere(world, herd)) && PatrolStep(world, herd, mind) is { } walk) return walk;
                return (new Graze(herd.Id), 1);
            case HerdState.Patrolling:
                if (t > 60) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                if (Roll(world, herd, 0.01 + 0.01 * GrazedLevelHere(world, herd)) && PatrolStep(world, herd, mind) is { } prowl) return prowl;
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
                if (t % CarcassSearchEveryMinutes == 0 && TowardNearest(world, herd, MeatSensed(world, herd.Tile)) is { } go) return go;
                if (mind.Hunger < 0.3 && t > 60) { Become(world, mind, HerdState.Resting); return Rest(herd); }
                // Old hyenas also shadowed feeding predators and wolf packs; carcasses alone draw them here.
                if (Roll(world, herd, 0.04) && PatrolStep(world, herd, mind) is { } walk) return walk;
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
        return options[world.Rng("herds", herd.Id).NextInt(options.Count)];
    }

    static bool Roll(World world, Agent herd, double chance) => world.Rng("herds", herd.Id).NextDouble() < chance;

    static bool CanMove(Agent herd) => herd.Members.All(Mobile);

    /// One step on toward the tile of its land it is making for (old TryPatrolTerritory); once
    /// there, it makes for another anywhere on its land.
    /// old game: each step aimed at the next tile in flood-fill order and moved the aim on at once, so a
    /// herd shuffled among the first few tiles and a pack never left its den's doorstep (bug, fixed).
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

    /// One step toward a tile: the shortest way through its own land when there is one (old
    /// Navigation.FindRoute, kept to the territory so it stays cheap), else the shortest way over
    /// open ground (Route). Null when already there, lame, or there is no way.
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
    /// the distance to the nearest goal), or null when none turns up within MaxRouteTiles (old
    /// Navigation.FindRoute). Not a greedy step to the nearer neighbour, which a mountain between a herd
    /// and its target holds still for good; and not a shortest way inside a box around the two ends,
    /// which moves with the herd, so the next box's way can turn it back and it walks to and fro for
    /// days. On one fixed map every step along a shortest way brings it strictly nearer.
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

    /// A prey herd on this tile, not already in a fight.
    static Agent? PreyHere(World world, Agent predator) =>
        HerdsAt(world, predator.Tile).FirstOrDefault(h => h.Id != predator.Id && !IsPredator(h.Species) && Sim.IsAlive(h)
            && Combat.EncounterOf(world, h.Id) == null);

    /// Old AnimalType.BaseDetectionRange, in tiles.
    public static int DetectionRangeTiles(Species s) => s is Species.Wolf or Species.SaberTooth ? 12 : 8;

    /// Where the prey herds within the predator's senses stand.
    static HashSet<TileId> PreySensed(World world, Agent predator)
    {
        int range = DetectionRangeTiles(predator.Species);
        return world.Agents.Values
            .Where(h => h.Mind is HerdMind && !IsPredator(h.Species) && MapGen.Manhattan(h.Tile, predator.Tile) <= range)
            .Select(h => h.Tile).ToHashSet();
    }

    /// The tiles with meat on them within the scavenger's nose (old CarcassSearchRadius).
    static HashSet<TileId> MeatSensed(World world, TileId from)
    {
        var found = new HashSet<TileId>();
        for (int dx = -CarcassSearchRadius; dx <= CarcassSearchRadius; dx++)
            for (int dy = -CarcassSearchRadius; dy <= CarcassSearchRadius; dy++)
            {
                var t = new TileId(from.X + dx, from.Y + dy);
                int d = Math.Abs(dx) + Math.Abs(dy);
                if (d == 0 || d > CarcassSearchRadius) continue;
                if (world.Tiles.TryGetValue(t, out var s) && s.Carcasses != null && MeatHere(world, t) > 0) found.Add(t);
            }
        return found;
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
        double watchfulness = SightingDetectionFactor(world, hunter, herd, member);
        var animals = Combat.SendMembers(world, herd, member).Select(f => f with { DetectionFactor = watchfulness }).ToList();
        return Combat.Create(world, [new Fighter(hunter) { Weapon = weapon }], animals,
            prey.Tile, HuntStartDistanceM, Awareness.Engaged, awareness);
    }

    /// How watchful the stalked animals are, from what the herd is doing (old HuntRunner passed the
    /// sighting's DetectionModifier to RunHunt as every animal's DetectionFactor). Old
    /// HuntingSightingSelector read the animal's wounds first, then nervousness over 0.7 as alert,
    /// then its activity, and picked one sighting of that behaviour's pool at random; here the
    /// herd's state stands for the activity and its learned fear for the nervousness.
    public static double SightingDetectionFactor(World world, AgentId hunter, AgentId herd, int member)
    {
        var agent = world.Agent(herd);
        var mind = (HerdMind)agent.Mind;
        var pool = Combat.WoundLevel(agent.Members[member]) > 0.05 ? Sightings.Wounded
            : mind.State == HerdState.Fleeing || mind.Fear > 0.7 ? Sightings.Alert
            : mind.State switch
            {
                HerdState.Grazing or HerdState.Feeding => Sightings.Grazing,
                HerdState.Resting => Sightings.Resting,
                _ => Sightings.Moving, // patrolling, hunting: on the move
            };
        return pool[world.Rng("combat", hunter).NextInt(pool.Count)];
    }

    // Old HuntingSightings' DetectionModifier values, pool by pool (Content/species.json).
    static readonly SightingPools Sightings = Content.Tables.Sightings;
}

/// How watchful a stalked animal is, by what its herd is doing: one factor of the pool is picked at random.
public sealed record SightingPools(IReadOnlyList<double> Grazing, IReadOnlyList<double> Resting, IReadOnlyList<double> Wounded,
    IReadOnlyList<double> Alert, IReadOnlyList<double> Moving);
