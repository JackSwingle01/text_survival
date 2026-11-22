namespace Survival.Tests;

/// The combat trace hook (rebuild/Tools/TuneViewer/README.md): a watched fight is the same fight, and the
/// watcher sees every round from the placement on.
public class CombatObserverTests
{
    sealed class Recorder : ICombatObserver
    {
        public List<CombatRoundView> Rounds { get; } = [];
        public void Round(CombatRoundView round) => Rounds.Add(round);
    }

    static (string Save, string Events, int Rounds, Recorder? Seen) Headless(bool watch)
    {
        var (w, t) = CombatTests.Field(7);
        var pack = CombatTests.Herd(w, Species.Wolf, 3, t);
        var prey = CombatTests.Herd(w, Species.Caribou, 2, t);
        var enc = Combat.Create(w, [new Fighter(pack, 0), new Fighter(pack, 1), new Fighter(pack, 2)], [new Fighter(prey, 0)], t, 15,
            Awareness.Engaged, Awareness.Alert);
        var seen = watch ? new Recorder() : null;
        if (seen != null) Combat.Watch(w, enc, seen);
        var events = new List<WorldEvent>();
        Combat.ResolveHeadless(w, enc, events);
        return (Save.ToJson(w), string.Join("\n", events), enc.Rounds, seen);
    }

    static (string Save, string Events, int Rounds, Recorder? Seen) Stepped(bool watch)
    {
        var (w, t) = CombatTests.Field(7);
        var wolf = CombatTests.Herd(w, Species.Wolf, 1, t);
        var enc = Combat.Create(w, [new Fighter(AgentId.Player) { Weapon = CombatTests.Spear(w) }], [new Fighter(wolf)], t, 6,
            Awareness.Engaged, Awareness.Engaged);
        var seen = watch ? new Recorder() : null;
        if (seen != null) Combat.Watch(w, enc, seen);
        var events = new List<WorldEvent>();
        for (int i = 0; i < 60 && w.Encounters.Contains(enc); i++)
        {
            var me = Combat.UnitOf(enc, AgentId.Player);
            var act = Combat.Actions(w, enc, me).Contains(CombatAction.Attack) ? CombatAction.Attack : CombatAction.Advance;
            events.AddRange(Sim.Step(w, new Fight(AgentId.Player, act), 1).Events);
        }
        return (Save.ToJson(w), string.Join("\n", events), enc.Rounds, seen);
    }

    [Fact]
    public void AWatchedHeadlessFight_IsTheSameFight_AndEveryRoundIsSeen()
    {
        var plain = Headless(false);
        var watched = Headless(true);
        Assert.Equal(plain.Save, watched.Save);
        Assert.Equal(plain.Events, watched.Events);
        Assert.Equal(Enumerable.Range(0, plain.Rounds + 1), watched.Seen!.Rounds.Select(r => r.Round));
        Assert.All(watched.Seen.Rounds, r => Assert.Equal(4, r.Fighters.Count));
        Assert.Null(watched.Seen.Rounds[0].Fighters[0].Action);
        Assert.NotNull(watched.Seen.Rounds[1].Fighters[0].Action);
    }

    [Fact]
    public void AWatchedSteppedFight_IsTheSameFight_AndEveryRoundIsSeen()
    {
        var plain = Stepped(false);
        var watched = Stepped(true);
        Assert.Equal(plain.Save, watched.Save);
        Assert.Equal(plain.Events, watched.Events);
        var rounds = watched.Seen!.Rounds.Select(r => r.Round).ToList();
        Assert.Equal(0, rounds[0]);
        Assert.Equal(plain.Rounds, rounds.Max());
        Assert.Contains(watched.Seen.Rounds, r => r.Fighters.Any(f => f.Action == CombatAction.Attack || f.Action == CombatAction.Advance));
    }
}
