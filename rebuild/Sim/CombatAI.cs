namespace Survival;

/// Where a fighter stands toward its target (combat grilling round 1, 2026-11-21). The one thing a fighter
/// stores about how it is fighting; everything else is derived. Down (dead, or consciousness under 0.1)
/// and Gone (left the field alive) are read off the body and the field (Combat.StateOf), never stored.
/// Clash is Pressing with the target in reach.
public enum FighterState { Unaware, Wary, Pressing, Standoff, GivingGround, Fleeing, Gone, Down }

/// What moves a fighter's morale. Wounds are not an event: they cost morale through vitality and lost
/// blood for as long as they last (Combat.HealthPenalty).
public enum MoraleEvent { DealtDamage, AllyKilled, AllyFled, Intimidated }

/// The fighter state machine, morale and the AI's turn (combat grilling rounds 1 and 2). Any unit the
/// player is not driving takes its turn here, animal or person.
///
/// A fighter's margin toward its target is its boldness, less fire's deterrence, less the threat it
/// sees in the target. The margin moves it between bands, each with its own way in and out
/// (hysteresis), so a fighter that has committed stays committed until the margin really turns:
/// Pressing from +0.2, back to Standoff below 0; Giving ground below -0.3, back above -0.1; Fleeing
/// below -0.8, back above -0.4. An Unaware fighter becomes Wary when its alertness reaches 0.3 (and
/// Unaware again below 0.1), and engages at 0.7. A fighter's actions are its state's, within what the
/// distance allows (Combat.Refusal); the AI takes the first of its state's priority list.
public static partial class Combat
{
    // ---- the bands (starting values, to be tuned) ----
    public const double PressingEnterMargin = 0.2, PressingLeaveMargin = 0.0;
    public const double GivingGroundEnterMargin = -0.3, GivingGroundRecoverMargin = -0.1;
    public const double FleeingEnterMargin = -0.8, FleeingRecoverMargin = -0.4;
    public const double WaryAlertnessLevel = 0.3, UnawareAlertnessLevel = 0.1, EngagedAlertnessLevel = 0.7;
    /// Alertness one notice adds, and what a look that notices nothing takes away.
    public const double NoticeAlertnessDelta = 0.35, FadeAlertnessDelta = 0.1;

    // ---- morale (starting values) ----
    const double DealtDamageMorale = 0.15, ChaseInstinctMorale = 0.3, TargetGivingGroundMorale = 0.05,
        TargetPressingMoralePerThreat = 0.05, StandoffCostMorale = 0.02, AllyKilledMorale = -2.0, AllyFledMorale = -1.0;
    const double ThreatDropPerM = 0.07;

    // ---- the AI's choices ----
    /// A person throws only at this hit chance or better (round 2), and shouts only at this chance or better.
    public const double PersonThrowMinPct = 0.5, IntimidateMinPct = 0.5;
    /// A target nearer by a metre is worth this much margin; a better target must beat the current one by
    /// SwitchGainMargin before turning to it is worth the round it costs.
    const double ClosenessMarginPerM = 0.02, SwitchGainMargin = 0.1;
    const int WanderM = 2;

    // ---- morale ----

    /// How dangerous this fighter is right now: its species' threat, scaled by how alive it is.
    public static double Threat(World world, CombatUnit u) => StatsOf(world, u).Threat * Body.Vitality(BodyOf(world, u));

    /// Courage now: the species' own, its allies' strength and what has happened in the fight, less what
    /// its wounds cost it. Old: plus an opening optimism of up to +1.0 that one wound wiped out (deleted,
    /// round 2: the bands' hysteresis does its job).
    public static double UnitBoldness(World world, Encounter enc, CombatUnit u) =>
        Courage(StatsOf(world, u), u.BoldnessModifier, BodyOf(world, u)) + AllyBonus(world, enc, u);

    /// A fighter's own courage: its kind's, what has moved it so far, less its wounds. UnitBoldness adds its allies.
    static double Courage(SpeciesStats stats, double boldnessModifier, BodyState body) => stats.Boldness + boldnessModifier - HealthPenalty(body);

