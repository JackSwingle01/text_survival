namespace Survival.Tests;

/// Carcasses and butchering (old CarcassFeature, ButcherStrategy). Goldens are exact outputs of the
/// old CarcassFeature on the same animal bodies (harness golden3a: old AnimalFactory bodies, their
/// kg copied here). Old CarcassFeatureButcheringOverflowTests ported.
public class CarcassTests
{
    static readonly AgentId P = AgentId.Player;
    static readonly WeatherSample Mild = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);
    const int Digits = 9;

    /// Old AnimalFactory bodies: weight, muscle, fat (kg), bird, megafauna.
    static readonly Dictionary<string, (double W, double Muscle, double Fat, bool Bird, bool Mega)> Animals = new()
    {
        ["Rabbit"] = (2.062641801423291, 1.0313209007116455, 0.2062641801423291, false, false),
        ["Caribou"] = (124.4465826469443, 74.66794958816658, 18.666987397041645, false, false),
        ["Wolf"] = (41.52815800699646, 24.91689480419788, 8.305631601399293, false, false),
        ["Bear"] = (225.99621700503752, 124.29791935277063, 67.79886510151125, false, false),
        ["Mammoth"] = (6344.009779679869, 3172.0048898399345, 2220.403422887954, false, true),
        ["Ptarmigan"] = (0.5513861826959172, 0.3308317096175503, 0.08270792740438758, true, false),
    };

    static (World, Carcass) Make(string animal, double effectiveHours = 0)
    {
        var w = World.New(17);
        w.FixedWeather = Mild;
        var (kg, muscle, fat, bird, mega) = Animals[animal];
        var body = BodyState.Create(BodyKind.Quadruped, kg, fat / kg, muscle / kg);
        var special = animal == "Mammoth" ? new Dictionary<Resource, double> { [Resource.Ivory] = 4, [Resource.MammothHide] = 15 } : null;
        var c = Carcasses.Create(w, Map.Of(w).Camp, animal, body, 0, bird, mega, special: special);
        c.EffectiveHours = effectiveHours;
        return (w, c);
    }

    // Old yields: meat, bone, hide, sinew, fat, feather; and minutes for quick, careful, full.
    public static readonly TheoryData<string, double[], int[]> Yields = new()
    {
        { "Rabbit", [0.6703585854625697, 0.2062641801423291, 0.2062641801423291, 0.10313209007116456, 0.12375850808539746, 0], [17, 34, 51] },
        { "Caribou", [48.53416723230828, 7.777911415434017, 12.44465826469443, 6.222329132347215, 11.200192438224986, 0], [134, 267, 401] },
        { "Wolf", [16.195981622728624, 2.0764079003498224, 4.1528158006996465, 2.0764079003498233, 4.983378960839576, 0], [56, 112, 167] },
        { "Bear", [80.79364757930091, 8.474858137688908, 22.599621700503754, 11.299810850251877, 40.67931906090675, 0], [241, 481, 721] },
        // Megafauna caps meat, fat and bone but not sinew (old quirk, kept: 317 kg of sinew).
        { "Mammoth", [80, 8, 0, 317.20048898399347, 15, 0], [619, 1238, 1857] },
        { "Ptarmigan", [0.21504061125140772, 0.034461636418494826, 0, 0.027569309134795862, 0.04962475644263255, 0.04411089461567338], [16, 32, 47] },
    };

    [Theory, MemberData(nameof(Yields))]
    public void Yields_AndButcheringTimes_MatchOld(string animal, double[] kg, int[] minutes)
    {
        var (_, c) = Make(animal);
        Resource[] order = [Resource.RawMeat, Resource.Bone, Resource.Hide, Resource.Sinew, Resource.RawFat, Resource.Feather];
        for (int i = 0; i < order.Length; i++) Assert.Equal(kg[i], c.Remaining.GetValueOrDefault(order[i]), Digits);
        Assert.Equal(minutes, new[] { ButcherMode.QuickStrip, ButcherMode.Careful, ButcherMode.FullProcessing }.Select(m => Carcasses.RemainingMinutes(c, m)));
    }

    // Old CarcassFeature.Harvest(all of it, careful) by decay stage, with a knife and without:
    // effective hours, knife, meat, bone, hide, then decay level.
    public static readonly TheoryData<string, double, bool, double, double, double> Butchering = new()
    {
        { "Caribou", 0, true, 48.53416723230827, 7.777911415434017, 12.44465826469443 },
        { "Caribou", 0, false, 24.252425445636145, 5.2112006483407916, 0 },
        { "Caribou", 8, true, 34.94460040726195, 7.777911415434017, 12.44465826469443 },
        { "Caribou", 18, true, 23.296400271507974, 7.777911415434017, 12.44465826469443 },
        { "Caribou", 30, false, 6.40651007466469, 5.2112006483407916, 0 },
        { "Bear", 8, false, 29.058704065846857, 5.678154952251569, 0 },
        { "Wolf", 30, true, 4.275739148400355, 2.0764079003498224, 4.1528158006996465 },
        { "Rabbit", 30, false, 0.1, 0.1381970006953605, 0 },       // the 0.1 kg meat floor
        { "Ptarmigan", 0, true, 0.21504061125140772, 0, 0 },       // bone under 0.05 kg is not worth keeping
        { "Mammoth", 18, true, 38.4, 8, 0 },
    };

    [Theory, MemberData(nameof(Butchering))]
    public void Butchering_ByDecayAndBlade_MatchesOld(string animal, double hours, bool knife, double meat, double bone, double hide)
    {
        var (_, c) = Make(animal, hours);
        var got = Carcasses.Harvest(c, Carcasses.RemainingMinutes(c, ButcherMode.Careful), knife, false, ButcherMode.Careful);
        // Old meat came off in random portions and dropped a last scrap of up to 0.05 kg; stacks by weight keep it.
        Assert.InRange(Items.Have(got, Resource.RawMeat), meat - 1e-9, meat + 0.05);
        Assert.Equal(bone, Items.Have(got, Resource.Bone), Digits);
        Assert.Equal(hide, Items.Have(got, Resource.Hide), Digits);
    }

    [Fact]
    public void ModesAndPartialSessions_MatchOld()
    {
        var (_, quick) = Make("Caribou");
        var q = Carcasses.Harvest(quick, Carcasses.RemainingMinutes(quick, ButcherMode.QuickStrip), true, true, ButcherMode.QuickStrip);
        Assert.Equal(31.0618670286773, Items.Have(q, Resource.RawMeat), Digits);
        Assert.Equal(3.111164566173607, Items.Have(q, Resource.Bone), Digits);
        Assert.Equal(0, Items.Have(q, Resource.Hide));
        Assert.Equal(4.480076975289995, Items.Have(q, Resource.RawFat), Digits);

        var (_, full) = Make("Caribou");
        var f = Carcasses.Harvest(full, 30, true, false, ButcherMode.FullProcessing);
        Assert.Equal(4.505415162454874, Items.Have(f, Resource.RawMeat), Digits);
        Assert.Equal(0.6301279947489334, Items.Have(f, Resource.Sinew), Digits);
        Assert.Equal(78.90653121028166, Carcasses.TotalRemainingKg(full), Digits);

        var (_, mammoth) = Make("Mammoth");
        Carcasses.Harvest(mammoth, Carcasses.RemainingMinutes(mammoth, ButcherMode.Careful), false, false, ButcherMode.Careful);
        Assert.Equal(19, Carcasses.TotalRemainingKg(mammoth), Digits); // ivory and the great hide wait for a blade
    }

    /// Old DecayLevel through ApplyTemperatureDecay: 3 h at 60F, 10 h at 40F, 10 h at 20F, 20 h at 10F, 40 h at -5F.
    [Fact]
    public void DecayTimeline_MatchesOld()
    {
        double h = 0;
        (double TempF, int Hours, double Eff, double Level)[] steps =
            [(60, 3, 6, 0.275), (40, 10, 16, 0.6), (20, 10, 21, 0.725), (10, 20, 26, 0.84), (-5, 40, 30, 0.92)];
        foreach (var (tempF, hours, eff, level) in steps)
        {
            h += hours * Carcasses.DecayFactor(tempF);
            Assert.Equal(eff, h, 9);
            Assert.Equal(level, Carcasses.DecayLevel(h), 9);
        }
    }

    [Fact]
    public void Decay_CatchesUpLazily_AndHowOftenYouLookChangesNothing()
    {
        var (a, ca) = Make("Caribou");
        var (b, cb) = Make("Caribou");
        a.Minute = b.Minute = 0;
        for (int m = 0; m < 2000; m += 37) { b.Minute = m; Carcasses.CatchUp(b, Map.Of(b).Camp, cb); }
        a.Minute = b.Minute = 2000;
        Carcasses.CatchUp(a, Map.Of(a).Camp, ca);
        Carcasses.CatchUp(b, Map.Of(b).Camp, cb);
        Assert.Equal(ca.EffectiveHours, cb.EffectiveHours, 12);
        Assert.Equal(2000 / 60.0 * Carcasses.DecayFactor(Weather.AirAtF(a, Map.Of(a).Camp, Mild, 0)), ca.EffectiveHours, 9);
    }

    // ---- through Sim.Step ----

    [Fact]
    public void Butcher_RunsItsTime_FillsThePack_BloodiesYou_AndLeavesNoCarcass()
    {
        var (w, c) = Make("Rabbit");
        Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
        var r = Sim.Step(w, new Butcher(P, c.Id), 120);
        Assert.Equal(("butchered", 34), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Contains(r.Events, e => e is Butchered { Finished: true });
        // All of it, less the 34 minutes' decay at the camp's air (decay costs meat only).
        double decay = Carcasses.DecayLevel(34 / 60.0 * Carcasses.DecayFactor(Weather.AirAtF(w, Map.Of(w).Camp, Mild, 0)));
        Assert.Equal(0.6703585854625697 * (1 - decay * 0.8), Items.Have(Items.Pack(w, P), Resource.RawMeat), 9);
        Assert.Empty(w.Tiles[Map.Of(w).Camp].Carcasses!);
        Assert.True(Effects.Severity(w.Agent(P).Body, EffectKind.Bloody) > 0);
        Assert.Equal("no such carcass here", Sim.Step(w, new Butcher(P, c.Id), 10).InterruptedReason);
    }

    /// Old CarcassFeatureButcheringOverflowTests: a full pack leaves the rest on the carcass, and
    /// partial sessions eventually finish it.
    [Fact]
    public void AFullPack_LeavesYieldOnTheCarcass_AndSessionsEventuallyFinishIt()
    {
        var (w, c) = Make("Caribou");
        Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
        double total = Carcasses.TotalRemainingKg(c);
        var r = Sim.Step(w, new Butcher(P, c.Id), 24 * 60);
        Assert.Equal("butchered", r.InterruptedReason);
        Assert.Equal(Items.CapacityKg(Items.Pack(w, P)), Items.CarriedKg(w.Agent(P)), 6);
        Assert.InRange(Carcasses.TotalRemainingKg(c), total - 20, total - 9); // ~10 kg taken, the rest of the meat's loss is decay
        Assert.Contains(c, w.Tiles[Map.Of(w).Camp].Carcasses!);
        for (int i = 0; i < 40 && !Carcasses.IsButchered(c); i++)
        {
            Sim.Step(w, new Stash(P, Resource.RawMeat, 99), 1);
            foreach (var res in new[] { Resource.Bone, Resource.Hide, Resource.Sinew, Resource.RawFat }) Sim.Step(w, new Stash(P, res, 99), 1);
            Sim.Step(w, new Butcher(P, c.Id), 60);
            w.Agent(P).Body.EnergyMinutes = Body.MaxEnergyMinutes;
            w.Agent(P).Body.CaloriesKcal = 1500; w.Agent(P).Body.HydrationMl = 3000;
        }
        Assert.True(Carcasses.IsButchered(c));
    }

    /// old game: a frozen carcass's 50% time penalty lengthened the session but the harvest credited
    /// the inflated minutes, so a partial session on frozen meat took off more, not less (bug, fixed).
    [Fact]
    public void AFrozenCarcass_ButchersSlower()
    {
        (World, Carcass) Session(bool frozen)
        {
            var (w, c) = Make("Caribou");
            Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
            Items.Pack(w, P).Gear.Add(Items.Make("large_bag"));
            w.Minute = 3 * 60;
            c.LastUpdatedMinute = w.Minute; // decayed to now; frozen is the last air it felt
            c.LastKnownTempF = frozen ? 5 : 41;
            Assert.Equal(frozen, Carcasses.IsFrozen(w, c));
            return (w, c);
        }
        var (fw, frozenC) = Session(true);
        var (tw, thawedC) = Session(false);
        Sim.Step(fw, new Butcher(P, frozenC.Id), 30);
        Sim.Step(tw, new Butcher(P, thawedC.Id), 30);
        Assert.True(Carcasses.TotalRemainingKg(frozenC) > Carcasses.TotalRemainingKg(thawedC));
    }

    [Fact]
    public void Carcasses_RoundTripThroughASave()
    {
        var (w, c) = Make("Wolf");
        var loaded = Save.FromJson(Save.ToJson(w));
        var lc = Carcasses.Find(loaded, Map.Of(loaded).Camp, c.Id)!;
        Assert.Equal(c.Remaining, lc.Remaining);
        Assert.Equal(1, loaded.NextCarcassId);
    }
}
