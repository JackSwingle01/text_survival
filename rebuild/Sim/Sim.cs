using System.Text.Json.Serialization;

namespace Survival;

/// How a step ended. Finished: the activity ran its course (the minutes given ran out, or it stopped
/// itself having done what it set out to: arrived, ate, made, woke fully rested). Refused: it could
/// not start, and no time passed (or a fight round's action the fight had moved past). Interrupted:
/// something stopped it short (the fire went out, too dark, exhausted, died).
public enum StepOutcome { Finished, Refused, Interrupted }

/// What one Sim.Step did. Reason is the stop in the system's own words, for display, whatever the
/// outcome: null only for a step that finished by running the minutes it was given.
/// Work: the step's work session, ended or held, for a screen that shows what it turned up (a forage's finds).
public sealed record StepResult(List<WorldEvent> Events, int MinutesElapsed, StepOutcome Outcome, string? Reason, WorkSession? Work = null)
{
    /// Alias of Reason, whatever the outcome. Read Outcome to tell an arrival from an interruption.
    public string? InterruptedReason => Reason;
}

/// An activity under way: what an NPC or herd is doing between its mind's decisions. Saved on the
/// agent, so a load resumes it mid-way. The player's lives inside one Sim.Step, or across the steps of one
/// job when it is held between them (Sim.Step with more).
public sealed class Doing
{
    /// The activity, when it is not work: a work session carries its own, and one record held in
    /// two places would load as two.
    public Activity? Plain { get; init; }
    public WorkSession? Work { get; init; }
    [JsonIgnore] public Activity Activity => Work?.Activity ?? Plain!;
    public required int MinutesLeft { get; set; }
    /// Why a fire activity's act stops the agent after its first minute, if it does.
    public string? Acted { get; init; }
    /// Work left between two steps of one job (Sim.Step with more): the next step of the same activity carries it on.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Held { get; set; }
}

/// A non-player agent's mind turned an activity down this minute: the activity was refused. It
/// waits a minute and decides again.
public sealed record PlanRefused(long Minute, AgentId Who, TileId Tile, string Activity, string Reason) : WorldEvent(Minute, Tile);

public static class Sim
{
    /// Runs an activity minute by minute until done or interrupted. A refused activity returns at
    /// once: no time passes. Every minute it lives, every other agent with a mind of its own lives
    /// that minute too (Scheduler), so the world moves with the clock however the caller chunks its steps.
    /// more: the caller will step this same activity again, so work is held at the end of the minutes rather than
    /// ended: one search, one luck roll and one tally however many steps the job is cut into. Finish ends it.
    public static StepResult Step(World world, Activity activity, int minutes, bool more = false)
    {
        if (minutes <= 0) throw new ArgumentOutOfRangeException(nameof(minutes), minutes, "Step needs at least one minute");
        var agent = world.Agent(activity.Who);
        if (!IsAlive(agent)) throw new InvalidOperationException($"{agent.Id} is dead and cannot act");
        var events = new List<WorldEvent>();
        int filed = 0; // how far into events the tile journals have read (Journal.File)
        WorkSession? work = null;
        StepResult Result(int elapsed, StepOutcome outcome, string? reason)
        {
            Journal.File(world, events, ref filed);
            return new StepResult(events, elapsed, outcome, reason, work);
        }
        if (agent.Id == AgentId.Player && Crossed(world)) return new StepResult(events, 0, StepOutcome.Refused, RunOver);
        // A fight holds every agent with a member still in it: no walking off without fleeing.
        if (activity is not Fight && Held(world, agent.Id))
            return new StepResult(events, 0, StepOutcome.Refused, InAnEncounter);
        Doing doing;
        // The same work held from this job's last step (and nothing refusing it now) is carried on, not begun again.
        if (agent.Doing is { Held: true } held && held.Activity == activity && ActivityRefusal(world, activity) == null)
        {
            (doing, held.Held, held.MinutesLeft, agent.Doing) = (held, false, minutes, null); // the same work, carried on
        }
        else
        {
            if (agent.Doing != null) // told to do something else: what it was doing ends here
            {
                Stop(world, agent, agent.Doing, events);
                agent.Doing = null;
            }
            if (Start(world, activity, minutes, events, out doing) is { } refused) return Result(0, StepOutcome.Refused, refused.Reason);
        }
        work = doing.Work;
        bool fighting = Held(world, agent.Id); // a round, or a hunt it has just started
        agent.Stepping = activity;
        try
        {
            for (int elapsed = 1; elapsed <= minutes; elapsed++)
            {
                // A round: the action at the start of its minute (refused once the fight moved on), the rest after.
                if (activity is Fight act && Combat.Act(world, act, events) is string stale)
                {
                    Stop(world, agent, doing, events);
                    return Result(elapsed - 1, StepOutcome.Refused, stale);
                }
                world.Minute++;
                Scheduler.Minute(world, activity.Who, events);
                // Set upon this minute: the body lives it held, and nothing of the activity runs.
                var interrupt = !fighting && Held(world, agent.Id)
                    ? LiveBodies(world, agent, new Fight(agent.Id, CombatAction.Wait), events) ?? SetUpon
                    : Live(world, agent, doing, events);
                if (EndMinute(world, agent, activity, events, ref filed) && interrupt == null && activity is not Fight) interrupt = PredatorInSight;
                if (interrupt == Done && more && doing.Work != null && agent.Mind is PlayerMind)
                {
                    (doing.Held, agent.Doing) = (true, doing);
                    return Result(elapsed, StepOutcome.Finished, null);
                }
                if (interrupt != null)
                {
                    Stop(world, agent, doing, events); // death too: what was gathered is reported
                    return interrupt == Done ? Result(elapsed, StepOutcome.Finished, null)
                        : Result(elapsed, Finishes(interrupt) ? StepOutcome.Finished : StepOutcome.Interrupted, interrupt);
                }
            }
            throw new InvalidOperationException("An activity outlived the minutes it was given");
        }
        finally { agent.Stepping = null; }
    }