    /// What its wounds cost a fighter's courage: how far from alive it is, and the blood it has lost
    /// (round 2: derived from vitality and blood, no number per hit). Old: 1.5 for the worst region
    /// plus 0.3 while bleeding, so one spear hit cost about 1.8 and turned anything.
    public static double HealthPenalty(BodyState body) => (1 - Body.Vitality(body)) + 0.5 * (1 - body.BloodPct);

    /// How much this fighter trusts its side.
    // Old NPCs scaled cohesion by their opinion of each ally; nothing writes opinions yet (Npc Opinions).
    static double Cohesion(World world, CombatUnit u) => StatsOf(world, u).Cohesion;

    /// Threat seen from here: falls off 7% a metre, so nothing past 14 m frightens.
    public static double PerceivedThreat(World world, CombatUnit u, CombatUnit other) =>
        PerceivedThreat(Threat(world, other), u.Position.DistanceTo(other.Position));

    static double PerceivedThreat(double threat, double distanceM) => Math.Max(0, (1 - distanceM * ThreatDropPerM) * threat);

    /// Up to 15% of each ally's threat, as far as this fighter trusts it.
    static double AllyBonus(World world, Encounter enc, CombatUnit u) =>
        AllyBonus(Cohesion(world, u), Allies(enc, u).Select(a => Threat(world, a)));

    static double AllyBonus(double cohesion, IEnumerable<double> allyThreats) => 0.15 * allyThreats.Sum(t => Math.Max(0, t * cohesion));

    // ---- fire ----

    /// What flame does to a fighter's nerve: fire-fear × each flame's intensity × the
    /// falloff from where the fighter stands, min(1, (FullWithinM / d)²). The one formula: the grid
    /// measures d from the pit's cell or the torch-bearer, the map from the person the predator would
    /// come for. Fire-fear is one value for every animal; people are not deterred.
    public static double FireDeterrence(SpeciesStats stats, IEnumerable<(double IntensityPct, double DistanceM)> flames)
    {
        if (!stats.IsAnimal) return 0;
        var d = Fire.Deterrence;
        return flames.Sum(f => d.FireFear * f.IntensityPct * (f.DistanceM <= d.FullWithinM ? 1 : Math.Pow(d.FullWithinM / f.DistanceM, 2)));
    }

    /// Pits stand at Encounter.FireCell, torches with their bearers; a torch out of the fight has no place on the grid.
    static double FireDeterrence(World world, Encounter enc, CombatUnit u)
    {
        if (!StatsOf(world, u).IsAnimal || world.Fires.Count == 0) return 0;
        var flames = new List<(double, double)>();
        foreach (var f in Fire.FlamesOn(world, enc.Tile))
        {
            var at = f.Holder is AgentId holder ? Field(enc).FirstOrDefault(o => o.Agent == holder)?.Position : enc.FireCell;
            if (at is Cell c) flames.Add((f.IntensityPct, u.Position.DistanceTo(c)));
        }
        return FireDeterrence(StatsOf(world, u), flames);
    }

    /// Brings the flames a fight reads up to this minute: once a round, before anyone weighs them.
    static void CatchUpFlames(World world, Encounter enc, List<WorldEvent> events) => Fire.CatchUpOn(world, enc.Tile, events);

    /// Boldness, less fire's deterrence, less the threat seen in the other and in whoever else is
    /// pressing it: the one number the bands read.
    public static double Margin(World world, Encounter enc, CombatUnit u, CombatUnit other) =>
        UnitBoldness(world, enc, u) - FireDeterrence(world, enc, u) - PerceivedThreat(world, u, other) - Pressure(world, enc, u, other);

    /// Half the threat of every other enemy pressing in on this fighter within 5 m (round 6): a pack
    /// round you weighs on you even while you face one of it.
    static double Pressure(World world, Encounter enc, CombatUnit u, CombatUnit facing) =>
        OtherPressureFactor * Enemies(enc, u).Where(e => e != facing && StateOf(world, e) == FighterState.Pressing && TargetOf(enc, e) == u
            && e.Position.DistanceTo(u.Position) <= PressureRangeM).Sum(e => Threat(world, e));

    const double OtherPressureFactor = 0.5, PressureRangeM = 5;

