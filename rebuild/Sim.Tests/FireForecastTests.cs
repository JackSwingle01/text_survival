using CsCheck;
using Survival.Tests.Verification;
using static Survival.Tests.Verification.ConservationWorlds;

namespace Survival.Tests;

/// Fire.Forecast (#269): the one answer to what fire can be had, read by every caller. Each test builds its pit and
/// fuel by hand; the stepped ones a bare world with no animals.
public class FireForecastTests
{
    static readonly Resource[] Fuels = [.. Fire.Props.Keys.Order()];

    static Inventory Offer(params (Resource Resource, double Kg)[] kgs)
    {
        var inv = new Inventory();
        foreach (var (r, kg) in kgs) TestKit.Give(inv, r, kg);
        return inv;
    }

    static FireForecast Ask(Inventory fuel, double hoursWanted, FireState? pit = null, ToolType? starter = ToolType.FireStriker, PitType newPit = PitType.Open) =>
        Fire.Forecast(new FireAsk(pit, newPit, fuel, starter, hoursWanted));

    static Inventory With(Inventory fuel, Resource r, double kg)
    {
        var more = Items.CopyResources(fuel);
        Items.AddKg(more, r, kg);
        return more;
    }

    /// The two states the NPCs died in (#258): the plan counted the fuel and the strike was refused.
    [Fact]
    public void TheTwoStates_FromTheNpcDeaths()
    {
        var sticks = Ask(Offer((Resource.Tinder, 0.1), (Resource.Stick, 4.4)), hoursWanted: 1);
        Assert.True(sticks.Hours >= 1, $"4.4 kg of sticks forecast {sticks.Hours:0.00} h");
        Assert.Empty(sticks.Missing);

        var withoutOak = Ask(Offer((Resource.Tinder, 0.1), (Resource.Stick, 4.7)), hoursWanted: 8);
        var oak = Ask(Offer((Resource.Tinder, 0.1), (Resource.Stick, 4.7), (Resource.OakBranch, 16)), hoursWanted: 8);
        Assert.True(oak.Hours > withoutOak.Hours + 1 || oak.Missing.Any(m => m.Kind == FireNeedKind.Bridge),
            $"oak: {oak.Hours:0.00} h against {withoutOak.Hours:0.00} h without, missing {string.Join(", ", oak.Missing)}");
    }

    /// Tinder, a starter and 10 kg of one fuel: the hours count it, or a bridge to it is named that does.
    [Fact]
    public void EveryFuel_IsReachable()
    {
        foreach (var fuel in Fuels)
        {
            var tinder = Offer((Resource.Tinder, 0.1));
            var alone = Ask(tinder, hoursWanted: 12);
            var offer = With(tinder, fuel, 10);
            var f = Ask(offer, hoursWanted: 12);
            double ownHours = Fire.FuelHours(fuel, 10) * 0.5; // counted: at least half its reckoned hours more than tinder alone
            if (f.Hours >= alone.Hours + ownHours) continue;
            var bridge = f.Missing.SingleOrDefault(m => m.Kind == FireNeedKind.Bridge);
            Assert.True(bridge != null, $"{fuel}: {f.Hours:0.00} h, no bridge named ({string.Join(", ", f.Missing)})");
            double bridgeAlone = Ask(With(tinder, bridge.AnyOf[0], bridge.Kg), hoursWanted: 12).Hours;
            double bridged = Ask(With(offer, bridge.AnyOf[0], bridge.Kg), hoursWanted: 12).Hours;
            Assert.True(bridged >= bridgeAlone + ownHours, $"{fuel}: bridged by {bridge.Kg:0.00} kg of {bridge.AnyOf[0]}, {bridged:0.00} h against {bridgeAlone:0.00} h for the bridge alone");
        }
    }

    [Fact]
    public void NoStarter_AndNoFire_SaysOnlyThatAStarterIsMissing()
    {
        var f = Ask(Offer((Resource.Stick, 3)), hoursWanted: 4, starter: null);
        Assert.Equal((0.0, 0.0), (f.StrikePct, f.Hours));
        Assert.Equal([FireNeedKind.Starter], f.Missing.Select(m => m.Kind));
        Assert.Empty(f.Missing[0].AnyOf);
    }

    [Fact]
    public void NoTinder_NamesTinder_AndForecastsNoFire()
    {
        var f = Ask(Offer((Resource.Stick, 6)), hoursWanted: 1);
        Assert.Equal((0.0, 0.0), (f.StrikePct, f.Hours));
        Assert.Equal(FireNeedKind.Tinder, f.Missing[0].Kind);
        Assert.Equal(Fire.Tinders, f.Missing[0].AnyOf);
        Assert.Empty(Ask(With(Offer((Resource.Stick, 6)), f.Missing[0].AnyOf[0], f.Missing[0].Kg), hoursWanted: 1).Missing);
    }

