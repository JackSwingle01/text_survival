using CsCheck;
using Survival;

namespace Survival.Tests.Verification;

/// A transformation's finish minute does not depend on who is watching or how the world is stepped
/// (spec 2026-11-24 fire-and-rack-as-stations, "Stepping"): the same fire, with the same piece on it,
/// advanced in one step or in many small ones, with the player standing at it or far away, finishes in
/// the same minute, or not at all. Snow melting beside it gives each litre in the same minute, and stops at the
/// same cap.
public class StationSteppingTests
{
    static readonly AgentId P = AgentId.Player;

    public sealed record Scene(double BirchKg, double PineKg, int[] Chunks, bool Present);

    static readonly Gen<Scene> Scenes = Gen.Select(Gen.Double[0, 3], Gen.Double[0, 2], Gen.Int[1, 40].Array[1, 12], Gen.Bool,
        (birch, pine, chunks, present) => new Scene(birch, pine, chunks, present));

    const int Horizon = 180;

    static string FinishMinutes(Scene s, bool reference)
    {
        var w = ConservationWorlds.Bare(-5);
        var camp = Map.Of(w).Camp;
        var f = new FireState { Tile = camp, LastUpdatedMinute = w.Minute };
        if (s.BirchKg > 0.01) f.BurningKg[Resource.Birch] = s.BirchKg;
        if (s.PineKg > 0.01) f.UnburnedKg[Resource.Pine] = s.PineKg;
        w.Fires[new FireId(w.NextFireId++)] = f;
        Items.AddPiece(Items.Pack(w, P), Resource.RawMeat, 0.4);
        Assert.Equal(1, Sim.Step(w, new PutOn(P, "cook_meat"), 1).MinutesElapsed);
        Assert.Equal(1, Sim.Step(w, new PutOn(P, "melt_snow"), 1).MinutesElapsed);
        if (!reference && !s.Present) w.Agent(P).Tile = Map.Step(Map.Step(Map.Step(camp, Direction.West), Direction.West), Direction.West);

        var events = new List<WorldEvent>();
        if (reference) events.AddRange(Sim.Advance(w, Horizon));
        else
        {
            int left = Horizon, i = 0;
            while (left > 0)
            {
                int n = Math.Min(left, s.Chunks[i++ % s.Chunks.Length]);
                events.AddRange(Sim.Advance(w, n));
                left -= n;
            }
        }
        return string.Join(", ", events.OfType<TransformationFinished>().Select(e => $"{e.Recipe} {e.Minute}"));
    }

    static void Run(Scene s) => Assert.Equal(FinishMinutes(s, reference: true), FinishMinutes(s, reference: false));

    [Fact]
    public void TheFinishMinute_IsTheSame_HoweverTheWorldIsStepped_AndWhoeverIsThere() => Scenes.Check(Run, iter: 12);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void TheFinishMinute_IsTheSame_HoweverTheWorldIsStepped_AndWhoeverIsThere_Many() => Scenes.Check(Run, iter: 300);
}
