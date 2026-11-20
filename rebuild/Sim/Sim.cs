namespace Survival;

public sealed record StepResult(List<WorldEvent> Events, int MinutesElapsed, string? InterruptedReason);

public static class Sim
{
    /// Runs an activity minute by minute until done or interrupted. Mutates the world in place.
    /// The same path runs for every agent. A refused activity returns at once: no time passes.
    // ponytail: only the acting agent ticks; other agents catch up lazily once phase 3 adds them (plan decision 3).
    public static StepResult Step(World world, Activity activity, int minutes)
    {
        if (minutes <= 0) throw new ArgumentOutOfRangeException(nameof(minutes), minutes, "Step needs at least one minute");
        var agent = world.Agent(activity.Who);
        if (!Body.IsAlive(agent.Body)) throw new InvalidOperationException($"{agent.Id} is dead and cannot act");
        var events = new List<WorldEvent>();
        // A fight holds every agent with a member still in it: no walking off without fleeing.
        if (activity is not Fight && Combat.EncounterOf(world, agent.Id) is Encounter fighting && fighting.Units.Any(u => u.Agent == agent.Id && u.InField))
            return new StepResult(events, 0, "You are in a fight.");

        string? acted = null;
        WorkSession? work = null;
        if (activity is StartFire or AddFuel or LightTorch or GatherCharcoal)
        {
            Fire.CatchUpAround(world, agent, events); // the refusal reads the fire as it is now
            if (Fire.Refusal(world, activity) is string refused) return new StepResult(events, 0, refused);
            acted = Fire.Act(world, activity, events); // a failed ignition still costs its minute
        }
        else if (activity is Travel travel)
        {
            if (Traversal.Refusal(world, travel) is string refused) return new StepResult(events, 0, refused);
            Traversal.Begin(world, travel);
        }
        else if (Work.Handles(activity))
        {
            Fire.CatchUpAround(world, agent, events); // light, and the fire to cook on, as they are now
            if (Work.Refusal(world, activity) is string refused) return new StepResult(events, 0, refused);
            work = Work.Begin(world, activity, events);
        }
        else if (activity is Fight fight)
        {
            if (Combat.Refusal(world, fight) is string refused) return new StepResult(events, 0, refused);
        }
        else if (activity is not (Wait or Rest or Sleep))
            throw new NotSupportedException($"No step rule for activity {activity.GetType().Name}");

        if (activity is not Travel) agent.Crossing = null; // turned back: it never left its tile
        for (int elapsed = 1; elapsed <= minutes; elapsed++)
        {
            // A round: the action at the start of its minute (refused once the fight moved on), the rest after.
            if (activity is Fight act && Combat.Act(world, act, events) is string stale) return new StepResult(events, elapsed - 1, stale);
            world.Minute++;
            Fire.CatchUpAround(world, agent, events); // before anything reads the fire this minute
            string? interrupt = Body.Tick(world, agent, Surroundings(world, agent, activity), events);
            if (activity is Travel && interrupt != "died") interrupt = Traversal.Advance(world, agent, events) ?? interrupt;
            if (work != null && interrupt != "died") interrupt = Work.Minute(world, agent, work, events) ?? interrupt;
            if (activity is Fight round) interrupt = Combat.EndRound(world, round, events) ?? interrupt; // also when the minute killed you: the fight ends
            interrupt ??= acted;
            if (interrupt != null)
            {
                if (work != null) Work.End(world, agent, work, events); // death too: what was gathered is reported
                return new StepResult(events, elapsed, interrupt);
            }
        }
        if (work != null) Work.End(world, agent, work, events);
        return new StepResult(events, minutes, null);
    }

    /// What the world and this activity do to the agent's body this minute.
    internal static SurvivalContext Surroundings(World world, Agent agent, Activity activity)
    {
        // Every activity so far keeps the agent put beside whatever fire there is: full radiant heat.
        // Old ActivityConfig: metabolic factor and how close to the fire the activity keeps you.
        var gathering = activity is Gather g ? Pools.Def(world, new PoolId(agent.Tile, g.Pool)) : null;
        var (activityFactor, fireProximityFactor, sleeping) = activity switch
        {
            Sleep => (0.5, 2.0, true),
            Wait or Rest or StartFire or AddFuel or LightTorch or GatherCharcoal or Stash or Unstash or Wear => (1.0, 2.0, false),
            Eat or Drink or Cook or MeltSnow => (1.0, 2.0, false), // eating, tending the fire
            Travel => (1.5, 0.0, false), // walking: away from the fire's radiant heat
            Fight => (2.0, 0.0, false), // old ActivityType.Fighting
            Gather when gathering is TreePool => (1.8, 0.0, false), // chopping
            Gather when gathering is StockPool { Kind: StockKind.Detail } => (1.0, 2.0, false), // old ExamineStrategy: Resting
            Gather or Butcher => (1.5, 0.0, false), // foraging, harvesting, salvaging, butchering

            _ => throw new NotSupportedException($"No survival profile for activity {activity.GetType().Name}"),
        };
        var warmth = Fire.WarmthFor(world, agent.Id);
        var sky = Weather.At(world, world.Minute);
        // Old SurvivalContext.GroundContactFor: idle and walking both go through the ground on foot.
        var contact = activity switch
        {
            Sleep or Rest => SurfaceContact.RestingOnGround,
            Gather when gathering is StockPool { Kind: StockKind.Detail } => SurfaceContact.RestingOnGround,
            StartFire or AddFuel or LightTorch or GatherCharcoal or Eat or Drink or Cook or MeltSnow or Butcher => SurfaceContact.WorkingOnGround,
            _ => SurfaceContact.Walking,
        };
        double groundWetting = Ground.ContactWettingRate(world, agent.Tile, contact, Effects.Severity(agent.Body, EffectKind.Wet));
        return new SurvivalContext
        {
            AirTempF = Weather.AirAtF(world, agent.Tile, sky, warmth.WindProtectionLevel) + warmth.AirHeatF,
            // Old CalculateEffectiveWindSpeed: the regional wind less the pit's walls; the tile's
            // wind factor reaches the body only through the wind chill above.
            WindLevel = sky.WindLevel * (1 - warmth.WindProtectionLevel),
            Precipitation = sky.Precipitation,
            PrecipitationPct = sky.PrecipitationPct,
            OverheadCoverLevel = Map.OverheadCover(Map.Of(world), agent.Tile),
            FireBonusF = warmth.PitHeatF * fireProximityFactor,
            TorchHeatF = warmth.TorchHeatF,
            ClothingClo = agent.Body.ClothingClo,
            ClothingWeightKg = agent.Body.ClothingWeightKg,
            ActivityFactor = activityFactor,
            IsSleeping = sleeping,
            GroundWettingPctPerMinute = groundWetting,
            // ponytail: no bedding yet; old protection was 0.5 + quality * 0.4 when resting on a BeddingFeature.
            GroundProtectionLevel = 0,
        };
    }
}