    [Fact]
    public void ABurningPit_IsSureToBurn_AndItsNextIsWhatItTakes()
    {
        var pit = new FireState { Tile = default, LastUpdatedMinute = 0, BurningKg = { [Resource.BirchBranch] = 3 } };
        var f = Ask(Offer((Resource.OakBranch, 4)), hoursWanted: 2, pit, starter: null);
        Assert.Equal(1, f.StrikePct);
        Assert.Equal(Resource.OakBranch, f.Next?.Fuel);
        Assert.True(f.Hours > Fire.HoursItWillBurn(pit));
        Assert.Null(Ask(Offer((Resource.OakBranch, 4)), hoursWanted: 2, pit: null).Next); // cold: none until the strike
    }

    // ---- the properties ----

    /// A random pit (none, cold, glowing, burning), pit type, fuel on offer from every fuel, starter or none, hours wanted.
    sealed record Case(int PitKind, PitType PitType, Resource PitFuel, double PitKg, (Resource, double)[] Offer, bool Starter, double HoursWanted)
    {
        public FireState? Pit()
        {
            if (PitKind == 0) return null;
            var f = new FireState { Tile = default, LastUpdatedMinute = 0, Pit = PitType };
            if (PitKind == 1) { if (PitKg > 0.3) f.UnburnedKg[PitFuel] = PitKg; return f; }
            if (PitKind == 2)
            {
                Fire.AddFuel(f, Resource.Stick, PitKg);
                Fire.IgniteAll(f);
                while (Fire.BurningKg(f) > 0) Fire.Tick(f, default, ++f.LastUpdatedMinute, []);
                f.LastUpdatedMinute = 0;
                return f;
            }
            f.BurningKg[Resource.Stick] = 0.5;
            f.BurningKg[PitFuel] = PitKg;
            return f;
        }

        public FireAsk Ask() => new(Pit(), PitType, OfferInv(), Starter ? ToolType.FireStriker : null, HoursWanted);

        public Inventory OfferInv()
        {
            var inv = new Inventory();
            foreach (var (r, kg) in Offer) TestKit.Give(inv, r, kg);
            return inv;
        }

        public override string ToString() =>
            $"pit {PitKind} {PitType} {PitFuel} {PitKg:0.00} kg, offer [{string.Join(", ", Offer.Select(o => $"{o.Item1} {o.Item2:0.00}"))}], starter {Starter}, {HoursWanted:0.0} h";
    }

    static readonly Gen<Case> Cases =
        from kind in Gen.Int[0, 3]
        from type in Gen.Enum<PitType>()
        from pitFuel in Gen.Int[0, Fuels.Length - 1].Select(i => Fuels[i])
        from pitKg in Gen.Double[0.5, 4]
        from offer in Gen.Select(Gen.Int[0, Fuels.Length - 1].Select(i => Fuels[i]), Gen.Double[0.02, 10]).Array[0, 4]
        from starter in Gen.Int[0, 3]
        from hours in Gen.Double[0.5, 12]
        select new Case(kind, type, pitFuel, pitKg, offer, starter > 0, hours);

    /// Missing empty: struck until lit, then fed Next each minute, the fire burns at least 0.85 of the hours forecast.
    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void WhenNothingIsMissing_TheTendedFireBurnsTheHours()
    {
        Cases.Check(c =>
        {
            var ask = c.Ask();
            var forecast = Fire.Forecast(ask);
            if (forecast.Missing.Count > 0) return;
            double minutes = Stepped(ask, forecast);
            Assert.True(minutes >= 0.85 * forecast.Hours * 60, $"{c}: burned {minutes} min of {forecast.Hours * 60:0} forecast");
        }, iter: 60);
    }

    /// A cold pit with room left, but less than the lightest piece of the only tinder on offer: no strike, as StartFire
    /// refuses it ("no room for a whole ...").
    [Fact]
    public void ATinderWhosePieceDoesNotFit_IsNoStrike()
    {
        var pit = new FireState { Tile = default, LastUpdatedMinute = 0, Pit = PitType.Open };
        pit.UnburnedKg[Resource.Stick] = Fire.MaxFuelKg(PitType.Open) - 0.02;
        var offer = new Inventory();
        Items.AddPiece(offer, Resource.Chaga, 0.1);
        var f = Ask(offer, hoursWanted: 1, pit: pit);
        Assert.Equal((0.0, 0.0), (f.StrikePct, f.Hours));
        Assert.Null(f.Strike);
    }

