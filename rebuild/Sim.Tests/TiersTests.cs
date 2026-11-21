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
}
