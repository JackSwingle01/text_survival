namespace Survival;

/// The odds a pursuer runs down a fleer (Combat.CatchOdds): the chance, and when and how far into the
/// chase the catch would come (the chase limit, if it never would).
public sealed record CatchOdds(double Pct, double Seconds, double PursuerMetres);

/// Flight and catch-up. A fighter that turns to run is resolved at once by a
/// closed-form chase against everyone pressing it: each runner sprints until its reserve is spent, then
/// holds endurance speed; a pursuer catches if it closes the gap at any second before
/// its chase limit. Speeds vary about 8% each way, which turns the
/// gain into odds, and the flight is rolled once. Under 5% the fleer is gone at once; an AI pursuer
/// chases at 25% or better; the player is offered the chase with its odds. Caught, the fight goes on at
/// contact, the runner with no defence that round; not caught, the fleer and its chasers are gone, a tile
/// on for every 100 m they ran, five at most, and the trail they leave runs that way.
public static partial class Combat
{
    /// The spread of a runner's speed about its expected one, as a standard deviation.
    const double SpeedSpreadSd = 0.08;
    const double GoneAtOncePct = 0.05, AiChasesFromPct = 0.25, PersonRunsBelowPct = 0.5;
    /// Close enough to strike at once: the Close zone.
    const double ContactM = 1;
    const double MetresPerTile = 100;
    const int MaxFlightTiles = 5;
    /// A fighter standing off or giving ground this far from every enemy has broken off.
    public const double BreakOffM = 30;

    /// The chance this pursuer catches this fleer if both run now, and when. Reads only. The gain is checked
    /// every second up to the pursuer's chase limit, because speeds fall smoothly as each reserve drains
    /// (Body.SprintPlan) and the best moment can fall anywhere in it.
    /// Seconds is when the gap would close (or the chase limit, if it never would at the expected speeds).
    public static CatchOdds CatchOdds(World world, CombatUnit pursuer, CombatUnit fleer)
    {
        var (bp, rp) = (BodyOf(world, pursuer), RunningOf(world, pursuer));
        var (bf, rf) = (BodyOf(world, fleer), RunningOf(world, fleer));
        double limit = StatsOf(world, pursuer).ChaseLimitSeconds;
        double need = pursuer.Position.DistanceTo(fleer.Position) - ContactM;
        double best = 0, closes = double.NaN;
        for (int t = 1; t <= Math.Ceiling(limit); t++)
        {
            double seconds = Math.Min(t, limit);
            double dp = Body.SprintPlan(bp, rp, seconds).Metres, df = Body.SprintPlan(bf, rf, seconds).Metres;
            double gain = dp - df - need, spread = SpeedSpreadSd * Math.Sqrt(dp * dp + df * df);
            best = Math.Max(best, spread <= 0 ? (gain >= 0 ? 1 : 0) : NormalCdf(gain / spread));
            if (double.IsNaN(closes) && gain >= 0) closes = seconds;
        }
        double when = double.IsNaN(closes) ? limit : closes;
        return new CatchOdds(best, when, Body.SprintPlan(bp, rp, when).Metres);
    }

    static List<CombatUnit> Pursuers(World world, Encounter enc, CombatUnit fleer) =>
        Enemies(enc, fleer).Where(e => StateOf(world, e) == FighterState.Pressing && TargetOf(enc, e) == fleer).ToList();

    static double BestCatchPct(World world, Encounter enc, CombatUnit fleer) =>
        Pursuers(world, enc, fleer).Select(p => CatchOdds(world, p, fleer).Pct).DefaultIfEmpty(0).Max();

