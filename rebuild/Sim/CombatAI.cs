namespace Survival;

/// The one thing a fighter stores about how it is fighting. Down and Gone are read off the body and
/// the field (Combat.StateOf), never stored. Clash is Pressing with the target in reach.
public enum FighterState { Unaware, Wary, Pressing, Standoff, GivingGround, Fleeing, Gone, Down }

/// Wounds are not an event: they cost morale through Combat.HealthPenalty for as long as they last.
public enum MoraleEvent { DealtDamage, AllyKilled, AllyFled, Intimidated }

/// Any unit the player is not driving takes its turn here. Each resolve band has its own way in and out,
/// so a fighter that has committed stays committed until its resolve really turns.
public static partial class Combat
{
    // Starting values, to be tuned.
    public const double PressingEnterResolve = 0.2, PressingLeaveResolve = 0.0;
    public const double GivingGroundEnterResolve = -0.3, GivingGroundRecoverResolve = -0.1;
    public const double FleeingEnterResolve = -0.8, FleeingRecoverResolve = -0.4;
    public const double WaryAlertnessLevel = 0.3, UnawareAlertnessLevel = 0.1, EngagedAlertnessLevel = 0.7;
    public const double NoticeAlertnessDelta = 0.35, FadeAlertnessDelta = 0.1;

    const double DealtDamageMorale = 0.15, ChaseInstinctMorale = 0.3, TargetGivingGroundMorale = 0.05,
        TargetPressingMoralePerThreat = 0.05, StandoffCostMorale = 0.02, AllyKilledMorale = -2.0, AllyFledMorale = -1.0;
    const double ThreatDropPerM = 0.07;

    public const double PersonThrowMinPct = 0.5, IntimidateMinPct = 0.5;
    /// A better target must beat the current one by SwitchGainScore to be worth the round turning costs.
    const double ClosenessScorePerM = 0.02, SwitchGainScore = 0.1;
    const int WanderM = 2;

    public static double Threat(World world, CombatUnit u) => StatsOf(world, u).Threat * Body.Vitality(BodyOf(world, u), SpeciesOf(world, u));

    public static double UnitBoldness(World world, Encounter enc, CombatUnit u) =>
        Courage(SpeciesOf(world, u), u.BoldnessModifier, BodyOf(world, u)) + AllyBonus(world, enc, u);

    static double Courage(Species species, double boldnessModifier, BodyState body) =>
        SpeciesTable.Of(species).Boldness + boldnessModifier - HealthPenalty(body, species);

    public static double HealthPenalty(BodyState body, Species species) => (1 - Body.Vitality(body, species)) + 0.5 * (1 - body.BloodPct);

    // Could scale by its opinion of each ally; nothing writes opinions yet.
    static double Cohesion(World world, CombatUnit u) => StatsOf(world, u).Cohesion;

    public static double PerceivedThreat(World world, CombatUnit u, CombatUnit other) =>
        PerceivedThreat(Threat(world, other), u.Position.DistanceTo(other.Position));

    static double PerceivedThreat(double threat, double distanceM) => Math.Max(0, (1 - distanceM * ThreatDropPerM) * threat);

    static double AllyBonus(World world, Encounter enc, CombatUnit u) =>
        AllyBonus(Cohesion(world, u), Allies(enc, u).Select(a => Threat(world, a)));

    static double AllyBonus(double cohesion, IEnumerable<double> allyThreats) => 0.15 * allyThreats.Sum(t => Math.Max(0, t * cohesion));

    /// One formula for grid and map: the grid measures distance from the pit's cell or the torch-bearer,
    /// the map from the person the predator would come for. People are not deterred.
    public static double FireDeterrence(SpeciesStats stats, IEnumerable<(double IntensityPct, double DistanceM)> flames)
    {
        if (!stats.IsAnimal) return 0;
        var d = Fire.Deterrence;
        return flames.Sum(f => d.FireFear * f.IntensityPct * (f.DistanceM <= d.FullWithinM ? 1 : Math.Pow(d.FullWithinM / f.DistanceM, 2)));
    }

    /// A torch out of the fight has no place on the grid.
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

    /// Once a round, before anyone weighs them.
    static void CatchUpFlames(World world, Encounter enc, List<WorldEvent> events) => Fire.CatchUpOn(world, enc.Tile, events);

    public static double Resolve(World world, Encounter enc, CombatUnit u, CombatUnit other) =>
        UnitBoldness(world, enc, u) - FireDeterrence(world, enc, u) - PerceivedThreat(world, u, other) - Pressure(world, enc, u, other);

    /// A pack round you weighs on you even while you face one of it.
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

    /// A standoff wears a fighter down, so a held animal tests and then breaks off.
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

