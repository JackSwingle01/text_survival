using System.Text.Json.Serialization;

namespace Survival;

public sealed record StepResult(List<WorldEvent> Events, int MinutesElapsed, string? InterruptedReason);

/// An activity under way: what an NPC or herd is doing between its mind's decisions. Saved on the
/// agent, so a load resumes it mid-way. The player's lives only inside one Sim.Step.
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
}

/// A non-player agent's mind turned an activity down this minute: the activity was refused. It
/// waits a minute and decides again.
public sealed record PlanRefused(long Minute, AgentId Who, TileId Tile, string Activity, string Reason) : WorldEvent(Minute, Tile);

public static class Sim
{
    /// Runs an activity minute by minute until done or interrupted. Mutates the world in place.
    /// The same path runs for every agent. A refused activity returns at once: no time passes.
    /// Every minute it lives, every other agent with a mind of its own lives that minute too
    /// (Scheduler), so the world moves with the clock however the caller chunks its steps.
    public static StepResult Step(World world, Activity activity, int minutes)
    {
        if (minutes <= 0) throw new ArgumentOutOfRangeException(nameof(minutes), minutes, "Step needs at least one minute");
        var agent = world.Agent(activity.Who);
        if (!IsAlive(agent)) throw new InvalidOperationException($"{agent.Id} is dead and cannot act");
        var events = new List<WorldEvent>();
        int filed = 0; // how far into events the tile journals have read (Journal.File)
        StepResult Result(int elapsed, string? reason)
        {
            Journal.File(world, events, ref filed);
            return new StepResult(events, elapsed, reason);
        }
        if (agent.Id == AgentId.Player && Crossed(world)) return new StepResult(events, 0, RunOver);
        // A fight holds every agent with a member still in it: no walking off without fleeing.
        if (activity is not Fight && Held(world, agent.Id))
            return new StepResult(events, 0, InAFight);
        if (agent.Doing != null) // told to do something else: what it was doing ends here
        {
            Stop(world, agent, agent.Doing, events);
            agent.Doing = null;
        }
        if (Start(world, activity, minutes, events, out var doing) is string refused) return Result(0, refused);

        for (int elapsed = 1; elapsed <= minutes; elapsed++)
        {
            // A round: the action at the start of its minute (refused once the fight moved on), the rest after.
            if (activity is Fight act && Combat.Act(world, act, events) is string stale)
            {
                Stop(world, agent, doing, events);
                return Result(elapsed - 1, stale);
            }
            world.Minute++;
            Scheduler.Minute(world, activity.Who, events);
            var interrupt = Live(world, agent, doing, events);
            EndMinute(world, agent, activity, events, ref filed);
            if (interrupt != null)
            {
                Stop(world, agent, doing, events); // death too: what was gathered is reported
                return Result(elapsed, interrupt == Done ? null : interrupt);
            }
        }
        throw new InvalidOperationException("An activity outlived the minutes it was given");
    }

    /// The close of every minute the world lives: the player, awake, takes in what is around them
    /// (Knowledge.Witness), and the minute's events go into the tile journals. Asleep is away.
    static void EndMinute(World world, Agent? player, Activity activity, List<WorldEvent> events, ref int filed)
    {
        if (player is { Mind: PlayerMind } && activity is not Sleep && IsAlive(player) && world.Agents.ContainsKey(player.Id))
            Knowledge.Witness(world, player, events, ref filed);
        Journal.File(world, events, ref filed);
    }

    /// What Live returns when the minutes given ran out with nothing else to stop for.
    internal const string Done = "done";

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
    internal static bool Held(World world, AgentId id) =>
        world.Encounters.Count > 0 && world.Encounters.Any(e => e.Units.Any(u => u.Agent == id && u.InField));

    /// Whether any body of the agent still lives.
    public static bool IsAlive(Agent agent) => agent.Members.Any(Body.IsAlive);

    // ---- one activity, whoever does it: start, a minute lived, stop ----