    /// The fighter runs for it: resolved at once against everyone pressing it. Predators facing it first
    /// take the chase instinct into their bands, so a wolf standing off can be set running by the turn to run.
    static void Flight(World world, Encounter enc, CombatUnit fleer, List<WorldEvent> events)
    {
        Enter(world, enc, fleer, FighterState.Fleeing);
        foreach (var e in Enemies(enc, fleer).Where(e => !IsPlayer(e) && TargetOf(enc, e) == fleer).ToList()) UpdateState(world, enc, e);
        var odds = Pursuers(world, enc, fleer).Select(p => (Pursuer: p, Odds: CatchOdds(world, p, fleer))).ToList();
        if (odds.All(o => o.Odds.Pct < GoneAtOncePct)) { GetAway(world, enc, fleer); return; }
        var chasers = odds.Where(o => !IsPlayer(o.Pursuer) && o.Odds.Pct >= AiChasesFromPct).ToList();
        if (chasers.Count > 0)
        {
            Chase(world, enc, fleer, chasers);
            return;
        }
        if (odds.Any(o => IsPlayer(o.Pursuer) && o.Odds.Pct >= GoneAtOncePct))
        {
            enc.PendingFlight = enc.Units.IndexOf(fleer); // the player decides whether to give chase (Chase)
            return;
        }
        GetAway(world, enc, fleer);
    }

    /// The chase, rolled once for everyone after the runner together: each has its own odds, so the chance
    /// any of them runs it down is 1 - the product of each missing it, and the first to close the gap is
    /// the one that catches. Caught, the fight goes on at contact, the runner with no defence this round and
    /// no second flight; every chaser stopped when it was caught. Not caught, each chaser ran to its own
    /// limit and the runner as long as the longest of them: all are gone, that way.
    static void Chase(World world, Encounter enc, CombatUnit fleer, List<(CombatUnit Pursuer, CatchOdds Odds)> chasers)
    {
        double any = 1 - chasers.Aggregate(1.0, (miss, c) => miss * (1 - c.Odds.Pct));
        bool caught = world.Rng("flight", fleer.Agent).NextDouble() < any;
        if (caught)
        {
            var (catcher, odds) = chasers.OrderBy(c => c.Odds.Seconds).ThenByDescending(c => c.Odds.Pct).ThenBy(c => enc.Units.IndexOf(c.Pursuer)).First();
            foreach (var (c, _) in chasers) Body.Sprint(BodyOf(world, c), RunningOf(world, c), odds.Seconds);
            Body.Sprint(BodyOf(world, fleer), RunningOf(world, fleer), odds.Seconds);
            catcher.Position = ResolveDestination(world, enc, fleer.Position.Toward(catcher.Position, 1), catcher);
            fleer.RunDown = true;
            ChargeChase(enc, odds.Seconds);
            return;
        }
        var heading = HeadingAway(enc, fleer);
        double longest = chasers.Max(c => StatsOf(world, c.Pursuer).ChaseLimitSeconds);
        foreach (var (c, _) in chasers)
            Gone(world, enc, c, heading, Body.Sprint(BodyOf(world, c), RunningOf(world, c), StatsOf(world, c).ChaseLimitSeconds), chasing: true);
        Gone(world, enc, fleer, heading, Body.Sprint(BodyOf(world, fleer), RunningOf(world, fleer), longest));
        ChargeChase(enc, longest);
    }

    /// A chase longer than the round it began in adds its minutes to the fight (the time a headless fight
    /// charges its fighters); within a round it is part of the round.
    static void ChargeChase(Encounter enc, double seconds) => enc.Rounds += Math.Max(0, (int)Math.Ceiling(seconds / 60) - 1);

    /// Nobody gives chase (or nobody could catch it): it runs until its reserve is spent, and is gone.
    static void GetAway(World world, Encounter enc, CombatUnit fleer)
    {
        double metres = Body.SprintUntilSpent(BodyOf(world, fleer), RunningOf(world, fleer));
        Gone(world, enc, fleer, HeadingAway(enc, fleer), metres);
        enc.PendingFlight = null;
    }

    /// Off the field, with how far and which way it went. A chaser left after its quarry: its side does not
    /// lose heart over it, and the fight does not count it as running away.
    static void Gone(World world, Encounter enc, CombatUnit u, Direction heading, double metres, bool chasing = false)
    {
        u.FledHeading = heading;
        u.FledTiles = Math.Min(MaxFlightTiles, (int)Math.Round(metres / MetresPerTile));
        u.LeftChasing = chasing;
        if (chasing) u.InField = false;
        else Flee(world, enc, u);
    }