    /// Animals do not fear weapons; they learn through injury.
    static double Intimidation(World world, CombatUnit target, CombatUnit shouter) =>
        world.Rng("combat", shouter.Agent).NextDouble() < IntimidationChance(world, target, shouter) ? 0.3 : 0.05;

    public static double IntimidationChance(World world, CombatUnit target, CombatUnit shouter) =>
        0.3 + Body.Vitality(BodyOf(world, shouter), SpeciesOf(world, shouter)) * 0.2
        + (Body.Vitality(BodyOf(world, target), SpeciesOf(world, target)) < 0.5 ? 0.15 : 0);

    public static FighterState StateOf(World world, CombatUnit u) =>
        !u.InField ? (Alive(world, u) ? FighterState.Gone : FighterState.Down)
        : IsDown(world, u) ? FighterState.Down
        : u.State;

    static bool IsDown(World world, CombatUnit u) => IsDown(BodyOf(world, u));

    internal static bool IsDown(BodyState body) => !Body.IsAlive(body) || Body.Speed(body) <= 0 && Body.Dexterity(body) <= 0;

    static bool IsAware(FighterState s) => s is FighterState.Pressing or FighterState.Standoff or FighterState.GivingGround or FighterState.Fleeing;

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

    static void Engage(World world, Encounter enc, CombatUnit u)
    {
        u.AlertnessLevel = 1;
        if (!IsAware(u.State)) Enter(world, enc, u, StatsOf(world, u).IsSkittish ? FighterState.Fleeing : FighterState.Standoff);
    }

    static void Alert(World world, Encounter enc, CombatUnit u, double delta)
    {
        if (IsAware(u.State)) return;
        u.AlertnessLevel = Math.Clamp(u.AlertnessLevel + delta, 0, 1);
        if (u.AlertnessLevel >= EngagedAlertnessLevel) Engage(world, enc, u);
        else if (u.State == FighterState.Unaware && u.AlertnessLevel >= WaryAlertnessLevel) Enter(world, enc, u, FighterState.Wary);
        else if (u.State == FighterState.Wary && u.AlertnessLevel < UnawareAlertnessLevel) Enter(world, enc, u, FighterState.Unaware);
    }

    static void UpdateState(World world, Encounter enc, CombatUnit u)
    {
        if (!IsAware(u.State)) return;
        if (StatsOf(world, u).IsSkittish) { Enter(world, enc, u, FighterState.Fleeing); return; }
        if (TargetOf(enc, u) is not CombatUnit target) return;
        var next = NextState(u.State, Resolve(world, enc, u, target));
        // A person runs only from what it can outrun; else it gives ground.
        if (next == FighterState.Fleeing && !StatsOf(world, u).IsAnimal && BestCatchPct(world, enc, u) >= PersonRunsBelowPct)
            next = FighterState.GivingGround;
        Enter(world, enc, u, next);
    }

    public static FighterState NextState(FighterState state, double resolve)
    {
        if (state == FighterState.Fleeing) return resolve > FleeingRecoverResolve ? FighterState.GivingGround : FighterState.Fleeing;
        if (resolve < FleeingEnterResolve) return FighterState.Fleeing;
        return state switch
        {
            FighterState.Pressing => resolve < GivingGroundEnterResolve ? FighterState.GivingGround
                : resolve < PressingLeaveResolve ? FighterState.Standoff : FighterState.Pressing,
            FighterState.GivingGround => resolve >= PressingEnterResolve ? FighterState.Pressing
                : resolve > GivingGroundRecoverResolve ? FighterState.Standoff : FighterState.GivingGround,
            FighterState.Standoff => resolve >= PressingEnterResolve ? FighterState.Pressing
                : resolve < GivingGroundEnterResolve ? FighterState.GivingGround : FighterState.Standoff,
            _ => state,
        };
    }

    /// The player has no thresholds of their own: their action sets their state.
    static void PlayerState(World world, Encounter enc, CombatUnit u, CombatAction action, Cell before)
    {
        if (!u.InField) return;
        if (!IsAware(u.State))
        {
            if (action == CombatAction.Wait) return;
            Engage(world, enc, u);
        }
        var target = TargetOf(enc, u);
        double closer = target == null ? 0 : before.DistanceTo(target.Position) - u.Position.DistanceTo(target.Position);
        var state = action switch
        {
            CombatAction.Advance or CombatAction.Attack or CombatAction.Throw or CombatAction.ThrowStone or CombatAction.Shove => FighterState.Pressing,
            CombatAction.Retreat => FighterState.GivingGround,
            CombatAction.Move => closer >= 1 ? FighterState.Pressing : closer <= -1 ? FighterState.GivingGround : FighterState.Standoff,
            CombatAction.Turn => u.State,
            CombatAction.Flee => FighterState.Fleeing,
            CombatAction.Chase => FighterState.Pressing,
            _ => FighterState.Standoff,
        };
        Enter(world, enc, u, state);
    }