    /// Ends work held between steps (Step with more): what it gathered is reported, as if its last step had ended it.
    public static List<WorldEvent> Finish(World world, AgentId who)
    {
        var events = new List<WorldEvent>();
        var agent = world.Agent(who);
        if (agent.Doing is not { Held: true } held) return events;
        Stop(world, agent, held, events);
        agent.Doing = null;
        int filed = 0;
        Journal.File(world, events, ref filed);
        return events;
    }

    /// The work under way for this person, held between steps, or null: what the forage screen reads its finds from.
    public static WorkSession? WorkUnderWay(World world, AgentId who) => world.Agent(who).Doing is { Held: true, Work: { } w } ? w : null;

    /// The close of every minute the world lives: the person stepping, awake, looks about, and the
    /// minute's events go into the tile journals. True when they saw a predator come into sight or nearer.
    static bool EndMinute(World world, Agent? person, Activity activity, List<WorldEvent> events, ref int filed)
    {
        bool nearer = false;
        if (person != null && Looks(person, activity) && world.Agents.ContainsKey(person.Id))
            nearer = person.Mind is PlayerMind ? Memories.Witness(world, person, events, ref filed) : Memories.Watch(world, person, events);
        Journal.File(world, events, ref filed);
        return nearer;
    }

    public static bool Looks(Agent agent, Activity? activity) =>
        agent.Mind is PersonMind && activity is not Sleep && IsAlive(agent) && !Body.IsAsleep(agent.Body);

    public static Activity? ActivityOf(Agent agent) => agent.Doing?.Activity ?? agent.Stepping;

    public static bool InTheWorld(World world, Agent agent) => IsAlive(agent) && !(agent.Id == AgentId.Player && Crossed(world));

    /// The long-action rule: a predator seen coming into sight or nearer stops anything but a fight round.
    public const string PredatorInSight = "a predator in sight";
    /// A fight took hold of the person this minute.
    public const string SetUpon = "set upon";

    /// Ends what a person was doing, wherever they stood: what accrued is settled and a crossing under way is dropped.
    internal static void EndDoing(World world, Agent agent, List<WorldEvent> events)
    {
        if (agent.Doing is Doing d) Stop(world, agent, d, events);
        agent.Doing = null;
        agent.Crossing = null;
    }

    /// What Live returns when the minutes given ran out with nothing else to stop for.
    internal const string Done = "done";

