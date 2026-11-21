using Survival;
using Xunit;

public class ReportTests
{
    /// A populated world a couple of days on, with one of each thing the player can leave behind.
    static World Lived(int seed = 17)
    {
        var w = TestKit.Supply(World.New(seed));
        var camp = Map.Of(w).Camp;
        for (int i = 0; i < 10 && Fire.PitAt(w, camp) == null; i++)
            Sim.Step(w, new StartFire(AgentId.Player, ToolType.FireStriker, Resource.BirchBark, 0.05, 1.0), 1);
        Sim.Step(w, new Stash(AgentId.Player, Resource.Kindling, 1), 1);
        Sim.Step(w, new Gather(AgentId.Player, 0), 60);
        Carcasses.Create(w, camp, "Caribou", SpeciesTable.NewBody(Species.Caribou, w.Rng("test")), w.Minute);
        Sim.Advance(w, 2 * 1440);
        return w;
    }

    [Fact]
    public void Dump_is_deterministic()
    {
        Assert.Equal(Report.DumpText(Lived()), Report.DumpText(Lived()));
    }

    [Fact]
    public void Dump_covers_every_kind_present_and_distribution_counts_every_row()
    {
        var w = Lived();
        var finds = Report.Finds(w);
        var kinds = finds.Select(f => f.Kind).ToHashSet();
        foreach (var kind in new[] { "carcass", "stash", "fire pit", "shelter", "bedding", "place", "stock", "worked forage", "tracks", "herd" })
            Assert.Contains(kind, kinds);
        Assert.Equal(w.Agents.Count - 1, finds.Count(f => f.Category == FindCategory.Agent || f.Kind == "body"));

        var d = Report.Distribution(w, finds);
        Assert.Equal(finds.Count, d.Total);
        Assert.Equal(finds.Count, d.Regions.Sum(r => r.Total));
    }

    [Fact]
    public void Curve_has_one_point_per_chunk()
    {
        var w = World.New(17);
        var curve = Report.Curve(w, 4, 2);
        Assert.Equal(2, curve.Count);
        Assert.Equal([2, 4], curve.Select(p => p.Day));
        Assert.Throws<ArgumentException>(() => Report.Curve(w, 5, 2));
    }
}
