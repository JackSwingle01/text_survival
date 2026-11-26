using CsCheck;
using Survival;
using Survival.Tests.Verification;

namespace Survival.Tests;

/// The flint knife wears slowly, the obsidian knife hits harder, and an NPC sharpens its blade (#285).
public class BladeWearTests
{
    static readonly AgentId P = AgentId.Player;

    static World Noon(out Inventory pack)
    {
        var w = ConservationWorlds.Bare(-2);
        w.Minute = 3 * 60;
        pack = Items.Pack(w, P);
        TestKit.ClearResources(pack);
        pack.Gear.Clear();
        return w;
    }

    static Gear Knife(Inventory pack, int durability)
    {
        var knife = Items.Make("flint_knife");
        knife.Durability = durability;
        pack.Gear.Add(knife);
        return knife;
    }

    // ---- the flint knife ----

    [Fact]
    public void AFlintKnife_MadeNew_HasThirtyUses()
    {
        var knife = Items.Make("flint_knife");
        Assert.Equal((30, 30), (knife.Durability, knife.MaxDurability));
    }

    [Fact]
    public void AFlintKnife_LosesAUse_AsTheCuttingToolOfARecipe()
    {
        var w = Noon(out var pack);
        var knife = Knife(pack, 30);
        TestKit.GivePieces(pack, Resource.CuredHide, 1);
        Assert.Null(Sim.Step(w, new Make(P, "hide_strips"), 1).InterruptedReason);
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), Recipes.Get("hide_strips"), null, []));
        Assert.Equal(29, knife.Durability);
    }

    [Fact]
    public void AFlintKnife_AtZero_StopsWorking()
    {
        var w = Noon(out var pack);
        var knife = Knife(pack, 1);
        Assert.False(Items.Use(knife));
        Assert.False(Items.Works(knife));
        TestKit.GivePieces(pack, Resource.CuredHide, 1);
        Assert.Equal("you need something for cutting", Sim.Refusal(w, new Make(P, "hide_strips"))?.Reason);
    }

    [Fact]
    public void AFlintKnife_LosesAUse_OnABlock()
    {
        var (w, t) = CombatTests.Field();
        var knife = Items.Make("flint_knife");
        Items.Pack(w, P).Gear.Add(knife);
        for (int i = 0; i < 400; i++)
        {
            w.Encounters.Clear();
            w.Agent(P).Members[0] = BodyState.BaselineHuman();
            var herd = CombatTests.Herd(w, Species.Bear, 1, t);
            var e = Combat.Create(w, [new Fighter(P) { Weapon = knife }], [new Fighter(herd)], t, 1, Awareness.Engaged, Awareness.Engaged);
            var bear = e.Units.First(u => u.Team == 1);
            int before = knife.Durability;
            var r = Combat.Apply(w, e, bear, CombatAction.Attack, []);
            w.Agents.Remove(herd);
            if (!r.Blocked) continue;
            Assert.Equal(before - 1, knife.Durability);
            return;
        }
        Assert.Fail("no blow was blocked in 400");
    }

    [Fact]
    public void AFlintKnife_IsOfferedForSharpening_OnceItHasLostAUse()
    {
        var w = Noon(out var pack);
        pack.Gear.Add(Items.Make("stone"));
        var knife = Knife(pack, 30);
        static bool Offered(World w) => Verbs.At(w, P).Make.Any(v => v.Activity is Make { Recipe: "sharpen", Target: "flint_knife" });
        Assert.False(Offered(w));
        knife.Durability = 29;
        Assert.True(Offered(w));
    }

    [Fact]
    public void SharpeningAFlintKnife_AtTenOfThirty_BringsItToTwenty()
    {
        var w = Noon(out var pack);
        pack.Gear.Add(Items.Make("stone"));
        var knife = Knife(pack, 10);
        Assert.Null(Sim.Step(w, new Make(P, "sharpen", "flint_knife"), 1).InterruptedReason);
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), Recipes.Get("sharpen"), null, [], "flint_knife"));
        Assert.Equal(20, knife.Durability);
    }

    // ---- the obsidian knife ----

    [Fact]
    public void TheObsidianKnife_Hits1_15TimesAsHardAsFlint_AndKeepsTenUses()
    {
        var flint = Items.GearKinds["flint_knife"];
        var obsidian = Items.GearKinds["obsidian_knife"];
        Assert.Equal(1.15, obsidian.Weapon!.Damage / flint.Weapon!.Damage, 2);
        Assert.Equal(10, obsidian.MaxDurability);
    }

    // ---- an NPC sharpens its blade ----

    static (World World, Agent Npc, Gear Knife) Npc(int knifeUses, bool stone)
    {
        var w = ConservationWorlds.Bare(5);
        w.Agents.Remove(P);
        var npc = w.Agent(Survival.Npc.Spawn(w, Map.Of(w).Camp));
        var pack = ((NpcMind)npc.Mind).Pack;
        TestKit.ClearResources(pack);
        TestKit.Give(pack, Resource.Water, 2);
        var knife = pack.Gear.Single(g => g.Kind == "flint_knife");
        knife.Durability = knifeUses;
        if (stone) pack.Gear.Add(Items.Make("stone"));
        // Fuel for tonight on the camp's pile, so Warmth is not at Need.
        var pile = Items.PileAt(w, Map.Of(w).Camp);
        Items.AddKg(pile, Resource.Tinder, 1);
        Items.AddKg(pile, Resource.Stick, 20);
        Items.AddKg(pile, Resource.OakBranch, 40);
        return (w, npc, knife);
    }

    static Activity Decides(World w, Agent npc) => Survival.Npc.Decide(w, npc, []).Activity;

    static bool Sharpens(Activity a) => a is Make { Recipe: "sharpen", Target: "flint_knife" };

    [Fact]
    public void AnNpc_WithAKnifeAtHalf_AStone_AndNoNeed_SharpensIt()
    {
        var (w, npc, knife) = Npc(15, stone: true);
        var a = Decides(w, npc);
        Assert.True(Sharpens(a), $"chose {a}");
        Assert.NotEqual(StepOutcome.Refused, Sim.Step(w, a, Verbs.UntilDoneMinutes).Outcome);
        Assert.Equal(23, knife.Durability);
    }

    [Fact]
    public void AnNpc_WithAStatAtNeed_DoesNotSharpen()
    {
        var (w, npc, _) = Npc(15, stone: true);
        TestKit.ClearResources(((NpcMind)npc.Mind).Pack);
        npc.Body.HydrationMl = 0.3 * Body.MaxHydrationMl;
        Assert.False(Sharpens(Decides(w, npc)));
    }

    [Fact]
    public void AnNpc_WithNoStrikingTool_DoesNotSharpen()
    {
        var (w, npc, _) = Npc(15, stone: false);
        Assert.False(Sharpens(Decides(w, npc)));
    }

    [Fact]
    public void AnNpc_WithTheKnifeAboveHalf_DoesNotSharpen()
    {
        var (w, npc, _) = Npc(16, stone: true);
        Assert.False(Sharpens(Decides(w, npc)));
    }

    /// Cut short, the sharpen holds the knife in its project: the NPC goes on with it, even at Need.
    [Fact]
    public void AnNpc_GoesOnWithASharpenCutShort()
    {
        var (w, npc, knife) = Npc(15, stone: true);
        Assert.NotEqual(StepOutcome.Refused, Sim.Step(w, Decides(w, npc), 3).Outcome);
        Assert.DoesNotContain(knife, ((NpcMind)npc.Mind).Pack.Gear);
        npc.Body.HydrationMl = 0.3 * Body.MaxHydrationMl;
        var a = Decides(w, npc);
        Assert.True(Sharpens(a), $"chose {a}");
        Sim.Step(w, a, Verbs.UntilDoneMinutes);
        Assert.Contains(knife, ((NpcMind)npc.Mind).Pack.Gear);
        Assert.Equal(23, knife.Durability);
    }

    // ---- listed equals stepped ----

    public sealed record Case(int KnifeUses, bool Stone, double HydrationPct, bool Night)
    {
        public override string ToString() => $"knife {KnifeUses}/30, stone {Stone}, hydration {HydrationPct:0.00}, night {Night}";
    }

    static readonly Gen<Case> Cases = Gen.Select(Gen.Int[0, 30], Gen.Bool, Gen.Double[0.2, 1.0], Gen.Bool, (k, s, h, n) => new Case(k, s, h, n));

    /// Random knives, stones, thirst and hours: a sharpen it chooses is never refused, and stepped makes the knife better.
    [Fact]
    public void ListedEqualsStepped()
    {
        int sharpened = 0;
        Cases.Check(c =>
        {
            var (w, npc, knife) = Npc(c.KnifeUses, c.Stone);
            if (c.Night) while (Weather.IsDaytime(w.Minute)) w.Minute += 30;
            npc.Body.HydrationMl = c.HydrationPct * Body.MaxHydrationMl;
            var a = Decides(w, npc);
            var why = $"{c}: chose {a}";
            if (a is Make)
            {
                Interlocked.Increment(ref sharpened);
                Assert.True(Sharpens(a), why);
                Assert.NotEqual(StepOutcome.Refused, Sim.Step(w, a, Verbs.UntilDoneMinutes).Outcome);
                Assert.True(knife.Durability > c.KnifeUses, $"{why}: the knife at {knife.Durability}");
            }
        }, iter: 100);
        Assert.True(sharpened > 0, "no case sharpened");
    }
}