    /// The stops that mean the activity did what it set out to: each system's own words for having
    /// finished. Every other stop is an interruption. Body.Tick's "fully rested" (a sleep that woke
    /// rested) and Combat.EndRound's "fight over" are written in those systems as literals.
    static readonly HashSet<string> Finishing =
    [
        Traversal.Arrived, Traversal.CrossedThePassReason,
        Work.Ate, Work.Drank, Work.WashedReason, Work.CookedReason, Work.Melted, Work.TreeFelled, Work.ButcheredReason, Work.WalkedAbout,
        Shelters.BuiltReason, Shelters.Packed, Shelters.AsGoodAsItGets,
        Making.MadeIt, Making.Spoiled,
        Fire.DidNotCatch, // the strike was made and failed: the attempt is over
        FullyRestedReason, EncounterOver,
    ];

    public const string FullyRestedReason = "fully rested", EncounterOver = "fight over";

    public static bool Finishes(string reason) => Finishing.Contains(reason);

    /// Why a player who has crossed the pass can do nothing more.
    public const string RunOver = "the run is over: you crossed the pass";

    /// The player has stood on the far side: the run is won and over (Map.Outcome). From then on the
    /// player is out of the world: Step refuses them and Advance runs the valley without them, so
    /// nothing can walk them back or let them die after the win.
    static bool Crossed(World world) => Map.Outcome(world) == RunOutcome.Crossed;

    /// Lets the world's clock run with nobody acting for the player: NPCs and herds live these
    /// minutes on their own minds, and a living player lives them waiting where they stand (a
    /// headless simulation, or a player who has stepped away). The world lives either way. A player
    /// in a fight waits it out round by round (Fight Wait): the fight goes on around them. A fight
    /// whose player died outside a round ends here, before the minute. A player who has crossed the
    /// pass lives no more minutes here: the valley goes on without them.
    public static List<WorldEvent> Advance(World world, int minutes)
    {
        if (minutes <= 0) throw new ArgumentOutOfRangeException(nameof(minutes), minutes, "Advance needs at least one minute");
        var events = new List<WorldEvent>();
        int filed = 0;
        // Time the player spends on nothing ends any work held between the steps of a job: it is not carried on later.
        if (world.Agents.TryGetValue(AgentId.Player, out var holder) && holder.Doing is { Held: true }) EndDoing(world, holder, events);
        for (int i = 0; i < minutes; i++)
        {
            world.Agents.TryGetValue(AgentId.Player, out var player);
            if (player != null && Crossed(world)) player = null;
            if (player != null && Combat.EncounterOf(world, player.Id) is Encounter fight)
            {
                if (!IsAlive(player)) Combat.Finish(world, fight, events);
                else
                {
                    Journal.File(world, events, ref filed);
                    var round = Step(world, new Fight(player.Id, CombatAction.Wait), 1); // files its own
                    events.AddRange(round.Events);
                    filed = events.Count;
                    if (round.MinutesElapsed != 1) throw new InvalidOperationException($"A waiting round was refused: {round.InterruptedReason}");
                    continue;
                }
            }
            world.Minute++;
            Scheduler.Minute(world, null, events);
            bool living = player != null && IsAlive(player);
            if (living) LiveBodies(world, player!, new Wait(player!.Id), events);
            EndMinute(world, living ? player : null, new Wait(AgentId.Player), events, ref filed);
        }
        Journal.File(world, events, ref filed);
        return events;
    }

    /// Whether a fight holds this agent: it has a member still in the field. A fled member is out.
    /// Asked of every animal every minute, nearly always with no fight on: loops, so that costs no object.
    internal static bool Held(World world, AgentId id)
    {
        foreach (var fight in world.Encounters)
            foreach (var unit in fight.Units)
                if (unit.Agent == id && unit.InField) return true;
        return false;
    }

    public static bool IsAlive(Agent agent)
    {
        foreach (var b in agent.Members) if (Body.IsAlive(b)) return true;
        return false;
    }

    // ---- one activity, whoever does it: start, a minute lived, stop ----

