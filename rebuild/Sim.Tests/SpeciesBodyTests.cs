namespace Survival.Tests;

/// The species round (dev/active/rebuild-roadmap.md, 2026-11-21): realistic bodies, real running, Kleiber's
/// metabolism for animals, a calorie store that scales with mass, and one sprint reserve per body.
/// Expected values come from documentation/species-stats-research.md, not from the code.
public class SpeciesBodyTests
{
    static Motion M(Species s) => SpeciesTable.Of(s).Motion;
    static BodyState Fresh(Species s) => SpeciesTable.Average(s);
    static Rng TestRng() => Rng.ForSystem(1, "test");

    // ---- who outruns whom (research §3.1, §6) ----

    [Fact]
    public void Caribou_OutsprintsAWolf_ButTheWolfOutlastsIt()
    {
        Assert.True(M(Species.Caribou).TopSpeedMps > M(Species.Wolf).TopSpeedMps);
        Assert.True(M(Species.Wolf).EnduranceSpeedMps > M(Species.Caribou).EnduranceSpeedMps);
    }

    [Theory]
    [InlineData(Species.Wolf)]
    [InlineData(Species.Bear)]
    [InlineData(Species.Homotherium)]
    [InlineData(Species.Hyena)]
    [InlineData(Species.Caribou)]
    [InlineData(Species.Bison)]
    public void APerson_CannotOutrunIt(Species s)
    {
        var person = M(Species.Human);
        Assert.True(M(s).TopSpeedMps > person.TopSpeedMps, $"a person outsprints a {s}");
    }

    [Fact]
    public void APerson_OutsprintsAMammoth()
    {
        Assert.True(M(Species.Human).TopSpeedMps > M(Species.Mammoth).TopSpeedMps);
    }

    // ---- build ----

    [Fact]
    public void Animals_AreHalfMuscleOrLess_WolvesAndMammothsLean()
    {
        foreach (var s in Enum.GetValues<Species>().Where(s => SpeciesTable.Of(s).IsAnimal))
            Assert.InRange(SpeciesTable.Of(s).MusclePct, 0.40, 0.50);
        Assert.InRange(SpeciesTable.Of(Species.Wolf).FatPct, 0.08, 0.12);
        Assert.InRange(SpeciesTable.Of(Species.Mammoth).FatPct, 0.08, 0.12);
    }

    [Fact]
    public void TheBaselinePerson_IsAnUpperPalaeolithicMan()
    {
        var man = BodyState.BaselineHuman();
        Assert.Equal(68, man.WeightKg, 6);
        Assert.Equal(0.12, man.FatPct, 6);
        Assert.Equal(0.38, man.MusclePct, 6);
    }

    [Fact]
    public void AWoman_IsSmallerFatterAndSlowerThanAMan()
    {
        var woman = BodyState.BaselineHuman(Sex.Female);
        Assert.Equal(55, woman.WeightKg, 6);
        Assert.Equal(0.22, woman.FatPct, 6);
        Assert.Equal(0.31, woman.MusclePct, 6);
        Assert.Equal(7.3, Body.TopSpeedMps(woman, SpeciesTable.Running(Species.Human, woman)), 6);
        Assert.Equal(2.9, Body.EnduranceSpeedMps(SpeciesTable.Running(Species.Human, woman)), 6);
    }

    [Fact]
    public void Homotherium_ReplacesTheSabreTooth_AtAbout190Kg()
    {
        Assert.Equal(190, SpeciesTable.Of(Species.Homotherium).WeightKg);
    }

    // ---- metabolism ----

    [Fact]
    public void AnAnimal_BurnsKleibersRate()
    {
        // 70 M^0.75 kcal a day: a 45 kg wolf about 1,220, a 600 kg bison about 8,480 (research §1, rounded).
        Assert.InRange(Body.MetabolismKcalPerDay(BodyState.Create(BodyKind.Quadruped, 45, 0.10, 0.48), 1.0), 1205, 1235);
        Assert.InRange(Body.MetabolismKcalPerDay(BodyState.Create(BodyKind.Quadruped, 600, 0.12, 0.45), 1.0), 8400, 8560);
    }