    static void Morale(World world, Encounter enc, CombatUnit u, MoraleEvent evt, CombatUnit other)
    {
        u.BoldnessModifier += evt switch
        {
            MoraleEvent.DealtDamage => DealtDamageMorale,
            MoraleEvent.AllyKilled => AllyKilledMorale * Cohesion(world, u),
            MoraleEvent.AllyFled => AllyFledMorale * Cohesion(world, u),
            MoraleEvent.Intimidated => -Intimidation(world, u, other),
            _ => throw new ArgumentOutOfRangeException(nameof(evt), evt, "Unknown morale event"),
        };
    }

    /// Once a round, what the target is doing tells on the fighter's nerve: a target giving ground
    /// emboldens, one pressing in daunts by its threat, and a standoff wears a fighter down, so a held
    /// animal tests and then breaks off.
    static void RoundMorale(World world, Encounter enc, CombatUnit u)
    {
        if (TargetOf(enc, u) is not CombatUnit target) return;
        u.BoldnessModifier += StateOf(world, target) switch
        {
            FighterState.GivingGround => TargetGivingGroundMorale,
            FighterState.Pressing => -TargetPressingMoralePerThreat * Threat(world, target),
            _ => 0,
        };
        if (u.State == FighterState.Standoff) u.BoldnessModifier -= StandoffCostMorale;
    }

    /// A shout and a show: works better from a healthy shouter on a weakened target. A weapon does not
    /// come into it: animals do not fear weapons, they learn through injury (round 2).
    static double Intimidation(World world, CombatUnit target, CombatUnit shouter) =>
        world.Rng("combat", shouter.Agent).NextDouble() < IntimidationChance(world, target, shouter) ? 0.3 : 0.05;

    public static double IntimidationChance(World world, CombatUnit target, CombatUnit shouter) =>
        0.3 + Body.Vitality(BodyOf(world, shouter)) * 0.2 + (Body.Vitality(BodyOf(world, target)) < 0.5 ? 0.15 : 0);

    // ---- the state ----

    /// The fighter's state: Down and Gone read off the body and the field, the rest as stored.
    public static FighterState StateOf(World world, CombatUnit u) =>
        !u.InField ? (Alive(world, u) ? FighterState.Gone : FighterState.Down)
        : IsDown(world, u) ? FighterState.Down
        : u.State;

    /// Dead, or too far gone to know where it is.
    static bool IsDown(World world, CombatUnit u)
    {
        var body = BodyOf(world, u);
        return !Body.IsAlive(body) || Body.GetCapacities(body).Consciousness < 0.1;
    }

    static bool IsAware(FighterState s) => s is FighterState.Pressing or FighterState.Standoff or FighterState.GivingGround or FighterState.Fleeing;

    /// Moves a fighter into a state. Turning to run sets off every predator that has it as its target:
    /// the chase instinct.
    internal static void Enter(World world, Encounter enc, CombatUnit u, FighterState state)
    {
        if (state is FighterState.Gone or FighterState.Down) throw new ArgumentException($"{state} is read off the field and the body, not entered");
        if (u.State == state) return;
        u.State = state;
        if (state != FighterState.Fleeing || u.HasRun) return;
        u.HasRun = true; // the chase instinct fires on the first turn to run, not on every wavering
        foreach (var e in Enemies(enc, u).Where(e => TargetOf(enc, e) == u && StatsOf(world, e).Role == Role.Predator).ToList())
            e.BoldnessModifier += ChaseInstinctMorale;
    }

    /// Becoming aware of the fight: skittish animals run (round 1); anything else stands and weighs it up.
    static void Engage(World world, Encounter enc, CombatUnit u)
    {
        u.AlertnessLevel = 1;
        if (!IsAware(u.State)) Enter(world, enc, u, StatsOf(world, u).IsSkittish ? FighterState.Fleeing : FighterState.Standoff);
    }

