using Survival;
using Xunit;

namespace Survival.Tests;

/// The sink the food chain bench reads: what each animal took in, from what, in kcal.
public class IntakeSinkTests
{
    sealed class Log : IIntakeSink
    {
        public readonly List<(AgentId Who, IntakeKind Kind, double Kcal, Cause? Cause)> Took = [];
        void IIntakeSink.Took(Agent animal, IntakeKind kind, double kcal, Cause? killedBy) => Took.Add((animal.Id, kind, kcal, killedBy));
        public double Kcal(IntakeKind kind) => Took.Where(t => t.Kind == kind).Sum(t => t.Kcal);
    }

    static double Held(BodyState b) => b.CaloriesKcal + Body.KcalPerKgFat * b.FatKg + Body.KcalPerKgMuscle * b.MuscleKg;

    static (World World, Agent Wolf, Log Log) WolfAtACarcass(Cause cause)
    {
        var w = PlannerTests.Strip();
        var log = new Log();
        w.Intake = log;
        var tile = new TileId(20, 1);
        Combat.LeaveCarcass(w, tile, Species.Caribou, BodyState.Create(SpeciesTable.Of(Species.Caribou).Body, 110, 0.08, 0.47), cause);
        var wolf = w.Agent(Agents.Spawn(w, Species.Wolf, tile));
        wolf.Body.CaloriesKcal = 0.2 * Body.CaloriesCapacityKcal(wolf.Body);
        return (w, wolf, log);
    }

    [Fact]
    public void AWolfFeedingAtACarcass_TakesInWhatItsBodyGainsPlusWhatItBurns()
    {
        var (w, wolf, log) = WolfAtACarcass(new Cause(null, "a test"));
        double before = Held(wolf.Body);
        const int minutes = 30;
        Sim.Advance(w, minutes);
        double burned = minutes * Fauna.BurnKcalPerMinute(wolf.Body);
        Assert.True(log.Kcal(IntakeKind.Carcass) > 100, "the wolf never fed");
        Assert.Equal(Held(wolf.Body) - before + burned, log.Kcal(IntakeKind.Carcass), tolerance: 0.01 * burned + 1);
        Assert.All(log.Took, t => Assert.Equal(wolf.Id, t.Who));
    }

    [Fact]
    public void ACarcass_NamesItsKillerToTheSink()
    {
        var killer = new AgentId(4242);
        var (w, _, log) = WolfAtACarcass(new Cause(killer, "a wolf"));
        Sim.Advance(w, 5);
        Assert.Contains(log.Took, t => t.Kind == IntakeKind.Carcass && t.Cause?.Who == killer);
    }

    [Fact]
    public void AGrazingCaribou_TakesInWhatItsBodyGainsPlusWhatItBurns()
    {
        var w = PlannerTests.Strip();
        w.PoolCache = null; // the plain grows forage
        var log = new Log();
        w.Intake = log;
        var caribou = w.Agent(Agents.Spawn(w, Species.Caribou, new TileId(20, 1)));
        caribou.Body.CaloriesKcal = 0.3 * Body.CaloriesCapacityKcal(caribou.Body);
        double before = Held(caribou.Body);
        const int minutes = 60;
        Sim.Advance(w, minutes);
        double burned = minutes * Fauna.BurnKcalPerMinute(caribou.Body);
        Assert.True(log.Kcal(IntakeKind.Grazed) > 100, "the caribou never grazed");
        Assert.Equal(Held(caribou.Body) - before + burned, log.Kcal(IntakeKind.Grazed), tolerance: 0.01 * burned + 1);
    }

    [Fact]
    public void WithNoSink_NothingChanges()
    {
        string Run(bool sink)
        {
            var (w, _, _) = WolfAtACarcass(new Cause(null, "a test"));
            if (!sink) w.Intake = null;
            Sim.Advance(w, 60);
            return Save.ToJson(w);
        }
        Assert.Equal(Run(sink: false), Run(sink: true));
    }
}