    /// Checks and begins an activity at this instant; refusal or null. Instant parts (lighting,
    /// eating, putting down) happen here, before the first minute is lived.
    internal static Refusal? Start(World world, Activity activity, int minutes, List<WorldEvent> events, out Doing doing)
    {
        var agent = world.Agent(activity.Who);
        doing = null!;
        string? acted = null;
        WorkSession? work = null;
        // Fire, work and building read the fire as it is now: its light, its heat, the pit to tend.
        if (ReadsTheFire(activity)) Fire.CatchUpAround(world, agent, events);
        if (ActivityRefusal(world, activity) is { } refused) return refused;
        if (activity is StartFire or AddFuel or LightTorch or GatherCharcoal)
            acted = Fire.Act(world, activity, events); // a failed ignition still costs its minute
        else if (activity is Travel travel) Traversal.Begin(world, travel);
        else if (Work.Handles(activity)) work = Work.Begin(world, activity, events);
        else if (Shelters.Handles(activity)) Shelters.Begin(world, activity);
        else if (Making.Handles(activity)) Making.Begin(world, activity, events);
        else if (Fauna.Handles(activity) && Fauna.Begin(world, activity, events) is int fought) minutes = fought; // a hunt lasts as long as its fight

        if (activity is not Travel) agent.Crossing = null; // turned back: it never left its tile
        doing = new Doing { Plain = work == null ? activity : null, Work = work, MinutesLeft = minutes, Acted = acted };
        return null;
    }

    /// Why this activity cannot start now, or null: the refusal Step would give, without a minute
    /// passing or anything changing. Fires around the agent must be caught up (they are after any
    /// step: every minute lived catches them up).
    public static Refusal? Refusal(World world, Activity activity)
    {
        var agent = world.Agent(activity.Who);
        if (!IsAlive(agent)) return new("dead");
        if (agent.Id == AgentId.Player && Crossed(world)) return new(RunOver);
        if (activity is not Fight && Held(world, agent.Id)) return new(InAnEncounter);
        return ActivityRefusal(world, activity);
    }

    public const string InAnEncounter = "You are in a fight.";

    public const string Asleep = "You are asleep.";

    static bool ReadsTheFire(Activity activity) =>
        activity is StartFire or AddFuel or LightTorch or GatherCharcoal || Work.Handles(activity) || Shelters.Handles(activity) || Making.Handles(activity);

    /// The owning system's refusal for this activity: the one dispatch Start and Refusal share.
    static Refusal? ActivityRefusal(World world, Activity activity) => activity switch
    {
        not (Wait or Rest or Sleep or Fight { Action: CombatAction.Wait }) when world.Agent(activity.Who) is { Mind: PersonMind } person && Body.IsAsleep(person.Body) => new(Asleep),
        StartFire or AddFuel or LightTorch or GatherCharcoal => Fire.Refusal(world, activity),
        Travel travel => Traversal.Refusal(world, travel),
        _ when Work.Handles(activity) => Work.Refusal(world, activity),
        _ when Shelters.Handles(activity) => Shelters.Refusal(world, activity),
        _ when Making.Handles(activity) => Making.Refusal(world, activity),
        Fight fight => Combat.Refusal(world, fight),
        _ when Fauna.Handles(activity) => Fauna.Refusal(world, activity),
        Wait or Rest or Sleep => null,
        _ => throw new NotSupportedException($"No step rule for activity {activity.GetType().Name}"),
    };

    /// One minute lived by the agent doing this, after world.Minute moved on. Returns why to stop:
    /// an interruption, or "done" when the minutes given ran out.
    internal static string? Live(World world, Agent agent, Doing d, List<WorldEvent> events)
    {
        var activity = d.Activity;
        string? interrupt;
        if (agent.Mind is HerdMind)
        {
            // Animals live outside the human survival model: Fauna ticks their wounds and hunger.
            interrupt = LiveBodies(world, agent, activity, events);
            // Its last member fell in a fight whose aftermath has not taken the dead yet: nobody walks or eats.
            if (interrupt == null && !IsAlive(agent)) interrupt = "died";
            if (interrupt == null && activity is Travel) interrupt = Traversal.Advance(world, agent, events);
            if (interrupt == null && Fauna.Handles(activity)) interrupt = Fauna.Minute(world, agent, activity, events);
        }
        else
        {
            interrupt = LiveBodies(world, agent, activity, events);
            // Before any of the minute runs: a fight may have held a waking activity through the drop.
            if (interrupt != "died" && activity is not (Wait or Rest or Sleep or Fight) && Body.IsAsleep(agent.Body))
                return Body.FellAsleep;
            if (activity is Travel && interrupt != "died") interrupt = Traversal.Advance(world, agent, events) ?? interrupt;
            if (d.Work != null && interrupt != "died") interrupt = Work.Minute(world, agent, d.Work, events) ?? interrupt;
            if (Shelters.Handles(activity) && interrupt != "died") interrupt = Shelters.Minute(world, agent, activity, events) ?? interrupt;
            if (Making.Handles(activity) && interrupt != "died") interrupt = Making.Minute(world, agent, activity, events) ?? interrupt;
            if (activity is Fight round) interrupt = Combat.EndRound(world, round, events) ?? interrupt; // also when the minute killed you: the fight ends
        }
        interrupt ??= d.Acted;
        if (interrupt == null && --d.MinutesLeft <= 0) interrupt = Done;
        return interrupt;
    }