    /// Alertness moved: the awareness bands, each with its own way in and out.
    static void Alert(World world, Encounter enc, CombatUnit u, double delta)
    {
        if (IsAware(u.State)) return;
        u.AlertnessLevel = Math.Clamp(u.AlertnessLevel + delta, 0, 1);
        if (u.AlertnessLevel >= EngagedAlertnessLevel) Engage(world, enc, u);
        else if (u.State == FighterState.Unaware && u.AlertnessLevel >= WaryAlertnessLevel) Enter(world, enc, u, FighterState.Wary);
        else if (u.State == FighterState.Wary && u.AlertnessLevel < UnawareAlertnessLevel) Enter(world, enc, u, FighterState.Unaware);
    }

    /// The margin bands, for an aware fighter the AI drives. Skittish animals always flee once engaged.
    static void UpdateState(World world, Encounter enc, CombatUnit u)
    {
        if (!IsAware(u.State)) return;
        if (StatsOf(world, u).IsSkittish) { Enter(world, enc, u, FighterState.Fleeing); return; }
        if (TargetOf(enc, u) is not CombatUnit target) return;
        var next = NextState(u.State, Margin(world, enc, u, target));
        // A person runs only from what it can outrun (under a 50% chance of being caught); else it gives ground.
        if (next == FighterState.Fleeing && !StatsOf(world, u).IsAnimal && BestCatchPct(world, enc, u) >= PersonRunsBelowPct)
            next = FighterState.GivingGround;
        Enter(world, enc, u, next);
    }

    /// Where its resolve takes a fighter from the state it is in.
    public static FighterState NextState(FighterState state, double resolve)
    {
        if (state == FighterState.Fleeing) return resolve > FleeingRecoverMargin ? FighterState.GivingGround : FighterState.Fleeing;
        if (resolve < FleeingEnterMargin) return FighterState.Fleeing;
        return state switch
        {
            FighterState.Pressing => resolve < GivingGroundEnterMargin ? FighterState.GivingGround
                : resolve < PressingLeaveMargin ? FighterState.Standoff : FighterState.Pressing,
            FighterState.GivingGround => resolve >= PressingEnterMargin ? FighterState.Pressing
                : resolve > GivingGroundRecoverMargin ? FighterState.Standoff : FighterState.GivingGround,
            FighterState.Standoff => resolve >= PressingEnterMargin ? FighterState.Pressing
                : resolve < GivingGroundEnterMargin ? FighterState.GivingGround : FighterState.Standoff,
            _ => state,
        };
    }

    /// The state the player's own action puts them in (round 1: they have no thresholds of their own).
    /// An ambushed player who acts at all is in the fight from then on; one who keeps still stays unaware.
    static void PlayerState(World world, Encounter enc, CombatUnit u, CombatAction action, Cell before)
    {
        if (!u.InField) return;
        if (!IsAware(u.State))
        {
            if (action == CombatAction.Wait) return; // keeping still, still unaware
            Engage(world, enc, u); // acting at all, they know the fight is on
        }
        var target = TargetOf(enc, u);
        double closer = target == null ? 0 : before.DistanceTo(target.Position) - u.Position.DistanceTo(target.Position);
        var state = action switch
        {
            CombatAction.Advance or CombatAction.Attack or CombatAction.Throw or CombatAction.ThrowStone or CombatAction.Shove => FighterState.Pressing,
            CombatAction.Retreat => FighterState.GivingGround,
            CombatAction.Move => closer >= 1 ? FighterState.Pressing : closer <= -1 ? FighterState.GivingGround : FighterState.Standoff,
            CombatAction.Turn => u.State,
            CombatAction.Flee => FighterState.Fleeing, // run down: no defence this round
            CombatAction.Chase => FighterState.Pressing,
            _ => FighterState.Standoff, // Wait, Intimidate, PickUp, the calls
        };
        Enter(world, enc, u, state);
    }

    /// What a state allows, before the distance has its say (round 1). The player's state follows from
    /// their action, so only fighters the AI drives are held to this.
    // Doing nothing is open to anyone: a cornered runner with no step to take has only that.
    static bool StateAllows(FighterState state, CombatAction action) => action is CombatAction.Turn or CombatAction.Wait || state switch
    {
        FighterState.Unaware => action is CombatAction.Wait or CombatAction.Move,
        FighterState.Wary => action is CombatAction.Wait or CombatAction.Move or CombatAction.Retreat or CombatAction.Flee,
        FighterState.Pressing => action is not CombatAction.Retreat,
        FighterState.Standoff => action is not CombatAction.Advance,
        FighterState.GivingGround => action is CombatAction.Retreat or CombatAction.Attack or CombatAction.Intimidate or CombatAction.CallHelp
            or CombatAction.CallRetreat or CombatAction.Flee or CombatAction.PickUp or CombatAction.Wait or CombatAction.Move,
        FighterState.Fleeing => action is CombatAction.Flee or CombatAction.Move or CombatAction.Retreat,
        _ => action == CombatAction.Wait,
    };

