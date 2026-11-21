namespace Survival;

/// Watches a fight round by round, for replays (rebuild/Tools/TuneViewer). It must only read: no random
/// draws, no writes. With no observer attached a fight builds nothing for it, and a fight with one is
/// the same fight (CombatObserverTests).
public interface ICombatObserver
{
    void Round(CombatRoundView round);
}

/// The grid at the end of a round. Round 0 is the placement, before anyone acts. Events are the
/// WorldEvents this round raised (Injured, Killed, Died, FightEnded...). Fire is the burning pit's cell
/// (Encounter.FireCell), or null when no pit burns on the tile.
public sealed record CombatRoundView(int Round, IReadOnlyList<FighterView> Fighters, Cell? Fire, IReadOnlyList<WorldEvent> Events);

/// One unit of Encounter.Units, by its index there. Action and Target are what it did this round (null in
/// round 0, or if it did nothing); State is Combat.StateOf; Margin is Combat.Margin toward its target
/// (null with no target). The body numbers are read as the round ends, or null once the body is gone
/// (a herd member removed when it died).
public sealed record FighterView(int Index, AgentId Agent, int Member, int Team, Species Species,
    Cell Position, FighterState State, CombatAction? Action, int? Target, double? Margin,
    double? Boldness, double? Vitality, double? BloodPct, double? SprintReservePct, double? WoundLevel);

public static partial class Combat
{
    /// Attaches a watcher to a fight under way and shows it the field as it stands (round 0 when nothing
    /// has happened yet).
    public static void Watch(World world, Encounter enc, ICombatObserver observer)
    {
        enc.Observer = observer;
        enc.ObservedSpecies = enc.Units.Select(u => u.Agent).Distinct().ToDictionary(a => a, a => world.Agent(a).Species);
        Report(world, enc, []);
    }

    /// What one unit did this round, for the watcher; nothing when no one watches.
    static void Record(Encounter enc, CombatUnit u, CombatAction action, CombatUnit? target)
    {
        if (enc.Observer == null) return;
        u.RoundAction = action;
        u.RoundTarget = target == null ? null : enc.Units.IndexOf(target);
    }

    /// The round as it ended, to the watcher, if there is one. Reads only.
    static void Report(World world, Encounter enc, IReadOnlyList<WorldEvent> events)
    {
        if (enc.Observer is not ICombatObserver observer) return;
        var fighters = enc.Units.Select((u, i) =>
        {
            var agent = world.Agents.GetValueOrDefault(u.Agent);
            var body = agent != null && u.Member < agent.Members.Count ? agent.Members[u.Member] : null;
            bool reads = body != null && Body.IsAlive(body) && u.InField && !u.Released;
            var target = reads ? TargetOf(enc, u) : null;
            bool targetReads = target != null && world.Agents.TryGetValue(target.Agent, out var t) && target.Member < t.Members.Count;
            return new FighterView(i, u.Agent, u.Member, u.Team, agent?.Species ?? enc.ObservedSpecies![u.Agent], u.Position,
                body == null ? FighterState.Down : StateOf(world, u), u.RoundAction, u.RoundTarget,
                targetReads ? Margin(world, enc, u, target!) : null, reads ? UnitBoldness(world, enc, u) : null,
                body == null ? null : Body.Vitality(body), body?.BloodPct, body == null ? null : Body.SprintReservePct(body),
                body == null ? null : WoundLevel(body));
        }).ToList();
        foreach (var u in enc.Units) (u.RoundAction, u.RoundTarget) = (null, null);
        observer.Round(new CombatRoundView(enc.Rounds, fighters, Fire.FlamesOn(world, enc.Tile).Any(f => f.Holder == null) ? enc.FireCell : null, events));
    }
}