    /// The agent's bodies live this minute, and nothing else: hunger, cold, wounds. Every body lives
    /// every minute exactly once, through here or a headless fight's rounds (WoundsLivedThroughMinute).
    /// A fight holds its fighters' activities, never their bodies.
    internal static string? LiveBodies(World world, Agent agent, Activity activity, List<WorldEvent> events)
    {
        if (agent.Mind is HerdMind) return Fauna.Tick(world, agent, events, activity);
        Fire.CatchUpAround(world, agent, events); // before anything reads the fire this minute
        var body = agent.Body;
        var interrupt = Body.Tick(world, agent, Surroundings(world, agent, activity, events), events);
        body.WoundsLivedThroughMinute = Math.Max(body.WoundsLivedThroughMinute, world.Minute);
        return interrupt;
    }

    internal static void Stop(World world, Agent agent, Doing d, List<WorldEvent> events)
    {
        if (d.Work != null) Work.End(world, agent, d.Work, events);
    }

    /// How an activity holds the body: its metabolic factor, how close to the fire it keeps you, whether
    /// it is sleep, whether it keeps you put long enough for a shelter to help, and how it touches the ground.
    readonly record struct Hold(double ActivityFactor, double FireProximityFactor, bool Sleeping, bool Stationary, SurfaceContact Contact);

    /// The metabolic factor an activity holds the body at (HoldOf).
    internal static double ActivityFactor(World world, TileId tile, Activity activity) => HoldOf(world, tile, activity).ActivityFactor;

    static Hold HoldOf(World world, TileId tile, Activity activity)
    {
        var gathering = activity is Gather g ? Pools.Def(world, new PoolId(tile, g.Pool)) : null;
        var (activityFactor, fireProximityFactor, sleeping, stationary) = activity switch
        {
            Sleep => (0.5, 2.0, true, true),
            Wait or Rest or StartFire or AddFuel or LightTorch or GatherCharcoal or PutDown or PickUp or Wear or TakeOff => (1.0, 2.0, false, true),
            Eat or Drink or Wash or Cook or MeltSnow => (1.0, 2.0, false, true), // eating, washing, tending the fire
            ImproveShelter or PackTent or Make or Abandon => (1.0, 2.0, false, true), // crafting
            Travel or WalkAbout => (1.5, 0.0, false, false), // walking: away from the fire's radiant heat
            // Basal only: a fight's strikes, dodges, steps and sprints pay their own work as they happen (Body.Burst,
            // Body.Cover, Body.Sprint); a flat factor would charge them twice.
            Fight => (1.0, 0.0, false, true),
            Hunt => (1.0, 0.0, false, true), // a person setting out on a stalk: its rounds are Fight activities
            Gather when gathering is TreePool => (1.8, 0.0, false, false), // chopping
            Gather when gathering is StockPool { Kind: StockKind.Detail } => (1.0, 2.0, false, true), // examining: resting
            Gather or Butcher => (1.5, 0.0, false, false), // foraging, harvesting, salvaging, butchering

            _ => throw new NotSupportedException($"No survival profile for activity {activity.GetType().Name}"),
        };
        // Idle and walking both go through the ground on foot.
        var contact = activity switch
        {
            Sleep or Rest => SurfaceContact.RestingOnGround,
            Gather when gathering is StockPool { Kind: StockKind.Detail } => SurfaceContact.RestingOnGround,
            StartFire or AddFuel or LightTorch or GatherCharcoal or Eat or Drink or Wash or Cook or MeltSnow or Butcher => SurfaceContact.WorkingOnGround,
            _ => SurfaceContact.Walking,
        };
        return new Hold(activityFactor, fireProximityFactor, sleeping, stationary, contact);
    }

