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
        ["Hare"] = (2.062641801423291, 1.0313209007116455, 0.2062641801423291, false, false),
        ["Caribou"] = (124.4465826469443, 74.66794958816658, 18.666987397041645, false, false),
        ["Wolf"] = (41.52815800699646, 24.91689480419788, 8.305631601399293, false, false),
        ["Bear"] = (225.99621700503752, 124.29791935277063, 67.79886510151125, false, false),
        ["Mammoth"] = (6344.009779679869, 3172.0048898399345, 2220.403422887954, false, true),
        ["Ptarmigan"] = (0.5513861826959172, 0.3308317096175503, 0.08270792740438758, true, false),
    };

    static (World, Carcass) Make(string animal, double effectiveHours = 0)
    {
        var w = TestWorlds.New(17);
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
        { "Hare", [0.6703585854625697, 0.2062641801423291, 0.2062641801423291, 0.10313209007116456, 0.12375850808539746, 0], [17, 34, 51] },
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
        // The old ledger was a careful butcher's yield; this one is full processing's (Carcasses.ThoroughFactor).
        for (int i = 0; i < order.Length; i++) Assert.Equal(kg[i] * Carcasses.ThoroughFactor(order[i]), c.Remaining.GetValueOrDefault(order[i]), Digits);
        Assert.Equal(minutes, new[] { ButcherMode.QuickStrip, ButcherMode.Careful, ButcherMode.FullProcessing }.Select(m => Carcasses.RemainingMinutes(c, m)));
    }

    /// Old CarcassFeature.Harvest had no setup in it: the cut alone, all of it in one go, read off the
    /// carcass's yield ledger (what Sim.Step carries off a minute at a time).
    static Inventory CutAll(Carcass c, double minutes, bool knife, bool handsImpaired, ButcherMode mode)
    {
        Carcasses.Cut(c, minutes, knife, handsImpaired, mode);
        return Carcasses.TakeReady(c);
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
        { "Hare", 30, false, 0.1, 0.1381970006953605, 0 },       // the 0.1 kg meat floor
        { "Ptarmigan", 0, true, 0.21504061125140772, 0, 0 },       // bone under 0.05 kg is not worth keeping
        { "Mammoth", 18, true, 38.4, 8, 0 },
    };

    [Theory, MemberData(nameof(Butchering))]
    public void Butchering_ByDecayAndBlade_MatchesOld(string animal, double hours, bool knife, double meat, double bone, double hide)
    {
        var (_, c) = Make(animal, hours);
        var got = CutAll(c, Carcasses.RemainingMinutes(c, ButcherMode.Careful), knife, false, ButcherMode.Careful);
        // Old meat came off in random portions and dropped a last scrap of up to 0.05 kg; stacks by weight keep it.
        Assert.InRange(Items.Have(got, Resource.RawMeat), meat - 1e-9, meat + 0.05);
        Assert.Equal(bone, Items.Have(got, Resource.Bone), Digits);
        Assert.Equal(hide, Items.Have(got, Resource.Hide), Digits);
    }

    [Fact]
    public void ModesAndPartialSessions_MatchOld()
    {
        var (_, quick) = Make("Caribou");
        var q = CutAll(quick, Carcasses.RemainingMinutes(quick, ButcherMode.QuickStrip), true, true, ButcherMode.QuickStrip);
        Assert.Equal(31.0618670286773, Items.Have(q, Resource.RawMeat), Digits);
        Assert.Equal(3.111164566173607, Items.Have(q, Resource.Bone), Digits);
        Assert.Equal(0, Items.Have(q, Resource.Hide));
        Assert.Equal(4.480076975289995, Items.Have(q, Resource.RawFat), Digits);

        var (_, full) = Make("Caribou");
        var f = CutAll(full, 30, true, false, ButcherMode.FullProcessing);
        Assert.Equal(4.505415162454874, Items.Have(f, Resource.RawMeat), Digits);
        Assert.Equal(0.6301279947489334, Items.Have(f, Resource.Sinew), Digits);
        // Old 78.90653121028166 kg of the careful ledger; this ledger is full processing's (ThoroughFactor), cut at the old rate.
        Assert.Equal(85.515309275547125, Carcasses.TotalRemainingKg(full), Digits);

        var (_, mammoth) = Make("Mammoth");
        CutAll(mammoth, Carcasses.RemainingMinutes(mammoth, ButcherMode.Careful), false, false, ButcherMode.Careful);
        Assert.Equal(19, Carcasses.TotalRemainingKg(mammoth), Digits); // ivory and the great hide wait for a blade
    }

    /// Cutting never yields more than it takes off the carcass, by resource: in any mode, fresh or
    /// spoiling, steady or clumsy, with a blade or without, at the most practice there is. Old game: full
    /// processing gave 107% of the careful ledger it cut (bug, fixed); the review found the skill factor
    /// lifting every yield past the cut as well (120% careful, 128.8% full at level 20).
    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void Cutting_ConservesMass_AtMaxSkill_InEveryMode()
    {
        double maxSkill = Skills.ButcherYieldFactor(Skills.MaxLevel);
        foreach (var animal in Animals.Keys)
            foreach (var mode in Enum.GetValues<ButcherMode>())
                foreach (double hours in new[] { 0.0, 8, 30 })
                    foreach (bool knife in new[] { true, false })
                        foreach (bool clumsy in new[] { false, true })
                            foreach (double skill in new[] { 1.0, maxSkill })
                            {
                                var (_, c) = Make(animal, hours);
                                for (int i = 0; i < 200 && !Carcasses.AllCut(c); i++) Carcasses.Cut(c, 7, knife, clumsy, mode, skill);
                                foreach (var (r, kg) in c.YieldKg)
                                    Assert.True(kg <= c.CutKg[r] + 1e-9, $"{animal} {mode} {hours}h knife={knife} clumsy={clumsy} skill {skill}: {r} yielded {kg} kg of {c.CutKg[r]} kg cut");
                            }
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
        Assert.Equal(2000 / 60.0 * Carcasses.DecayFactor(Exposure.Surface(a, Map.Of(a).Camp, 0).AirTemperatureF), ca.EffectiveHours, 9);
    }

    /// old game: decay read the air a person feels, wind chill included, so a gale over a thaw kept
    /// meat as if it were freezing (bug, fixed): meat rots at the air that reaches it.
    [Fact]
    public void Decay_ReadsTheAirNotTheWindChill()
    {
        var w = TestWorlds.New(17);
        w.FixedWeather = new(0, WeatherCondition.Cloudy, 3, 0.6, 0, 0.9, WindDirection.North); // 37.4 °F, a 27 mph wind
        var tile = Map.Of(w).Camp;
        Assert.True(Exposure.OpenAirF(w, tile, Weather.At(w, 0)) < 32);
        var c = Carcasses.Create(w, tile, "Caribou", BodyState.Create(BodyKind.Quadruped, 124, 0.15, 0.6), 0);
        w.Minute = 10 * 60;
        Carcasses.CatchUp(w, tile, c);
        Assert.Equal(10.0 * Carcasses.DecayFactor(37.4), c.EffectiveHours, 9);
        Assert.Equal(1.0, Carcasses.DecayFactor(37.4));
        Assert.False(Carcasses.IsFrozen(w, c));
    }

    /// The same with the seed's own weather, which changes within the hour: each hour is read at
    /// the top of the hour, not at whatever minute someone last looked.
    [Theory, InlineData(17), InlineData(3), InlineData(9), InlineData(21)]
    public void Decay_InRealWeather_DoesNotDependOnHowOftenYouLook(int seed)
    {
        var a = TestWorlds.New(seed);
        var b = TestWorlds.New(seed);
        var tile = Map.Of(a).Camp;
        var body = BodyState.Create(BodyKind.Quadruped, 120, 0.15, 0.6);
        var once = Carcasses.Create(a, tile, "Caribou", body, 0);
        var often = Carcasses.Create(b, tile, "Caribou", body, 0);
        const long End = 14 * 1440;
        for (long m = 0; m < End; m += 37) { b.Minute = m; Carcasses.CatchUp(b, tile, often); }
        a.Minute = b.Minute = End;
        Carcasses.CatchUp(a, tile, once);
        Carcasses.CatchUp(b, tile, often);
        Assert.Equal(once.EffectiveHours, often.EffectiveHours, 9);
        Assert.Equal(once.LastKnownTempF, often.LastKnownTempF);
    }

    // ---- through Sim.Step ----

    [Fact]
    public void Butcher_RunsItsTime_FillsThePack_BloodiesYou_AndLeavesNoCarcass()
    {
        var (w, c) = Make("Hare");
        Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
        var r = Sim.Step(w, new Butcher(P, c.Id), 120);
        Assert.Equal(("butchered", 34), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Contains(r.Events, e => e is Butchered { Finished: true });
        // All of it, less the decay at the camp's air when each minute's cut came off: the setup
        // takes the first 30 minutes, the cutting the last 4 (decay costs meat only).
        double Meat(int minute) => 0.6703585854625697 * (1 - 0.8 * Carcasses.DecayLevel(minute / 60.0 * Carcasses.DecayFactor(Exposure.OpenAirF(w, Map.Of(w).Camp, Mild))));
        Assert.InRange(Items.Have(Items.Pack(w, P), Resource.RawMeat), Meat(34), Meat(30));
        Assert.Null(w.Tiles.GetValueOrDefault(Map.Of(w).Camp)?.Carcasses); // the emptied list goes, so the tile can clean up
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
        double total = c.Remaining.Sum(x => x.Value / Carcasses.ThoroughFactor(x.Key)); // what careful work recovers of the ledger
        var r = Sim.Step(w, new Butcher(P, c.Id), 24 * 60);
        Assert.Equal(Work.PackFull, r.InterruptedReason); // all cut, not all carried: the carcass is not done
        Assert.Contains(r.Events, e => e is Butchered { Finished: false });
        Assert.Equal(Items.CapacityKg(Items.Pack(w, P)), Items.CarriedKg(w.Agent(P)), 6);
        Assert.Equal(0, Carcasses.TotalRemainingKg(c));
        Assert.InRange(c.Pending.Values.Sum(), total - 20, total - 9); // ~10 kg taken, the rest of the meat's loss is decay
        Assert.Contains(c, w.Tiles[Map.Of(w).Camp].Carcasses!);
        Assert.Equal(Work.PackFull, Sim.Step(w, new Butcher(P, c.Id), 60).InterruptedReason); // refused: no room, nothing to cut
        for (int i = 0; i < 40 && !Carcasses.IsButchered(c); i++)
        {
            Sim.Step(w, new PutDown(P, Resource.RawMeat, 99), 1);
            foreach (var res in new[] { Resource.Bone, Resource.Hide, Resource.Sinew, Resource.RawFat }) Sim.Step(w, new PutDown(P, res, 99), 1);
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
            // Decay reads the air every hour, so a frozen carcass needs frozen air to stay frozen.
            if (frozen) w.FixedWeather = Mild with { TemperatureC = -20 };
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
        Sim.Step(fw, new Butcher(P, frozenC.Id), 90); // past the setup, paid once per carcass
        Sim.Step(tw, new Butcher(P, thawedC.Id), 90);
        Assert.True(Carcasses.TotalRemainingKg(frozenC) > Carcasses.TotalRemainingKg(thawedC));
    }

    /// A pack with room for everything, for sessions that must not be cut short by it.
    static void BigPack(World w)
    {
        var pack = Items.Pack(w, P);
        pack.Gear.Add(Items.Make("flint_knife"));
        for (int i = 0; i < 10; i++) pack.Gear.Add(Items.Make("large_bag"));
    }

    static void Refresh(World w)
    {
        var body = w.Agent(P).Body;
        body.EnergyMinutes = Body.MaxEnergyMinutes;
        body.CaloriesKcal = 1500;
        body.HydrationMl = 3000;
    }

    /// The world's results do not depend on how a caller chunks the work: one long butchering
    /// session and the same minutes in hours take the same time, yield the same, bloody you the
    /// same and leave the same scent. Old ButcherStrategy charged the setup, the scent and the
    /// blood once per session, and read decay once per session.
    [Fact]
    public void Butchering_InOneSessionOrInHours_ComesOutTheSame()
    {
        var (one, c1) = Make("Caribou");
        var (hours, c2) = Make("Caribou");
        BigPack(one);
        BigPack(hours);
        Assert.Equal(267, Carcasses.RemainingMinutes(c1, ButcherMode.Careful));

        Assert.Null(Sim.Step(one, new Butcher(P, c1.Id), 180).InterruptedReason);
        for (int i = 0; i < 3; i++) Assert.Null(Sim.Step(hours, new Butcher(P, c2.Id), 60).InterruptedReason);
        Assert.Equal(Carcasses.TotalRemainingKg(c1), Carcasses.TotalRemainingKg(c2), 9);
        Assert.Equal(c1.ScentBonus, c2.ScentBonus, 12);
        Assert.Equal(Effects.Severity(one.Agent(P).Body, EffectKind.Bloody), Effects.Severity(hours.Agent(P).Body, EffectKind.Bloody), 12);
        // Three hours elbow-deep. Blood on you now dries as you go (body review A5, 2026-11-21), so careful
        // butchering's 0.15 an hour settles near 0.15 rather than piling up: was > 0.3.
        Assert.True(Effects.Severity(one.Agent(P).Body, EffectKind.Bloody) > 0.1);

        var a = Sim.Step(one, new Butcher(P, c1.Id), 600);
        int minutes = 180 + a.MinutesElapsed, chunked = 180;
        while (Carcasses.Find(hours, Map.Of(hours).Camp, c2.Id) != null) chunked += Sim.Step(hours, new Butcher(P, c2.Id), 60).MinutesElapsed;
        Assert.Equal(("butchered", 267, 267), (a.InterruptedReason, minutes, chunked));
        var p1 = Items.Pack(one, P);
        var p2 = Items.Pack(hours, P);
        foreach (var r in new[] { Resource.RawMeat, Resource.Bone, Resource.Hide, Resource.Sinew, Resource.RawFat })
            Assert.Equal(Items.Have(p1, r), Items.Have(p2, r), 9);
    }

    /// One step, an hour at a time, or a minute at a time: the same pack, the same carcass, the same
    /// world, exactly. Old (and before this fix): the scrap minimum and the meat floor were applied to
    /// each step's cut, so a caribou butchered a minute at a time gave meat and nothing else, and a
    /// hare nothing at all but floored meat.
    [Theory, InlineData("Caribou"), InlineData("Hare")]
    [Trait("Category", TestCategories.LongRun)]
    public void Butchering_InOneStepHoursOrMinutes_YieldsExactlyTheSame(string animal)
    {
        (World, Carcass) Start() { var (w, c) = Make(animal); BigPack(w); return (w, c); }
        string Run(int chunk, int upTo)
        {
            var (w, c) = Start();
            for (int done = 0; done < upTo && Carcasses.Find(w, Map.Of(w).Camp, c.Id) != null;)
                done += Sim.Step(w, new Butcher(P, c.Id), Math.Min(chunk, upTo - done)).MinutesElapsed;
            return Save.ToJson(w);
        }
        foreach (int upTo in new[] { 20, 33, 100, 600 }) // in the setup, just past it, part-cut, all of it
        {
            string one = Run(upTo, upTo);
            Assert.Equal(one, Run(60, upTo));
            Assert.Equal(one, Run(1, upTo));
        }
        var (whole, wc) = Start();
        Assert.Equal("butchered", Sim.Step(whole, new Butcher(P, wc.Id), 600).InterruptedReason);
        var pack = Items.Pack(whole, P);
        foreach (var r in new[] { Resource.Bone, Resource.Hide, Resource.Sinew, Resource.RawFat })
            Assert.True(Items.Have(pack, r) > 0, $"no {r}");
    }

    /// Nothing is minted or lost but what the rules say: every kg off the carcass is still on it or
    /// in the cut ledger, and every kg the cut yielded is in the pack or waiting on the carcass -
    /// through a save in the middle, and through a pack too small for it.
    [Theory, InlineData("Caribou", true), InlineData("Caribou", false), InlineData("Hare", true)]
    public void Butchering_ConservesWhatTheCarcassHad_ThroughASave(string animal, bool bigPack)
    {
        var (w, c) = Make(animal);
        if (bigPack) BigPack(w); else Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
        var start = new Dictionary<Resource, double>(c.Remaining);
        double packBefore = Items.CarriedKg(w.Agent(P));
        for (int i = 0; i < 40; i++)
        {
            w = TestWorlds.Load(Save.ToJson(w));
            var cc = Carcasses.Find(w, Map.Of(w).Camp, c.Id);
            if (cc == null) break;
            c = cc;
            Sim.Step(w, new Butcher(P, c.Id), 15);
            foreach (var (r, kg) in start) Assert.Equal(kg, c.Remaining[r] + c.CutKg.GetValueOrDefault(r), 9);
            double carried = Items.CarriedKg(w.Agent(P)) - packBefore;
            Assert.Equal(c.YieldKg.Values.Sum(), carried + c.Pending.Values.Sum(), 9);
            if (!bigPack && Sim.Step(w, new Butcher(P, c.Id), 15).InterruptedReason == "your pack is full") break;
        }
        if (bigPack) Assert.Null(Carcasses.Find(w, Map.Of(w).Camp, c.Id));
        else Assert.True(c.Pending.Values.Sum() > 1); // all cut; what would not fit waits on the carcass
    }

    /// The carcass is caught up before its frozen state slows the minute (old order read the air it
    /// felt an hour ago for the first minute of the step).
    [Fact]
    public void AFreshlyFrozenCarcass_SlowsTheFirstMinuteToo()
    {
        var (w, c) = Make("Caribou");
        w.FixedWeather = Mild with { TemperatureC = -20 };
        BigPack(w);
        w.Minute = 3 * 60;
        c.LastKnownTempF = 41; // the last air it felt was warm; the hours since have been freezing
        Sim.Step(w, new Butcher(P, c.Id), 1);
        Assert.Equal(1 / 1.5, c.SetupMinutes, 9);
    }

    /// What a full pack could not hold goes back on the carcass as the share of it it was cut from,
    /// so two sessions with a small pack yield what one does with a big one.
    [Fact]
    public void AFullPack_OverTwoSessions_YieldsWhatOneBigPackDoes()
    {
        var (big, c1) = Make("Caribou");
        BigPack(big);
        Assert.Equal("butchered", Sim.Step(big, new Butcher(P, c1.Id, ButcherMode.QuickStrip), 24 * 60).InterruptedReason);
        var bigPack = Items.Pack(big, P);

        var (small, c2) = Make("Caribou");
        Items.Pack(small, P).Gear.Add(Items.Make("flint_knife"));
        var pile = new Inventory();
        for (int session = 0; session < 20 && Carcasses.Find(small, Map.Of(small).Camp, c2.Id) != null; session++)
        {
            Sim.Step(small, new Butcher(P, c2.Id, ButcherMode.QuickStrip), 24 * 60);
            var pack = Items.Pack(small, P);
            foreach (var r in new[] { Resource.RawMeat, Resource.Bone, Resource.RawFat }) Items.Add(pile, r, Items.Take(pack, r, 999));
            Refresh(small);
        }
        Assert.Null(Carcasses.Find(small, Map.Of(small).Camp, c2.Id));
        // Bone and fat are cut at half weight in a quick strip, and do not rot: what was put back
        // must not be halved again. (Meat rots in the hours between, so it is not compared.)
        Assert.Equal(Items.Have(bigPack, Resource.Bone), Items.Have(pile, Resource.Bone), 9);
        Assert.Equal(Items.Have(bigPack, Resource.RawFat), Items.Have(pile, Resource.RawFat), 9);
    }

    /// A carcass keeps the mode it was first butchered in; asking for another is refused, not swapped.
    [Fact]
    public void Butcher_InAnotherModeThanTheCarcasss_IsRefused()
    {
        var (w, c) = Make("Caribou");
        BigPack(w);
        Sim.Step(w, new Butcher(P, c.Id, ButcherMode.Careful), 5);
        string before = Save.ToJson(w);
        var r = Sim.Step(w, new Butcher(P, c.Id, ButcherMode.QuickStrip), 5);
        Assert.Equal((0, "this carcass is being butchered careful, not quick strip"), (r.MinutesElapsed, r.InterruptedReason));
        Assert.Equal(before, Save.ToJson(w));
        Assert.Null(Sim.Step(w, new Butcher(P, c.Id), 5).InterruptedReason);
        Assert.Null(Sim.Step(w, new Butcher(P, c.Id, ButcherMode.Careful), 5).InterruptedReason);
    }

    [Fact]
    public void Carcasses_RoundTripThroughASave()
    {
        var (w, c) = Make("Wolf");
        var loaded = TestWorlds.Load(Save.ToJson(w));
        var lc = Carcasses.Find(loaded, Map.Of(loaded).Camp, c.Id)!;
        Assert.Equal(c.Remaining, lc.Remaining);
        Assert.Equal(1, loaded.NextCarcassId);
    }

    /// Review: after the ledger was scaled up to full processing's (ThoroughFactor), scavengers ate off the
    /// scaled ledger: a carcass fed wolves 10% longer, and each kilo a wolf ate cost a careful butcher 1/1.1
    /// of a kilo. Animals eat in careful kilograms, so both are as they were before the rescale.
    [Fact]
    public void Scavengers_EatWhatACarefulButcherWouldCut()
    {
        var w = TestWorlds.New(17);
        var camp = Map.Of(w).Camp;
        var body = BodyState.Create(BodyKind.Quadruped, 120, 0.15, 0.6);
        var c = Carcasses.Create(w, camp, "Caribou", body, 0);
        double before = body.MuscleKg * 0.65; // the old ledger's meat: a careful butcher's
        Assert.Equal(before, Fauna.MeatHere(w, camp), 9);
        Assert.Equal(before * Carcasses.ThoroughFactor(Resource.RawMeat), c.Remaining[Resource.RawMeat], 9);
        Assert.Equal(5, Carcasses.EatMeat(c, 5), 12);
        Assert.Equal(before - 5, Fauna.MeatHere(w, camp), 9);
        Assert.Equal((before - 5) * Carcasses.ThoroughFactor(Resource.RawMeat), c.Remaining[Resource.RawMeat], 9); // a careful cut has 5 kg less
        // Bites of 0.3 kg, as a pack feeds: as many as the old ledger held, and then nothing.
        int bites = 0;
        double eaten = 5;
        while (Fauna.MeatHere(w, camp) > 0) { eaten += Carcasses.EatMeat(c, 0.3); bites++; }
        Assert.Equal((int)Math.Ceiling((before - 5) / 0.3), bites);
        Assert.Equal(before, eaten, 9);
        Assert.Equal(0, c.Remaining[Resource.RawMeat]);
        Assert.Equal(0, Carcasses.EatMeat(c, 1));
    }

    /// Bare hands finish what bare hands can cut: the tusks and the great hide are not torn off by
    /// hand, so they wait on the carcass for a blade rather than keep the work from ever ending. Old
    /// game: bare hands on a mammoth never finished (bug, fixed).
    [Fact]
    public void BareHands_FinishWhatTheyCanCut_AndTheTrophiesWaitForABlade()
    {
        var (_, mammoth) = Make("Mammoth");
        int bare = Carcasses.RemainingMinutes(mammoth, ButcherMode.Careful, hasCuttingTool: false);
        Assert.True(bare < Carcasses.RemainingMinutes(mammoth, ButcherMode.Careful)); // no time charged on what hands cannot cut
        Carcasses.Cut(mammoth, Carcasses.Prepare(mammoth, bare, ButcherMode.Careful), false, false, ButcherMode.Careful);
        Assert.True(Carcasses.AllCut(mammoth, hasCuttingTool: false));
        Assert.False(Carcasses.AllCut(mammoth));
        Assert.Equal(4, mammoth.Remaining[Resource.Ivory], Digits);
        Assert.Equal(15, mammoth.Remaining[Resource.MammothHide], Digits);
        Carcasses.Cut(mammoth, Carcasses.RemainingMinutes(mammoth, ButcherMode.Careful), true, false, ButcherMode.Careful);
        Assert.True(Carcasses.AllCut(mammoth));
        Assert.Equal(4, mammoth.YieldKg[Resource.Ivory], Digits);
    }

    /// The same through the work: a bare-handed session ends, the carcass stays for someone with a knife,
    /// and the knife finishes it.
    [Fact]
    public void BareHandedButchering_Ends_AndLeavesTheTuskForAKnife()
    {
        var w = TestWorlds.New(17);
        w.FixedWeather = Mild;
        var (kg, muscle, fat, _, _) = Animals["Wolf"];
        var body = BodyState.Create(BodyKind.Quadruped, kg, fat / kg, muscle / kg);
        var c = Carcasses.Create(w, Map.Of(w).Camp, "Wolf", body, 0, special: new Dictionary<Resource, double> { [Resource.Ivory] = 0.5 });
        w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = Map.Of(w).Camp, LastUpdatedMinute = 0, Pit = PitType.Stone, BurningKg = { [Resource.Oak] = 10 } };
        var r = Sim.Step(w, new Butcher(P, c.Id), 24 * 60);
        Assert.Equal(Work.NeedsABlade, r.InterruptedReason);
        Assert.Contains(c, w.Tiles[Map.Of(w).Camp].Carcasses!);
        Assert.Equal(0.5, c.Remaining[Resource.Ivory], Digits);
        Assert.Equal(Work.NeedsABlade, Sim.Step(w, new Butcher(P, c.Id), 60).InterruptedReason); // refused at once: nothing for hands
        Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
        Assert.Equal("butchered", Sim.Step(w, new Butcher(P, c.Id), 60).InterruptedReason);
        Assert.True(Items.Have(Items.Pack(w, P), Resource.Ivory) > 0.4);
    }
}
