namespace Survival.Tests;

/// The player-facing reader of the stat tiers (Tiers.Of): the same tier functions the NPC ranks by, with no
/// hysteresis, read off the person as they are. The UI's alerts show its Critical stats.
public class TiersTests
{
    [Fact]
    public void Of_ReadsEachStatsTier_OffThePersonAsTheyAre()
    {
        var w = World.New(17);
        var p = w.Agent(AgentId.Player);
        Items.Pack(w, AgentId.Player).Kg.Remove(Resource.Water);
        var b = p.Body;
        (b.CoreTempF, b.HydrationMl, b.EnergyMinutes) = (98.6, 0.95 * Body.MaxHydrationMl, Body.MaxEnergyMinutes);
        var fine = Tiers.Of(w, AgentId.Player);
        Assert.Equal(Tier.None, fine.Warmth);
        Assert.Equal(Tier.None, fine.Rest);

        (b.CoreTempF, b.HydrationMl, b.EnergyMinutes) = (Tiers.HypothermiaCoreF - 0.5, 0.4 * Body.MaxHydrationMl, 0.2 * Body.MaxEnergyMinutes);
        var bad = Tiers.Of(w, AgentId.Player);
        Assert.Equal(Tier.Critical, bad.Warmth);
        Assert.Equal(Tier.Need, bad.Water);
        Assert.Equal(Tier.Need, bad.Rest);
        Assert.Equal(Tier.Critical, bad[Stat.Warmth]);
    }

    [Theory]
    [InlineData(98.6, false, 30.0, Tier.None)]
    [InlineData(98.6, false, 20.0, Tier.Want)]   // tonight's 16 h in, not a spare half-day
    [InlineData(98.6, false, 4.0, Tier.Need)]    // tonight's fuel short is warmth, Need
    [InlineData(97.4, false, 30.0, Tier.Need)]
    [InlineData(97.6, true, 30.0, Tier.Need)]    // seeing to it: held to the satisfied line
    [InlineData(97.6, false, 30.0, Tier.None)]
    [InlineData(95.0, false, 30.0, Tier.Critical)]
    public void Warmth(double coreF, bool seeing, double fuelHours, Tier tier) =>
        Assert.Equal(tier, Tiers.WarmthTier(coreF, seeing, fuelHours, tonightHours: 16));

    [Theory]
    [InlineData(0.15, false, 3.0, 2.0, Tier.Critical)]
    [InlineData(0.40, false, 3.0, 2.0, Tier.Need)]
    [InlineData(0.80, true, 3.0, 2.0, Tier.Need)]
    [InlineData(0.80, false, 3.0, 2.0, Tier.None)]
    [InlineData(0.80, false, 1.0, 2.0, Tier.Want)]
    [InlineData(0.55, false, 0.0, 18.0, Tier.Need)] // nothing to hand and the evening's loss takes it to the line
    public void Water(double hydrationPct, bool seeing, double toHandL, double hoursLeft, Tier tier) =>
        Assert.Equal(tier, Tiers.WaterTier(hydrationPct, seeing, toHandL, hoursLeft));

    [Theory]
    [InlineData(0.10, false, Tier.Critical)]
    [InlineData(0.20, false, Tier.Need)]
    [InlineData(0.40, true, Tier.Need)]
    [InlineData(0.40, false, Tier.Want)]
    [InlineData(0.90, false, Tier.None)]
    public void Rest(double energyPct, bool seeing, Tier tier) => Assert.Equal(tier, Tiers.RestTier(energyPct, seeing));

    [Fact]
    public void Food_UnderTheFloorIsNeed_StarvingIsCritical()
    {
        var b = BodyState.BaselineHuman();
        double target = b.FatKg;
        Assert.Equal(Tier.Want, Tiers.FoodTier(b, target, false, foodToHand: true));
        b.CaloriesKcal = 0;
        Assert.Equal(Tier.Need, Tiers.FoodTier(b, target, false, foodToHand: false)); // stomach empty, nothing for today
        b.CaloriesKcal = 1500;
        b.FatKg = 0.09 * b.WeightKg; // under the 10% floor: weeks of reserve, Need
        Assert.Equal(Tier.Need, Tiers.FoodTier(b, b.FatKg, false, foodToHand: true));
        b.FatKg = 0.025 * b.WeightKg; // the body's own floor, 3% of what it weighs now
        Assert.Equal(Tier.Need, Tiers.FoodTier(b, b.FatKg, false, foodToHand: true));
        b.CaloriesKcal = 0;
        Assert.True(Tiers.Starving(b));
        Assert.Equal(Tier.Critical, Tiers.FoodTier(b, b.FatKg, false, foodToHand: true));
    }
}