    /// The radiant heat the activity takes from fires: the pit's by how close it keeps you, plus the
    /// heat a shelter holds, which rides with it (withheld as you warm to a sweat, by the body).
    static double FireBonusF(Fire.Warmth warmth, Hold hold) => warmth.PitHeatF * hold.FireProximityFactor + warmth.ShelterHeatF;

    static double BeddingF(BeddingProps? bedding, Hold hold) => hold.Sleeping ? bedding?.WarmthBonusF ?? 0 : 0;

    /// A body asleep where it dropped lives the minute as sleep, whatever it was doing.
    static Activity AsLived(Agent agent, Activity activity) => Body.IsAsleep(agent.Body) ? new Sleep(agent.Id) : activity;

    /// What the world and this activity do to the agent's body this minute.
    /// Events take a snow shelter melting away under someone keeping still (Shelters.At).
    internal static SurvivalContext Surroundings(World world, Agent agent, Activity activity, List<WorldEvent> events)
    {
        activity = AsLived(agent, activity);
        var hold = HoldOf(world, agent.Tile, activity);
        var cover = Shelters.CoverFor(world, agent.Tile, hold.Stationary, events);
        var bedding = Shelters.BeddingAt(world, agent.Tile);
        var exposure = Exposure.At(world, agent.Tile, Cover.In(cover, agent.Id), hold.Contact, Effects.Severity(agent.Body, EffectKind.Wet));
        var warmth = exposure.Warmth;
        return new SurvivalContext
        {
            AirTempF = exposure.AirF + BeddingF(bedding, hold),
            // The one wind at the body (Exposure.WindAt).
            WindLevel = exposure.WindLevel,
            FireDryingWindLevel = exposure.FireDryingWindLevel,
            Precipitation = exposure.Precipitation,
            PrecipitationPct = exposure.PrecipitationPct,
            OverheadCoverLevel = exposure.OverheadCoverLevel,
            // The shelter's held fire heat rides with the radiant heat, so it is withheld as you warm to a sweat; in the
            // air, it would sweat a sheltered sleeper by a big fire dry.
            FireBonusF = FireBonusF(warmth, hold),
            TorchHeatF = warmth.TorchHeatF,
            ClothingClo = agent.Body.ClothingClo,
            ClothingWeightKg = agent.Body.ClothingWeightKg,
            ActivityFactor = hold.ActivityFactor,
            IsSleeping = hold.Sleeping,
            GroundWettingPctPerMinute = exposure.GroundWettingPctPerMinute,
            GroundProtectionLevel = hold.Contact == SurfaceContact.RestingOnGround ? Shelters.GroundProtectionLevel(bedding) : 0,
            BeddingQualityPct = bedding?.QualityPct ?? SurvivalContext.NoBeddingQualityPct,
        };
    }

    /// What the person feels doing this here, now: the air their body lives by this minute (Surroundings'
    /// air, fire and torch heat), as its terms - the air (with the place's own cold), the wind and the
    /// sky, the shelter (when the activity keeps them still in it), the fire, a torch, bedding asleep.
    /// Terms that change nothing are left out; the rest add up to FeltF exactly. Reads only:
    /// the shelter is looked at (Shelters.CoverNow), and both airs come from the one Exposure.At. Fires
    /// around the person must be caught up (they are after any step).
    public static Feeling Feels(World world, AgentId who, Activity activity)
    {
        var agent = world.Agent(who);
        var hold = HoldOf(world, agent.Tile, AsLived(agent, activity));
        var cover = Shelters.CoverNow(world, agent.Tile, hold.Stationary);
        // No ground contact: how wet the ground makes you is not felt air, and reading it would observe
        // (catch up and store) the tile's ground.
        var e = Exposure.At(world, agent.Tile, Cover.In(cover, who));
        double openBeforeFireF = cover == default ? e.AirBeforeFireF : Exposure.At(world, agent.Tile, Cover.Open(who)).AirBeforeFireF;
        var all = new (string Factor, double DeltaF)[]
        {
            ("Air", e.SurfaceAirF),
            (e.WindLevel > 0.02 ? "Wind and sky" : "Sky", openBeforeFireF - e.SurfaceAirF),
            ("Shelter", e.AirBeforeFireF - openBeforeFireF),
            ("Fire", e.Warmth.AirHeatF + FireBonusF(e.Warmth, hold)),
            ("Torch", e.Warmth.TorchHeatF),
            ("Bedding", BeddingF(Shelters.BeddingAt(world, agent.Tile), hold)),
        };
        double felt = all.Sum(t => t.DeltaF);
        return new Feeling(felt, [.. all.Where((t, i) => i == 0 || t.DeltaF != 0)], e);
    }

