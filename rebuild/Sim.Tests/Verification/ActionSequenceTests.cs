using CsCheck;

namespace Survival.Tests.Verification;

/// The core is deterministic, and what it does never depends on how a caller cuts time into steps or
/// on a save and load between them (documentation/decisions). A random script of real player
/// activities, in a world with herds and sometimes an NPC, a carcass or a fight, is run four ways
/// from the same start: (a) each activity in one step, (b) each splittable activity's minutes cut
/// into random chunks, (c) as (a) with saves and loads between steps, and (d) as (b) with saves and
/// loads between chunks, mid-activity. All four must end in the same save, byte for byte, and live
/// the same events. A failure shrinks to the shortest script and prints it with the activities it
/// resolved to; ActionGen.Run replays it.
public class ActionSequenceTests
{
    /// Why the four runs disagree, or null: the first line names the law broken, the rest says where.
    static string? Failure(Script s)
    {
        Outcome a, b, c, d;
        try
        {
            a = ActionGen.Run(s, ActionGen.Mode.Straight);
            b = ActionGen.Run(s, ActionGen.Mode.Chunked, a.Resolved);
            c = ActionGen.Run(s, ActionGen.Mode.Saved, a.Resolved);
            d = ActionGen.Run(s, ActionGen.Mode.ChunkedSaved, a.Resolved);
        }
        catch (Exception e) { return $"threw {e.GetType().Name}\n{e}"; }
        string Context() => $"\nstraight run:\n  {string.Join("\n  ", a.Trace)}";
        if (a.Save != c.Save)
            return $"save and load changed the world\n{ActionGen.FirstDifference(a.Save, c.Save)}{Context()}\nsaved run:\n  {string.Join("\n  ", c.Trace)}";
        if (ActionGen.FirstEventDifference(a.Events, c.Events) is string ce)
            return $"save and load changed the events\n{ce}{Context()}";
        if (a.Save != b.Save)
            return $"chunking changed the world\n{ActionGen.FirstDifference(a.Save, b.Save)}{Context()}\nchunked run:\n  {string.Join("\n  ", b.Trace)}";
        // Step tallies (what one step of work brought in) are per step by design; everything else is the world's.
        if (ActionGen.FirstEventDifference(a.Events.Where(e => !ActionGen.IsTally(e)).ToList(), b.Events.Where(e => !ActionGen.IsTally(e)).ToList()) is string be)
            return $"chunking changed the events\n{be}{Context()}\nchunked run:\n  {string.Join("\n  ", b.Trace)}";
        if (b.Save != d.Save)
            return $"save and load mid-activity changed the world\n{ActionGen.FirstDifference(b.Save, d.Save)}{Context()}\nchunked and saved run:\n  {string.Join("\n  ", d.Trace)}";
        if (ActionGen.FirstEventDifference(b.Events, d.Events) is string de)
            return $"save and load mid-activity changed the events\n{de}{Context()}";
        return null;
    }

    /// CsCheck shrinks by sampling smaller cases, which an expensive property affords too few of. A
    /// failure is minimised here instead, greedily, keeping the law it broke: fewer activities, then
    /// fewer minutes, then the fewest, largest chunks and the fewest saves.
    static void Holds(Script s)
    {
        if (Failure(s) is not string failure) return;
        string law = failure.Split('\n')[0];
        bool Fails(Script t) => Failure(t) is string f && f.Split('\n')[0] == law;
        var (min, tries) = ActionGen.Minimize(s, Fails, maxTries: 300);
        throw new Exception($"{law}\nminimal script ({tries} tries): {min}\n{Failure(min)}\n\noriginal script: {s}");
    }

    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void ShortScripts_SameWorld_HoweverSteppedOrSaved() =>
        ActionGen.ScriptGen(5, 10, scale: 0.25).Check(Holds, iter: 4);

    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void LongScripts_SameWorld_HoweverSteppedOrSaved() =>
        ActionGen.ScriptGen(5, 40, scale: 1.0).Check(Holds, iter: Iterations("ACTION_SEQ_ITER", 60));

    /// Found by the long property (CsCheck seed e5dkWcb4mia1), minimised, when the walk was refused at a
    /// tenth of Moving (now at speed zero, Traversal.CannotWalk). It was checked when a walk starts
    /// (Traversal.Refusal) and never during it (Traversal.Advance): a
    /// player whose Moving capacity falls to 0.1 part-way across the tile finishes the crossing in
    /// one long step, but the same minutes cut into 17-minute steps stop at the next step, refused,
    /// five minutes short of the far side. The worlds differ from $.Minute on (1132 vs 1127).
    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void Regression_ExhaustedMidCrossing_StopsWhereverTheStepsAreCut() =>
        Assert.Null(Failure(new Script(new(17, Kit: true, Npc: true, Carcass: true, Fight: false, Mild: true),
            [new(Act.Gather, 19, 89), new(Act.Butcher, 21, 52), new(Act.Wait, 10, 161), new(Act.Gather, 56, 19), new(Act.Wait, 60, 111),
             new(Act.Gather, 19, 137), new(Act.Rest, 24, 61), new(Act.Gather, 42, 129), new(Act.Wait, 8, 143), new(Act.LightTorch, 40, 4),
             new(Act.LightTorch, 7, 4), new(Act.Gather, 20, 44), new(Act.Rest, 59, 145), new(Act.AddFuel, 26, 13), new(Act.Travel, 40, 26)],
            Chunks: [17], Saves: [])));

    static long Iterations(string variable, long fallback) =>
        long.TryParse(Environment.GetEnvironmentVariable(variable), out var n) && n > 0 ? n : fallback;
}