    /// Where a fighter would stand toward the strongest of a herd at this distance, before any fight:
    /// the same margin and bands a fight runs on (an NPC deciding whether to stand or run, round 2).
    /// No allies yet and no morale; the fighter's boldness delta is its own nature.
    public static FighterState WouldStand(World world, Fighter me, Agent threat, double distanceM)
    {
        var them = SpeciesTable.Of(threat.Species);
        var living = threat.Members.Where(Body.IsAlive).ToList();
        if (living.Count == 0) throw new ArgumentException($"{threat.Id} has nobody alive to stand against");
        double worst = living.Max(m => them.Threat * Body.Vitality(m));
        // Margin's terms, before the fight: no allies, no morale yet, and fire deters no person.
        double margin = Courage(SpeciesTable.Of(world.Agent(me.Agent).Species), me.BoldnessDelta, world.Agent(me.Agent).Members[me.Member])
            - PerceivedThreat(worst, distanceM);
        return NextState(FighterState.Standoff, margin);
    }

    /// The margin a herd would have toward the people on a tile were it at their position (the hold):
    /// Margin's terms before any fight, and it must follow Margin when Margin changes. Its boldest member leads, its other members are its
    /// allies, the person faced is the one it has the best margin against, every other person there presses
    /// on it, and every flame on the tile is beside them. Null when nobody alive stands there. The tile's
    /// flames must be caught up (Fire.CatchUpOn).
    public static double? MarginOnTile(World world, Agent herd, TileId tile)
    {
        var people = Nearby.At(world, tile).Select(world.Agent).Where(a => a.Mind is PersonMind && Sim.IsAlive(a)).ToList();
        if (people.Count == 0) return null;
        var stats = SpeciesTable.Of(herd.Species);
        var living = herd.Members.Where(Body.IsAlive).ToList();
        if (living.Count == 0) throw new ArgumentException($"{herd.Id} has nobody alive to come on");
        int lead = Enumerable.Range(0, living.Count).MaxBy(i => Courage(stats, 0, living[i]));
        double nerve = Courage(stats, 0, living[lead]) + AllyBonus(stats.Cohesion, living.Where((_, i) => i != lead).Select(m => stats.Threat * Body.Vitality(m)));
        double deterrence = FireDeterrence(stats, Fire.FlamesOn(world, tile).Select(f => (f.IntensityPct, 0.0)));
        var threats = people.Select(p => SpeciesTable.Of(p.Species).Threat * Body.Vitality(p.Body)).ToList();
        double all = threats.Sum();
        return threats.Max(t => nerve - deterrence - PerceivedThreat(t, 0) - OtherPressureFactor * (all - t));
    }

    /// A herd steps onto a tile with people only if its margin there, fire included, reaches the Pressing
    /// entry; short of that it holds where it is until the fire dies or the person steps away.
    public static bool WouldEnter(World world, Agent herd, TileId tile) =>
        MarginOnTile(world, herd, tile) is not double margin || margin >= PressingEnterMargin;

    // ---- targets ----

    /// Every enemy this fighter could face, nearest first: its index (Fight.Target), how far, whether it is
    /// the one faced now, and why turning to it would be refused (null when it would not). Turning costs the round.
    public static IReadOnlyList<CombatTarget> Targets(World world, Encounter enc, CombatUnit u) =>
        Enemies(enc, u).OrderBy(e => u.Position.DistanceTo(e.Position)).ThenBy(e => enc.Units.IndexOf(e))
            .Select(e => new CombatTarget(enc.Units.IndexOf(e), e.Agent, e.Member, u.Position.DistanceTo(e.Position), TargetOf(enc, u) == e,
                TargetOf(enc, u) == e ? null : Refusal(world, enc, u, CombatAction.Turn, e)))
            .ToList();