    /// A forecast: the core after living these legs in turn, each an activity on a tile for some minutes, if the sky,
    /// the fires and how wet the person is stay as they are now. Reads only, as Feels does; the ground's wetting is left
    /// out, since reading it would observe the ground. Fires on every tile a leg is lived on must be caught up.
    public static double CoreAfterF(World world, AgentId who, IEnumerable<(Activity Activity, TileId Tile, int Minutes)> legs)
    {
        var agent = world.Agent(who);
        var body = agent.Body;
        double startCoreF = body.CoreTempF, startBufferPct = body.ClothingHeatBufferPct;
        try
        {
            foreach (var (activity, tile, minutes) in legs)
            {
                if (minutes <= 0) continue;
                var d = Body.ProcessTemperature(body, Forecast(world, agent, activity, tile), minutes);
                body.CoreTempF += d.CoreTempDeltaF;
                body.ClothingHeatBufferPct += d.ClothingHeatBufferDeltaPct;
            }
            return body.CoreTempF;
        }
        finally
        {
            body.CoreTempF = startCoreF;
            body.ClothingHeatBufferPct = startBufferPct;
        }
    }

    /// Surroundings for a forecast on any tile: the shelter looked at, not caught up, and no ground contact.
    static SurvivalContext Forecast(World world, Agent agent, Activity activity, TileId tile)
    {
        var hold = HoldOf(world, tile, activity);
        var cover = Shelters.CoverNow(world, tile, hold.Stationary);
        var bedding = Shelters.BeddingAt(world, tile);
        var e = Exposure.At(world, tile, Cover.In(cover, agent.Id), wetnessPct: Effects.Severity(agent.Body, EffectKind.Wet));
        return new SurvivalContext
        {
            AirTempF = e.AirF + BeddingF(bedding, hold),
            WindLevel = e.WindLevel,
            FireDryingWindLevel = e.FireDryingWindLevel,
            Precipitation = e.Precipitation,
            PrecipitationPct = e.PrecipitationPct,
            OverheadCoverLevel = e.OverheadCoverLevel,
            FireBonusF = FireBonusF(e.Warmth, hold),
            TorchHeatF = e.Warmth.TorchHeatF,
            ClothingClo = agent.Body.ClothingClo,
            ClothingWeightKg = agent.Body.ClothingWeightKg,
            ActivityFactor = hold.ActivityFactor,
            IsSleeping = hold.Sleeping,
        };
    }
}

/// The air a person feels, and what makes it (Sim.Feels). Terms: "Air" first, then each term that
/// moves it; they add up to FeltF.
public sealed record Feeling(double FeltF, IReadOnlyList<(string Factor, double DeltaF)> Terms, Exposure Exposure);