    [Fact]
    public void APerson_KeepsKatchMcArdle()
    {
        // 370 + 21.6 x lean kg: a 68 kg man at 12% fat has 59.84 kg lean, 1,662.544 kcal a day.
        Assert.Equal(1662.544, Body.MetabolismKcalPerDay(BodyState.BaselineHuman(), 1.0), 6);
    }

    [Fact]
    public void TheCalorieStore_IsTheOld2000_ForTheBaselineMan()
    {
        Assert.Equal(2000, Body.CaloriesCapacityKcal(BodyState.BaselineHuman()), 6);
    }

    /// Gut contents and glycogen: about half a day to a day of what the body burns (research §3.2), for a
    /// hare and a mammoth alike. Old: 2,000 kcal for every body, which a mammoth emptied in under an hour.
    [Theory]
    [InlineData(Species.Rabbit)]
    [InlineData(Species.Wolf)]
    [InlineData(Species.Caribou)]
    [InlineData(Species.Bison)]
    [InlineData(Species.Mammoth)]
    public void TheCalorieStore_LastsUnderADayToADayAndAHalf_OfRest(Species s)
    {
        var body = Fresh(s);
        double hours = Body.CaloriesCapacityKcal(body) / (Body.MetabolismKcalPerDay(body, 1.0) / 24);
        Assert.InRange(hours, 12, 36);
    }

    // ---- the sprint reserve ----

    [Fact]
    public void AFreshBody_HasAFullReserve_AndRunsAtItsSpeciesSpeeds()
    {
        var man = BodyState.BaselineHuman();
        var running = SpeciesTable.Running(Species.Human, man);
        Assert.Equal(1.0, Body.SprintReservePct(man));
        Assert.Equal(8.5, Body.TopSpeedMps(man, running), 6);
        Assert.Equal(3.3, Body.EnduranceSpeedMps(running), 6);
    }

    [Fact]
    public void AStrike_TakesItsSliceOfTheReserve()
    {
        var man = BodyState.BaselineHuman();
        Body.Burst(man, SpeciesTable.Running(Species.Human, man));
        Assert.Equal(1 - 0.12, Body.SprintReservePct(man), 9);
    }

    [Fact]
    public void AnEmptyingReserve_SlowsTheSprintTowardEnduranceSpeed()
    {
        var man = BodyState.BaselineHuman();
        var running = SpeciesTable.Running(Species.Human, man);
        Body.Sprint(man, running, 60);
        Assert.Equal(0, Body.SprintReservePct(man), 9);
        Assert.Equal(3.3, Body.TopSpeedMps(man, running), 6);
    }

    /// A person's reserve refills with a 6 minute time constant at rest (research §1, Skiba 2012): after
    /// six minutes lived, a third of what was spent is still missing.
    [Fact]
    public void TheReserve_RefillsOverTheRecoveryTime()
    {
        var wolf = Fresh(Species.Wolf);
        var man = BodyState.BaselineHuman();
        Body.Sprint(man, SpeciesTable.Running(Species.Human, man), 60);
        for (int i = 0; i < 6; i++) Body.TickWounds(man, TestRng());
        Assert.Equal(1 - Math.Exp(-1), Body.SprintReservePct(man), 9);
        // A heavier body recovers more slowly, as (mass)^0.25: physiological time.
        Assert.True(Body.RecoverySeconds(Fresh(Species.Bison)) > Body.RecoverySeconds(wolf));
    }

    [Fact]
    public void EmptyingTheReserve_CostsALittleSleep_Once()
    {
        var man = BodyState.BaselineHuman();
        double before = man.EnergyMinutes;
        var running = SpeciesTable.Running(Species.Human, man);
        for (int i = 0; i < 20; i++) Body.Burst(man, running);
        Assert.Equal(before - Body.SpentReserveEnergyMinutes, man.EnergyMinutes, 9);
        Assert.InRange(Body.SpentReserveEnergyMinutes, 1, 10);
    }

    [Fact]
    public void ALoad_CutsTheSpeedAndStretchesEachBurst()
    {
        var man = BodyState.BaselineHuman();
        var free = SpeciesTable.Running(Species.Human, man);
        var laden = SpeciesTable.Running(Species.Human, man, new AbilityContext(EncumbrancePct: 2.0));
        Assert.True(Body.TopSpeedMps(man, laden) < Body.TopSpeedMps(man, free));
        Assert.True(Body.EnduranceSpeedMps(laden) < Body.EnduranceSpeedMps(free));
        Body.Burst(man, laden);
        Assert.True(1 - Body.SprintReservePct(man) > 0.12, "a laden strike cost no more than a free one");
    }