    /// Adding the first fuel of each entry at the weight given leaves nothing missing; while something is, the hours fall
    /// short. A missing starter names no fuel, so once it is added the forecast may name fuel, but never a starter again.
    [Fact]
    public void WhatIsMissing_IsEnough_AndOnlyWhenTheHoursFallShort()
    {
        Cases.Check(c =>
        {
            var ask = c.Ask();
            var forecast = Fire.Forecast(ask);
            if (forecast.Missing.Count == 0) return;
            Assert.True(forecast.Hours < ask.HoursWanted, $"{c}: {forecast.Hours:0.00} h with {string.Join(", ", forecast.Missing)}");
            var more = ask with { Fuel = Items.CopyResources(ask.Fuel) };
            foreach (var need in forecast.Missing)
                if (need.Kind == FireNeedKind.Starter) more = more with { Starter = ToolType.FireStriker };
                else Items.AddKg(more.Fuel, need.AnyOf[0], need.Kg);
            var after = Fire.Forecast(more);
            if (forecast.Missing[0].Kind == FireNeedKind.Starter) Assert.DoesNotContain(after.Missing, m => m.Kind == FireNeedKind.Starter);
            else Assert.True(after.Missing.Count == 0, $"{c}: added {string.Join(", ", forecast.Missing)}, still missing {string.Join(", ", after.Missing)} at {after.Hours:0.00} h");
        }, iter: 80);
    }

    /// More fuel on offer never shortens the fire, and never makes it lack more.
    [Fact]
    public void AddingFuel_NeverLowersTheHours_NorAddsAnEntry()
    {
        Gen.Select(Cases, Gen.Int[0, Fuels.Length - 1].Select(i => Fuels[i]), Gen.Double[0.02, 10]).Check(t =>
        {
            var (c, fuel, kg) = t;
            var ask = c.Ask();
            var before = Fire.Forecast(ask);
            var more = ask with { Fuel = Items.CopyResources(ask.Fuel) };
            TestKit.Give(more.Fuel, fuel, kg);
            var after = Fire.Forecast(more);
            Assert.True(after.Hours >= before.Hours - 1e-9, $"{c} + {fuel} {kg:0.00}: {after.Hours:0.00} h, was {before.Hours:0.00}");
            Assert.True(after.Missing.Count <= before.Missing.Count, $"{c} + {fuel} {kg:0.00}: missing {string.Join(", ", after.Missing)}, was {string.Join(", ", before.Missing)}");
        }, iter: 80);
    }

    /// The forecast's own fire stepped in a bare world: struck as it assumes until it catches (the tinder of a miss
    /// given back, since the hours are those of a fire that caught), then fed NextFuel each minute. Minutes alight.
    internal static int Stepped(FireAsk ask, FireForecast forecast)
    {
        var w = Bare(-5);
        var pack = Items.Pack(w, P);
        TestKit.ClearResources(pack);
        pack.Gear.Clear();
        Items.AddAll(pack, Items.CopyResources(ask.Fuel));
        if (ask.Starter is ToolType tool) pack.Gear.Add(Items.Make(tool switch { ToolType.FireStriker => "fire_striker", ToolType.BowDrill => "bow_drill", _ => "hand_drill" }));
        var camp = Camp(w);
        if (ask.Pit is { } p)
        {
            p.Tile = camp;
            p.LastUpdatedMinute = w.Minute;
            w.Fires[new FireId(w.NextFireId++)] = p;
        }
        int minutes = 0;
        if (forecast.Strike is { } s)
        {
            var strike = new StartFire(P, ask.Starter!.Value, s.Tinder, s.TinderKg, s.KindlingKg, ask.NewPit);
            for (int tries = 0; ; tries++)
            {
                Assert.Null(Sim.Refusal(w, strike));
                if (Sim.Step(w, strike, 1).Reason != Fire.DidNotCatch) break;
                Assert.True(tries < 50, "the strike never caught");
                Items.AddKg(pack, s.Tinder, s.TinderKg);
            }
            minutes = 1;
        }
        var pit = w.Fires[Fire.PitAt(w, camp)!.Value];
        while (true)
        {
            if (Fire.NextFuel(pit, pack) is { } next)
            {
                var add = new AddFuel(P, next.Fuel, next.Kg);
                Assert.Null(Fire.Refusal(w, add)); // the fire's own: hours on, the person may be asleep
                Fire.Act(w, add, []);
            }
            if (!Fire.IsActive(pit)) return minutes;
            minutes++;
            Sim.Advance(w, 1);
        }
    }
}