    /// Checks and begins an activity at this instant; refusal or null. Instant parts (lighting,
    /// eating, stashing) happen here, before the first minute is lived.
    internal static string? Start(World world, Activity activity, int minutes, List<WorldEvent> events, out Doing doing)
    {
        var agent = world.Agent(activity.Who);
        doing = null!;
        string? acted = null;
        WorkSession? work = null;
        // Fire, work and building read the fire as it is now: its light, its heat, the pit to tend.
        if (ReadsTheFire(activity)) Fire.CatchUpAround(world, agent, events);
        if (ActivityRefusal(world, activity) is string refused) return refused;
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
    public static string? Refusal(World world, Activity activity)
    {
        var agent = world.Agent(activity.Who);
        if (!IsAlive(agent)) return "dead";
        if (agent.Id == AgentId.Player && Crossed(world)) return RunOver;
        if (activity is not Fight && Held(world, agent.Id)) return InAFight;
        return ActivityRefusal(world, activity);
    }

    public const string InAFight = "You are in a fight.";

    static bool ReadsTheFire(Activity activity) =>
        activity is StartFire or AddFuel or LightTorch or GatherCharcoal || Work.Handles(activity) || Shelters.Handles(activity) || Making.Handles(activity);

    /// The owning system's refusal for this activity: the one dispatch Start and Refusal share.
    static string? ActivityRefusal(World world, Activity activity) => activity switch
    {
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
            // Animals live outside the human survival model (plan decision 2): Fauna ticks their wounds and hunger.
            interrupt = LiveBodies(world, agent, activity, events);
            if (interrupt == null && activity is Travel) interrupt = Traversal.Advance(world, agent, events);
            if (interrupt == null && Fauna.Handles(activity)) interrupt = Fauna.Minute(world, agent, activity, events);
        }
        else
        {
            interrupt = LiveBodies(world, agent, activity, events);
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
        if (agent.Mind is HerdMind) return Fauna.Tick(world, agent, events);
        Fire.CatchUpAround(world, agent, events); // before anything reads the fire this minute
        var body = agent.Body;
        var interrupt = Body.Tick(world, agent, Surroundings(world, agent, activity, events), events);
        body.WoundsLivedThroughMinute = Math.Max(body.WoundsLivedThroughMinute, world.Minute);
        return interrupt;
    }

    /// The activity is over, however it ended: what accrued to it is settled.
    internal static void Stop(World world, Agent agent, Doing d, List<WorldEvent> events)
    {
        if (d.Work != null) Work.End(world, agent, d.Work, events);
    }

    /// What the world and this activity do to the agent's body this minute.
    /// Events take a snow shelter melting away under someone keeping still (Shelters.At).
    internal static SurvivalContext Surroundings(World world, Agent agent, Activity activity, List<WorldEvent> events)
    {
        // Every activity so far keeps the agent put beside whatever fire there is: full radiant heat.
        // Old ActivityConfig: metabolic factor and how close to the fire the activity keeps you.
        var gathering = activity is Gather g ? Pools.Def(world, new PoolId(agent.Tile, g.Pool)) : null;
        // Stationary: whether it keeps you put long enough for a shelter to help (old ActivityConfig.IsStationary).
        var (activityFactor, fireProximityFactor, sleeping, stationary) = activity switch
        {
            Sleep => (0.5, 2.0, true, true),
            Wait or Rest or StartFire or AddFuel or LightTorch or GatherCharcoal or Stash or Unstash or Wear => (1.0, 2.0, false, true),
            Eat or Drink or Cook or MeltSnow => (1.0, 2.0, false, true), // eating, tending the fire
            Build or ImproveShelter or PackTent or Make or Abandon => (1.0, 2.0, false, true), // old ActivityType.Crafting
            Travel => (1.5, 0.0, false, false), // walking: away from the fire's radiant heat
            Fight => (2.0, 0.0, false, true), // old ActivityType.Fighting, which was stationary
            Gather when gathering is TreePool => (1.8, 0.0, false, false), // chopping
            Gather when gathering is StockPool { Kind: StockKind.Detail } => (1.0, 2.0, false, true), // old ExamineStrategy: Resting
            Gather or Butcher => (1.5, 0.0, false, false), // foraging, harvesting, salvaging, butchering

            _ => throw new NotSupportedException($"No survival profile for activity {activity.GetType().Name}"),
        };
        var cover = Shelters.CoverFor(world, agent.Tile, stationary, events);
        var bedding = Shelters.BeddingAt(world, agent.Tile);
        // Old SurvivalContext.GroundContactFor: idle and walking both go through the ground on foot.
        var contact = activity switch
        {
            Sleep or Rest => SurfaceContact.RestingOnGround,
            Gather when gathering is StockPool { Kind: StockKind.Detail } => SurfaceContact.RestingOnGround,
            StartFire or AddFuel or LightTorch or GatherCharcoal or Eat or Drink or Cook or MeltSnow or Butcher => SurfaceContact.WorkingOnGround,
            _ => SurfaceContact.Walking,
        };
        var exposure = Exposure.At(world, agent.Tile, Cover.In(cover, agent.Id), contact, Effects.Severity(agent.Body, EffectKind.Wet));
        var warmth = exposure.Warmth;
        return new SurvivalContext
        {
            // Bedding's warmth is felt asleep in it (old WarmthBonus: authored, never read - bug, fixed).
            AirTempF = exposure.AirF + (sleeping ? bedding?.WarmthBonusF ?? 0 : 0),
            // The one wind at the body (Exposure.WindAt): the wind chill in AirTempF read it too.
            WindLevel = exposure.WindLevel,
            FireDryingWindLevel = exposure.FireDryingWindLevel,
            Precipitation = exposure.Precipitation,
            PrecipitationPct = exposure.PrecipitationPct,
            OverheadCoverLevel = exposure.OverheadCoverLevel,
            // The shelter's held fire heat rides with the radiant heat: withheld once you sweat (old: it was
            // in the air, so a sheltered sleeper by a big fire sweated through every litre - bug, fixed).
            FireBonusF = warmth.PitHeatF * fireProximityFactor + warmth.ShelterHeatF,
            TorchHeatF = warmth.TorchHeatF,
            ClothingClo = agent.Body.ClothingClo,
            ClothingWeightKg = agent.Body.ClothingWeightKg,
            ActivityFactor = activityFactor,
            IsSleeping = sleeping,
            GroundWettingPctPerMinute = exposure.GroundWettingPctPerMinute,
            GroundProtectionLevel = contact == SurfaceContact.RestingOnGround ? Shelters.GroundProtectionLevel(bedding) : 0,
            BeddingQualityPct = bedding?.QualityPct ?? SurvivalContext.NoBeddingQualityPct,
        };
    }
}

/// Moves every agent that has a mind of its own (NPCs, herds) through the world's minutes. Runs once
/// for each minute the world lives, after the clock moves, whoever's step is living it, so one long
/// player step and many short ones, or a save and load between them, give the same world.
/// Each agent runs its current activity through the same Start/Live/Stop the player's step uses;
/// when the activity ends or is interrupted its mind (Minds.Decide) picks the next one.
// Every agent steps every minute. Plan decision 3 lets distant ones step hourly; do that
// (at hour-aligned world minutes, never by when the player looked) if a populated month gets slow.
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
            if (Sim.Live(world, agent, doing, events) == null) continue;
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
        var (activity, minutes) = Minds.Decide(world, agent, events);
        // Deciding may have been a fight the agent started (an NPC's hunt): it may not have survived it.
        if (!world.Agents.ContainsKey(agent.Id) || (agent.Mind is HerdMind ? agent.Members.Count == 0 : !Sim.IsAlive(agent))) return false;
        if (activity.Who != agent.Id) throw new InvalidOperationException($"{agent.Id}'s mind chose an activity for {activity.Who}");
        if (Sim.Start(world, activity, minutes, events, out var doing) is string refused)
        {
            if (agent.Mind is HerdMind) throw new InvalidOperationException($"Herd {agent.Id} chose a refused {activity}: {refused}");
            events.Add(new PlanRefused(world.Minute, agent.Id, agent.Tile, activity.GetType().Name, refused));
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
        HerdMind => Fauna.Decide(world, agent),
        PlayerMind => throw new InvalidOperationException("The player's activities come from the UI, not a mind"),
        _ => throw new NotSupportedException($"No decider for {agent.Mind.GetType().Name}"),
    };
}