    [Fact]
    public void AWoundedLeg_CutsTheSpeed()
    {
        var wolf = Fresh(Species.Wolf);
        double before = Body.TopSpeedMps(wolf, SpeciesTable.Running(Species.Wolf, wolf));
        var leg = wolf.Regions.First(r => r.Name.Contains("Leg"));
        leg.Muscle.Condition = 0.2;
        leg.Bone.Condition = 0.4;
        Assert.True(Body.TopSpeedMps(wolf, SpeciesTable.Running(Species.Wolf, wolf)) < before * 0.95);
    }

    // ---- what running and striking cost ----

    /// A 68 kg man sprinting flat out for 15 s, slowing as his reserve empties, covers about 100 m at
    /// 4 J/kg/m: about 6.5 kcal (research §5.3 puts 30 s of hard running at about 17).
    [Fact]
    public void ASprint_CostsWhatTheDistanceCosts()
    {
        var man = BodyState.BaselineHuman();
        double before = man.CaloriesKcal;
        Body.Sprint(man, SpeciesTable.Running(Species.Human, man), 15);
        Assert.InRange(before - man.CaloriesKcal, 5, 12);
    }

    /// A strike is about 20 METs for the seconds of sprint it costs: under a kilocalorie for a person.
    [Fact]
    public void AStrike_CostsUnderAKilocalorie()
    {
        var man = BodyState.BaselineHuman();
        double before = man.CaloriesKcal;
        Body.Burst(man, SpeciesTable.Running(Species.Human, man));
        Assert.InRange(before - man.CaloriesKcal, 0.3, 1.2);
    }

    [Fact]
    public void SpendingPastAnEmptyStore_BurnsFat()
    {
        var man = BodyState.BaselineHuman();
        man.CaloriesKcal = 0;
        double fat = man.FatKg;
        Body.SpendKcal(man, 77);
        Assert.Equal(fat - 0.01, man.FatKg, 9);
    }

    // ---- in a fight ----

    static (World, Encounter) PlayerAndWolf(int distanceM = 1)
    {
        var (w, t) = CombatTests.Field();
        var wolf = CombatTests.Herd(w, Species.Wolf, 1, t);
        w.Agent(AgentId.Player).Members[0] = BodyState.BaselineHuman(); // the reference man: running at exactly his kind's
        return (w, Combat.Create(w, [new Fighter(AgentId.Player)], [new Fighter(wolf)], t, distanceM, Awareness.Engaged, Awareness.Engaged));
    }

    [Fact]
    public void ADodge_DrawsOnTheReserve_NotOnSleep()
    {
        var (w, e) = PlayerAndWolf();
        var me = w.Agent(AgentId.Player).Body;
        double energy = me.EnergyMinutes;
        int dodged = 0;
        for (int i = 0; i < 40 && dodged == 0; i++)
        {
            e.Units[0].DodgeSet = true;
            if (Combat.Apply(w, e, e.Units[1], CombatAction.Attack, []).Dodged) dodged++;
            Combat.BodyOf(w, e.Units[0]).Regions.ForEach(r => { r.Skin.Condition = r.Muscle.Condition = r.Bone.Condition = 1; });
            me.BloodPct = 1;
            me.Effects.Clear();
        }
        Assert.Equal(1, dodged);
        Assert.True(Body.SprintReservePct(me) < 1, "the dodge cost no breath");
        Assert.Equal(energy, me.EnergyMinutes, 9);
    }

    [Fact]
    public void ASpentFighter_CannotDodge()
    {
        var (w, e) = PlayerAndWolf();
        var me = w.Agent(AgentId.Player).Body;
        for (int i = 0; i < 100; i++)
        {
            me.SprintSpentLevel = 1;
            e.Units[0].DodgeSet = true;
            Assert.False(Combat.Apply(w, e, e.Units[1], CombatAction.Attack, []).Dodged);
            Combat.BodyOf(w, e.Units[0]).Regions.ForEach(r => { r.Skin.Condition = r.Muscle.Condition = r.Bone.Condition = 1; });
            me.BloodPct = 1;
            me.Effects.Clear();
        }
    }