    /// The way away from everyone it was running from, as a compass direction (y runs north).
    static Direction HeadingAway(Encounter enc, CombatUnit fleer)
    {
        var enemies = Enemies(enc, fleer).ToList();
        double dx = enemies.Count == 0 ? 0 : fleer.Position.X - enemies.Average(e => e.Position.X);
        double dy = enemies.Count == 0 ? 1 : fleer.Position.Y - enemies.Average(e => e.Position.Y);
        return Math.Abs(dx) > Math.Abs(dy) ? (dx >= 0 ? Direction.East : Direction.West) : (dy >= 0 ? Direction.North : Direction.South);
    }

    static void PlayerChase(World world, Encounter enc, CombatUnit me)
    {
        var fleer = enc.Units[enc.PendingFlight!.Value];
        enc.PendingFlight = null;
        Chase(world, enc, fleer, [(me, CatchOdds(world, me, fleer))]);
    }

    /// The runner the player let go: gone, as it would have been with nobody after it.
    static void LetGo(World world, Encounter enc)
    {
        if (enc.PendingFlight is not int i) return;
        enc.PendingFlight = null;
        if (enc.Units[i].InField) GetAway(world, enc, enc.Units[i]);
    }

    /// Who is running from the player now and could be chased, with the odds, or null.
    public static (CombatUnit Fleer, CatchOdds Odds)? ChaseOffer(World world, Encounter enc, CombatUnit me) =>
        enc.PendingFlight is int i && enc.Units[i] is { InField: true } fleer ? (fleer, CatchOdds(world, me, fleer)) : null;

    /// Only once someone on the other side knows the fight is on: a hunter 34 m from a herd that has not
    /// seen it is stalking, not leaving.
    static void BreakOffs(World world, Encounter enc)
    {
        foreach (var u in Field(enc).ToList())
        {
            var enemies = Enemies(enc, u).ToList();
            if (StateOf(world, u) is FighterState.Standoff or FighterState.GivingGround
                && enemies.Any(e => IsAware(StateOf(world, e))) && enemies.All(e => e.Position.DistanceTo(u.Position) >= BreakOffM))
                Gone(world, enc, u, HeadingAway(enc, u), 0);
        }
    }

    /// Carries an agent along a heading tile by tile, recording each crossing through
    /// Tracks.RecordMove (the one seam for a crossing). Where the way ahead is blocked it swerves to a
    /// side (the left, then the right, of the heading); boxed in, it stops.
    static void Drift(World world, AgentId id, Direction heading, int tiles)
    {
        var agent = world.Agent(id);
        var m = Map.Of(world);
        if (tiles > 0) agent.Crossing = null; // whatever crossing it was on, it ran another way
        for (int i = 0; i < tiles; i++)
        {
            var open = new[] { heading, (Direction)(((int)heading + 3) % 4), (Direction)(((int)heading + 1) % 4) }
                .Select(d => Map.Step(agent.Tile, d)).Where(n => m.InBounds(n) && Traversal.Blocked(world, agent, agent.Tile, n) == null).Cast<TileId?>().FirstOrDefault();
            if (open is not TileId next) return;
            Tracks.RecordMove(world, agent.Tile, next, Tracks.MakerOf(agent.Species), agent.Members.Count, Tracks.IndividualDepth(agent.Species));
            agent.Tile = next;
        }
    }

    /// The standard normal's cumulative distribution (Abramowitz and Stegun 7.1.26, error under 1.5e-7).
    static double NormalCdf(double z)
    {
        double x = Math.Abs(z) / Math.Sqrt(2);
        double t = 1 / (1 + 0.3275911 * x);
        double erf = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return z >= 0 ? 0.5 * (1 + erf) : 0.5 * (1 - erf);
    }
}