    /// The fighter this one faces, or null once it has left the field. A target index that names no unit is a bug.
    public static CombatUnit? TargetOf(Encounter enc, CombatUnit u)
    {
        if (u.Target is not int i) return null;
        if (i < 0 || i >= enc.Units.Count) throw new InvalidOperationException($"{u.Agent} faces unit {i}, but the fight has {enc.Units.Count}");
        return enc.Units[i] is { InField: true } t && t.Team != u.Team ? t : null;
    }

    /// How much a fighter wants this target, weighted toward closer ones (round 1): a predator the most
    /// vulnerable (the best margin), anything else the biggest threat to it.
    static double TargetScore(World world, Encounter enc, CombatUnit u, CombatUnit other)
    {
        double want = StatsOf(world, u).Role == Role.Predator ? Margin(world, enc, u, other) : PerceivedThreat(world, u, other);
        return want - ClosenessMarginPerM * u.Position.DistanceTo(other.Position);
    }

    public static CombatUnit? BestTarget(World world, Encounter enc, CombatUnit u) =>
        Enemies(enc, u).OrderByDescending(e => TargetScore(world, enc, u, e)).ThenBy(e => enc.Units.IndexOf(e)).FirstOrDefault();

    /// A fighter whose target has left the field faces the best one left at once; turning from a target
    /// still there to a better one is an action, and costs the round (round 1).
    static void ReTarget(World world, Encounter enc, CombatUnit u)
    {
        if (TargetOf(enc, u) == null && BestTarget(world, enc, u) is CombatUnit best) u.Target = enc.Units.IndexOf(best);
    }

    // ---- steering ----

    /// Tactical metres per action, cut as the body's speed is (RunningOf: wounds, tiredness, load): a
    /// crippled animal can be run down and a mauled or laden person cannot sprint. Never more than
    /// the species' own. Old: cut by Moving alone, so a load never slowed anyone in a fight.
    public static int Allowance(World world, CombatUnit u)
    {
        double speedPct = Math.Clamp(RunningOf(world, u).SpeedPct, 0, 1);
        return Math.Max(1, (int)Math.Round(StatsOf(world, u).MoveAllowanceM * speedPct));
    }