    /// Only fighters the AI drives are held to this. A cornered runner with no step to take can only
    /// turn or wait.
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

    /// The resolve and bands a fight runs on, for an NPC deciding whether to stand or run.
    public static FighterState WouldStand(World world, Fighter me, Agent threat, double distanceM)
    {
        var them = SpeciesTable.Of(threat.Species);
        var living = Combat.Side(world, threat).Where(Sim.IsAlive).Select(a => a.Body).ToList();
        if (living.Count == 0) throw new ArgumentException($"{threat.Id} has nobody alive to stand against");
        double worst = living.Max(m => them.Threat * Body.Vitality(m, threat.Species));
        // No allies or morale before the fight, and fire deters no person.
        double resolve = Courage(world.Agent(me.Agent).Species, me.BoldnessDelta, world.Agent(me.Agent).Members[me.Member])
            - PerceivedThreat(worst, distanceM);
        return NextState(FighterState.Standoff, resolve);
    }

    /// Resolve's terms before any fight: it must follow Resolve when Resolve changes. The tile's flames
    /// must be caught up (Fire.CatchUpOn).
    public static double ResolveToward(World world, Agent animal, Agent target)
    {
        var stats = SpeciesTable.Of(animal.Species);
        var living = Combat.Side(world, animal).Where(Sim.IsAlive).Select(a => a.Body).ToList();
        if (living.Count == 0) throw new ArgumentException($"{animal.Id} has nobody alive to come on");
        var body = target.Body;
        var others = PeopleWith(world, target);
        double delta = BoldnessToward(world, animal, target) - 0.5;
        int lead = Enumerable.Range(0, living.Count).MaxBy(i => Courage(animal.Species, delta, living[i]));
        double nerve = Courage(animal.Species, delta, living[lead])
            + AllyBonus(stats.Cohesion, living.Where((_, i) => i != lead).Select(m => stats.Threat * Body.Vitality(m, animal.Species)));
        double deterrence = FireDeterrence(stats, Fire.FlamesOn(world, target.Tile).Select(f => (f.IntensityPct, 0.0)));
        double threat = SpeciesTable.Of(target.Species).Threat * Body.Vitality(body, target.Species);
        double pressure = OtherPressureFactor * others.Sum(p => SpeciesTable.Of(p.Species).Threat * Body.Vitality(p.Body, p.Species));
        return nerve - deterrence - PerceivedThreat(threat, 0) - pressure;
    }

    /// Carried meat draws it by scent (Scent.Of), not by boldness (CMB-16).
    public static double BoldnessToward(World world, Agent animal, Agent target)
    {
        var mind = Fauna.MindOf(animal);
        return Boldness(world, animal.Id, target.Body, target.Species, defending: Fauna.HoldsAKill(world, animal), hunger: Fauna.Hunger(animal), fear: mind.Fear,
            othersWithTarget: PeopleWith(world, target).Count);
    }

    static List<Agent> PeopleWith(World world, Agent target) =>
        Nearby.At(world, target.Tile).Where(a => a.Id != target.Id && a.Mind is PersonMind && Sim.InTheWorld(world, a)).ToList();

    public static Gear? BestWeapon(Agent person) =>
        Items.PackOf(person)!.Gear.Where(g => Items.Def(g).Weapon != null && Items.Works(g)).OrderByDescending(g => WeaponWorth(Items.Def(g).Weapon!)).FirstOrDefault();

    public static double? ResolveOnTile(World world, Agent herd, TileId tile)
    {
        var people = Nearby.At(world, tile).Where(a => a.Mind is PersonMind && Sim.InTheWorld(world, a)).ToList();
        return people.Count == 0 ? null : people.Max(p => ResolveToward(world, herd, p));
    }

    /// Short of the Pressing entry a herd holds where it is until the fire dies or the person steps away.
    public static bool WouldEnter(World world, Agent herd, TileId tile) =>
        ResolveOnTile(world, herd, tile) is not double resolve || resolve >= PressingEnterResolve;

    public static IReadOnlyList<CombatTarget> Targets(World world, Encounter enc, CombatUnit u) =>
        Enemies(enc, u).OrderBy(e => u.Position.DistanceTo(e.Position)).ThenBy(e => enc.Units.IndexOf(e))
            .Select(e => new CombatTarget(enc.Units.IndexOf(e), e.Agent, e.Member, u.Position.DistanceTo(e.Position), TargetOf(enc, u) == e,
                TargetOf(enc, u) == e ? null : Refusal(world, enc, u, CombatAction.Turn, e)))
            .ToList();