    [Fact]
    public void Striking_CostsCaloriesAndBreath()
    {
        var (w, e) = PlayerAndWolf();
        var me = w.Agent(AgentId.Player).Body;
        double kcal = me.CaloriesKcal;
        Combat.Apply(w, e, e.Units[0], CombatAction.Attack, []);
        Assert.True(me.CaloriesKcal < kcal);
        Assert.Equal(1 - 0.12, Body.SprintReservePct(me), 9);
    }

    [Fact]
    public void ALoad_ShortensTheStepsAFighterTakes()
    {
        var (w, e) = PlayerAndWolf(10);
        int free = Combat.Allowance(w, e.Units[0]);
        Items.Add(Items.Pack(w, AgentId.Player), Resource.RawMeat, 40);
        Assert.True(Combat.Allowance(w, e.Units[0]) < free);
    }

    /// The reserve refills a minute at a time, so how the minutes are cut and a save between them change nothing.
    [Fact]
    public void Recovery_IsTheSame_HoweverTheMinutesAreCut_AndThroughASave()
    {
        World Spent()
        {
            var w = World.New(5);
            var me = w.Agent(AgentId.Player).Body;
            Body.Sprint(me, SpeciesTable.Running(Species.Human, me), 60);
            return w;
        }
        var once = Spent();
        Sim.Step(once, new Rest(AgentId.Player), 45);
        var often = Spent();
        for (int i = 0; i < 45; i++) Sim.Step(often, new Rest(AgentId.Player), 1);
        var saved = Spent();
        Sim.Step(saved, new Rest(AgentId.Player), 20);
        saved = Save.FromJson(Save.ToJson(saved));
        Sim.Step(saved, new Rest(AgentId.Player), 25);
        Assert.True(Body.SprintReservePct(once.Agent(AgentId.Player).Body) is > 0.9 and < 1);
        Assert.Equal(Save.ToJson(once), Save.ToJson(often));
        Assert.Equal(Save.ToJson(once), Save.ToJson(saved));
    }

    /// A round of a fight in which the player only waits pays basal metabolism, once: the old flat factor
    /// of 2.0 would charge it twice on top of what strikes and steps now pay for themselves.
    [Fact]
    public void AFightRound_PaysBasalMetabolismOnce()
    {
        var (w, t) = CombatTests.Field();
        var wolf = CombatTests.Herd(w, Species.Wolf, 1, t);
        Combat.Create(w, [new Fighter(AgentId.Player)], [new Fighter(wolf)], t, 40, Awareness.Engaged, Awareness.Unaware);
        var me = w.Agent(AgentId.Player).Body;
        double kcal = me.CaloriesKcal, basal = Body.MetabolismKcalPerDay(me, 1.0) / 1440;
        Sim.Step(w, new Fight(AgentId.Player, CombatAction.Wait), 1);
        Assert.Equal(kcal - basal, me.CaloriesKcal, 9);
    }

    // ---- well fed for the autumn (decided 2026-11-21) ----

    [Fact]
    public void ThePlayerAndEveryNpc_StartWellFed_EachTheirOwn()
    {
        var fats = new List<double>();
        foreach (int seed in new[] { 1, 2, 3, 4 })
        {
            var w = World.New(seed);
            var npc = Npc.Spawn(w, Map.Of(w).Camp);
            foreach (var body in new[] { w.Agent(AgentId.Player).Body, w.Agent(npc).Body })
            {
                Assert.InRange(body.FatPct, 0.15, 0.18);
                Assert.Equal(59.84, body.StructuralKg + body.MuscleKg, 9); // the man's lean mass: the fat is laid on it
                fats.Add(body.FatPct);
            }
        }
        Assert.True(fats.Distinct().Count() == fats.Count, "two people drew the same fat");
        Assert.Equal(World.New(3).Agent(AgentId.Player).Body.FatKg, World.New(3).Agent(AgentId.Player).Body.FatKg);
    }

    [Fact]
    public void AWoman_StartsWellFedFromHerOwnBuild()
    {
        var woman = SpeciesTable.NewBody(Species.Human, Rng.ForSystem(9, "test"), Sex.Female);
        Assert.Equal(Sex.Female, woman.Sex);
        Assert.InRange(woman.FatPct, 0.25, 0.28);
    }
}