/// Moves every agent that has a mind of its own (NPCs, herds) through the world's minutes. Runs once
/// for each minute the world lives, after the clock moves, whoever's step is living it, so one long
/// player step and many short ones, or a save and load between them, give the same world.
/// Each agent runs its current activity through the same Start/Live/Stop the player's step uses.
// Every agent steps every minute. Distant ones could step hourly (at hour-aligned world minutes,
// never by when the player looked) if a populated month gets slow.
public static class Scheduler
{
    /// One minute for every scheduled agent but the one whose step is living it, in id order.
    /// An agent made this minute (a wounded animal splitting off) starts on the next one.
    public static void Minute(World world, AgentId? actor, List<WorldEvent> events)
    {
        // What lies dead goes on rotting, and herds renew toward what their land carries: settled on
        // the clock, before anyone lives the minute, so every way of stepping the world agrees.
        Remains.Hour(world);
        Fauna.Renew(world, events);
        Tracks.RecordActivity(world);
        foreach (var id in world.Agents.Keys.ToList())
        {
            if (id == actor || !world.Agents.TryGetValue(id, out var agent) || agent.Mind is PlayerMind) continue;
            // A herd's dead leave it as they fall (Fauna.Tick, the fight's aftermath): no body to examine.
            if (agent.Mind is not HerdMind && !Sim.IsAlive(agent)) continue;
            // A fight holds its fighters' activities (the player's rounds move them), not their bodies:
            // they live the minute where they stand. A fled fighter is out of it and lives on its own mind.
            if (Sim.Held(world, id)) { Sim.LiveBodies(world, agent, new Fight(id, CombatAction.Wait), events); continue; }
            if (agent.Doing == null && !Plan(world, agent, events)) continue;
            var doing = agent.Doing!;
            var stop = Sim.Live(world, agent, doing, events);
            if (stop == null && agent.Mind is NpcMind && Sim.Looks(agent, doing.Activity) && Memories.Watch(world, agent, events)) stop = Sim.PredatorInSight;
            if (stop == null) continue;
            if (world.Agents.ContainsKey(id)) Sim.Stop(world, agent, doing, events);
            // A fight's aftermath may already have given it something new to do.
            if (agent.Doing == doing) agent.Doing = null;
        }
    }

    /// The mind's next activity, started now. A person whose choice is refused waits out this
    /// minute and chooses again; a herd's mind is ours and must not ask for the impossible.
    static bool Plan(World world, Agent agent, List<WorldEvent> events)
    {
        // A person decides on the fire as it is now (light, a fire to tend), not as it was last looked at.
        if (agent.Mind is PersonMind) Fire.CatchUpAround(world, agent, events);
        // A person asleep where it dropped sleeps a minute at a time, its mind not asked (it could start a
        // hunt in deciding); once awake, slept out or woken by a blow, the mind decides again.
        var (activity, minutes) = agent.Mind is PersonMind && Body.IsAsleep(agent.Body)
            ? (new Sleep(agent.Id), 1)
            : Minds.Decide(world, agent, events);
        // Deciding may have been a fight the agent started (an NPC's hunt): it may not have survived it.
        if (!world.Agents.ContainsKey(agent.Id) || (agent.Mind is HerdMind ? agent.Members.Count == 0 : !Sim.IsAlive(agent))) return false;
        if (activity.Who != agent.Id) throw new InvalidOperationException($"{agent.Id}'s mind chose an activity for {activity.Who}");
        if (Sim.Start(world, activity, minutes, events, out var doing) is { } refused)
        {
            if (agent.Mind is HerdMind) throw new InvalidOperationException($"Herd {agent.Id} chose a refused {activity}: {refused}");
            events.Add(new PlanRefused(world.Minute, agent.Id, agent.Tile, activity.GetType().Name, refused.Reason));
            Sim.Start(world, new Wait(agent.Id), 1, events, out doing);
        }
        // Starting may have ended it: a hunt that killed every hunter.
        if (!world.Agents.ContainsKey(agent.Id) || (agent.Mind is HerdMind ? agent.Members.Count == 0 : !Sim.IsAlive(agent))) return false;
        agent.Doing ??= doing;
        return true;
    }
}

/// The one seam where minds plug in: whoever decides an agent's next activity, and for how long.
public static class Minds
{
    /// Events are for what deciding itself did: a fight an NPC chose to start is fought here.
    public static (Activity Activity, int Minutes) Decide(World world, Agent agent, List<WorldEvent> events) => agent.Mind switch
    {
        NpcMind => Npc.Decide(world, agent, events),
        HerdMind => Fauna.Decide(world, agent, events),
        PlayerMind => throw new InvalidOperationException("The player's activities come from the UI, not a mind"),
        _ => throw new NotSupportedException($"No decider for {agent.Mind.GetType().Name}"),
    };
}