    public static CombatUnit? TargetOf(Encounter enc, CombatUnit u)
    {
        if (u.Target is not int i) return null;
        if (i < 0 || i >= enc.Units.Count) throw new InvalidOperationException($"{u.Agent} faces unit {i}, but the fight has {enc.Units.Count}");
        return enc.Units[i] is { InField: true } t && t.Team != u.Team ? t : null;
    }

    /// A predator wants the most vulnerable (the best resolve), anything else the biggest threat to it.
    static double TargetScore(World world, Encounter enc, CombatUnit u, CombatUnit other)
    {
        double want = StatsOf(world, u).Role == Role.Predator ? Resolve(world, enc, u, other) : PerceivedThreat(world, u, other);
        return want - ClosenessScorePerM * u.Position.DistanceTo(other.Position);
    }

    public static CombatUnit? BestTarget(World world, Encounter enc, CombatUnit u) =>
        Enemies(enc, u).OrderByDescending(e => TargetScore(world, enc, u, e)).ThenBy(e => enc.Units.IndexOf(e)).FirstOrDefault();

    /// Turning from a target still there to a better one is an action, and costs the round.
    static void ReTarget(World world, Encounter enc, CombatUnit u)
    {
        if (TargetOf(enc, u) == null && BestTarget(world, enc, u) is CombatUnit best) u.Target = enc.Units.IndexOf(best);
    }

    /// A crippled animal can be run down, and a mauled or laden person cannot sprint.
    public static int Allowance(World world, CombatUnit u)
    {
        if (RunningOf(world, u) is not { } running) return 0;
        return Math.Max(1, (int)Math.Round(StatsOf(world, u).MoveAllowanceM * Math.Clamp(running.SpeedPct, 0, 1)));
    }

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
                if (rng.NextDouble() >= WaryLookPct || NearestEnemy(enc, u) is not CombatUnit seen) return Wait();
                return Go(u.Position.Toward(seen.Position, 1));
        }
        if (target == null) return Wait();
        if (BestTarget(world, enc, u) is CombatUnit best && best != target
            && TargetScore(world, enc, u, best) > TargetScore(world, enc, u, target) + SwitchGainScore)
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
                if (Allowed(CombatAction.Attack) && Resolve(world, enc, u, target) >= stats.StrikeBackResolve) return (CombatAction.Attack, null, null);
                return Go(u.Position.Away(target.Position, Math.Max(1, Allowance(world, u) / 2)));
            case FighterState.Fleeing:
                // Resolved at once by the catch-up (CombatFlight); run down this round, it can only stand there.
                return u.RunDown ? Wait() : (CombatAction.Flee, null, null);
        }
        return Wait();
    }

    const double WaryLookPct = 0.15;

    /// Standing off, an animal steps by its resolve past a deadband, never inside the hold line; else it stops or circles.
    static Cell? Hold(World world, Encounter enc, CombatUnit u, CombatUnit target, Rng rng)
    {
        double resolve = Resolve(world, enc, u, target), distanceM = u.Position.DistanceTo(target.Position);
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

    public static double HoldLineM(World world, CombatUnit u, CombatUnit target) =>
        Math.Max(StrikeRangeM(world, u), StrikeRangeM(world, target)) + HoldBeyondReachM;

    /// The cell must be free and on the grid, or the step would land on some other cell nobody checked.
    static bool KeepsHold(World world, Encounter enc, Cell c, CombatUnit u, CombatUnit target, double holdM) =>
        c.DistanceTo(target.Position) >= Math.Min(holdM, u.Position.DistanceTo(target.Position))
        && InBounds(c) && OnTheGrid(c) == c && !Field(enc).Any(o => o != u && o.Position == c && Alive(world, o));

    const double HoldBeyondReachM = 0.5, HoldDeadbandResolve = 0.05, HoldStepMPerResolve = 10;

    /// A step rounded onto the grid can come out a little long.
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

    static Cell Sideways(CombatUnit u, CombatUnit target, int side)
    {
        var d = u.Position.DirectionTo(target.Position);
        var step = u.Position.Move(new System.Numerics.Vector2(-d.Y * side, d.X * side), 1f);
        return new Cell(Math.Clamp(step.X, 1, MapSize - 2), Math.Clamp(step.Y, 1, MapSize - 2));
    }

    /// A pressing animal that closes into its reach strikes in the same turn.
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
        if (Apply(world, enc, u, action, events, face, to) is { Refused: { } refused })
            throw new InvalidOperationException($"The AI chose a refused {action} for {u.Agent}: {refused}");
        if (action == CombatAction.Turn) return;
        if (action == CombatAction.Move && u.State == FighterState.Pressing && StatsOf(world, u).IsAnimal
            && target is { InField: true } && u.InField && !IsDown(world, u) && u.Position.DistanceTo(target.Position) <= StrikeRangeM(world, u))
            Attack(world, enc, u, target, events);
        Sweep(world, enc, events, u.Agent);
    }
}