    /// The reachable cell closest to the target within the allowance, never through an occupied
    /// cell or between two diagonally.
    public static Cell Pursue(World world, Encounter enc, CombatUnit u, CombatUnit target)
    {
        var occupied = Field(enc).Where(o => o != u && Alive(world, o)).Select(o => o.Position).ToHashSet();
        var costs = new Dictionary<Cell, double> { [u.Position] = 0 };
        var queue = new PriorityQueue<Cell, double>();
        queue.Enqueue(u.Position, 0);
        double budget = Allowance(world, u);
        while (queue.TryDequeue(out var current, out double cost))
        {
            if (cost > costs[current]) continue;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var next = new Cell(current.X + dx, current.Y + dy);
                    if (!InBounds(next) || occupied.Contains(next)) continue;
                    if (dx != 0 && dy != 0 && (occupied.Contains(new(current.X + dx, current.Y)) || occupied.Contains(new(current.X, current.Y + dy)))) continue;
                    double nextCost = cost + (dx != 0 && dy != 0 ? Math.Sqrt(2) : 1);
                    if (nextCost > budget || (costs.TryGetValue(next, out double existing) && existing <= nextCost)) continue;
                    costs[next] = nextCost;
                    queue.Enqueue(next, nextCost);
                }
        }
        return costs.Keys.OrderBy(p => p.DistanceTo(target.Position)).ThenBy(p => costs[p]).First();
    }

    // ---- the AI's turn ----

    /// What this unit does on its turn: its state's priority list, first allowed action wins (round 2).
    /// Returns the action, the cell for a Move (already within its allowance), and whom to face for a Turn.
    public static (CombatAction Action, Cell? To, CombatUnit? Face) ChooseAction(World world, Encounter enc, CombatUnit u)
    {
        var rng = world.Rng("combat", u.Agent);
        var stats = StatsOf(world, u);
        var target = TargetOf(enc, u);
        bool Allowed(CombatAction a) => Refusal(world, enc, u, a) == null;
        (CombatAction, Cell?, CombatUnit?) Wait() => (CombatAction.Wait, null, null);
        (CombatAction, Cell?, CombatUnit?) Go(Cell to) => WithinAllowance(world, u, to) is Cell c ? (CombatAction.Move, c, null) : Wait();
        if (Body.IsAsleep(BodyOf(world, u))) return Wait();
        switch (u.State)
        {
            case FighterState.Unaware:
                if (rng.NextDouble() < 0.6) return Wait();
                return Go(Wander(u, rng));
            case FighterState.Wary:
                // Freezes and looks, or now and then comes a step nearer to see (round 2).
                if (rng.NextDouble() >= WaryLookPct || NearestEnemy(enc, u) is not CombatUnit seen) return Wait();
                return Go(u.Position.Toward(seen.Position, 1));
        }
        if (target == null) return Wait();
        if (BestTarget(world, enc, u) is CombatUnit best && best != target
            && TargetScore(world, enc, u, best) > TargetScore(world, enc, u, target) + SwitchGainMargin)
            return (CombatAction.Turn, null, best);
        switch (u.State)
        {
            case FighterState.Pressing:
                if (Allowed(CombatAction.Attack)) return (CombatAction.Attack, null, null);
                if (!stats.IsAnimal)
                {
                    if (u.Weapon is Gear w && Allowed(CombatAction.Throw) && ThrowChance(world, u, target, w) >= PersonThrowMinPct) return (CombatAction.Throw, null, null);
                    if (Allowed(CombatAction.ThrowStone) && StoneChance(world, u, target) >= PersonThrowMinPct) return (CombatAction.ThrowStone, null, null);
                }
                return Go(Pursue(world, enc, u, target));
            case FighterState.Standoff:
                if (!stats.IsAnimal)
                {
                    if (u.Weapon == null && Allowed(CombatAction.PickUp)) return (CombatAction.PickUp, null, null);
                    if (Allowed(CombatAction.Intimidate) && IntimidationChance(world, target, u) >= IntimidateMinPct) return (CombatAction.Intimidate, null, null);
                    return Wait();
                }
                if (Allowed(CombatAction.Attack)) return (CombatAction.Attack, null, null); // the target stepped into its reach
                return Hold(world, enc, u, target, rng) is Cell held ? Go(held) : Wait();
            case FighterState.GivingGround:
                if (!stats.IsAnimal && u.Weapon == null && Allowed(CombatAction.PickUp)) return (CombatAction.PickUp, null, null);
                // The enemy in reach: a fighting retreat strikes while it has the nerve for it (a tunable line).
                if (Allowed(CombatAction.Attack) && Margin(world, enc, u, target) >= stats.StrikeBackMargin) return (CombatAction.Attack, null, null);
                return Go(u.Position.Away(target.Position, Math.Max(1, Allowance(world, u) / 2)));
            case FighterState.Fleeing:
                // Resolved at once by the catch-up (CombatFlight); run down this round, it can only stand there.
                return u.RunDown ? Wait() : (CombatAction.Flee, null, null);
        }
        return Wait();
    }

    /// A wary animal's chance a round to come a step nearer to look (judgment: the sweeps tune it).
    const double WaryLookPct = 0.15;

    /// The hold: standing off, an animal steps toward or away in proportion to its resolve, past a deadband,
    /// and never comes inside the longer of the two reaches and half a metre; in the deadband, or at that
    /// line, it stops or circles a metre round. Null when it stays put.
    static Cell? Hold(World world, Encounter enc, CombatUnit u, CombatUnit target, Rng rng)
    {
        double resolve = Margin(world, enc, u, target), distanceM = u.Position.DistanceTo(target.Position);
        double holdM = HoldLineM(world, u, target);
        double stepM = Math.Abs(resolve) < HoldDeadbandResolve ? 0
            : Math.Clamp(resolve * HoldStepMPerResolve, -Allowance(world, u), Math.Max(0, distanceM - holdM));
        for (; Math.Abs(stepM) >= 1; stepM -= Math.Sign(stepM) * 0.5)
        {
            var c = stepM > 0 ? u.Position.Toward(target.Position, (float)stepM) : u.Position.Away(target.Position, (float)-stepM);
            if (c != u.Position && KeepsHold(world, enc, c, u, target, holdM)) return c;
        }
        if (rng.NextInt(2) == 0) return null;
        var side = Sideways(u, target, rng.NextInt(2) == 0 ? 1 : -1);
        return KeepsHold(world, enc, side, u, target, holdM) ? side : null;
    }

    /// Where an animal standing off stops: the longer of its reach and its target's, and half a metre more.
    public static double HoldLineM(World world, CombatUnit u, CombatUnit target) =>
        Math.Max(StrikeRangeM(world, u), StrikeRangeM(world, target)) + HoldBeyondReachM;

    /// A cell keeps the hold if it is outside the line, or no nearer than the animal stands now. It must be
    /// free and on the grid, or the step would land on some other cell nobody checked.
    static bool KeepsHold(World world, Encounter enc, Cell c, CombatUnit u, CombatUnit target, double holdM) =>
        c.DistanceTo(target.Position) >= Math.Min(holdM, u.Position.DistanceTo(target.Position))
        && InBounds(c) && OnTheGrid(c) == c && !Field(enc).Any(o => o != u && o.Position == c && Alive(world, o));

    const double HoldBeyondReachM = 0.5, HoldDeadbandResolve = 0.05, HoldStepMPerResolve = 10;

    /// The cell a step toward this one ends on within the unit's allowance (a step rounded onto the grid can
    /// come out a little long), or null when there is no step to take.
    static Cell? WithinAllowance(World world, CombatUnit u, Cell to)
    {
        int allowance = Allowance(world, u);
        for (double m = Math.Min(allowance, u.Position.DistanceTo(to)); m > 0; m -= 0.5)
        {
            var c = u.Position.Toward(to, (float)m);
            if (c != u.Position && u.Position.DistanceTo(c) <= allowance) return c;
        }
        return null;
    }

    static Cell Wander(CombatUnit u, Rng rng)
    {
        double angle = rng.NextDouble() * 2 * Math.PI;
        var wander = u.Position.Move(new System.Numerics.Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)), (float)(rng.NextDouble() * WanderM + 1));
        return new Cell(Math.Clamp(wander.X, 1, MapSize - 2), Math.Clamp(wander.Y, 1, MapSize - 2));
    }

    /// A metre to one side of the line to the target.
    static Cell Sideways(CombatUnit u, CombatUnit target, int side)
    {
        var d = u.Position.DirectionTo(target.Position);
        var step = u.Position.Move(new System.Numerics.Vector2(-d.Y * side, d.X * side), 1f);
        return new Cell(Math.Clamp(step.X, 1, MapSize - 2), Math.Clamp(step.Y, 1, MapSize - 2));
    }

    /// One AI turn: the round's morale, a target if it has lost one, the bands, then its state's action.
    /// A pressing animal that closes into its reach strikes in the same turn, unless a braced blow felled it.
    internal static void AiTurn(World world, Encounter enc, CombatUnit u, List<WorldEvent> events)
    {
        if (IsPlayer(u) || !u.InField || IsDown(world, u)) return;
        if (IsAware(u.State))
        {
            ReTarget(world, enc, u);
            RoundMorale(world, enc, u);
            UpdateState(world, enc, u);
        }
        var (action, to, face) = ChooseAction(world, enc, u);
        var target = TargetOf(enc, u);
        if (Apply(world, enc, u, action, events, face, to) is { Refused: string refused })
            throw new InvalidOperationException($"The AI chose a refused {action} for {u.Agent}: {refused}");
        if (action == CombatAction.Turn) return;
        if (action == CombatAction.Move && u.State == FighterState.Pressing && StatsOf(world, u).IsAnimal
            && target is { InField: true } && u.InField && !IsDown(world, u) && u.Position.DistanceTo(target.Position) <= StrikeRangeM(world, u))
            Attack(world, enc, u, target, events);
        Sweep(world, enc, events, u.Agent);
    }
}
