namespace Survival.Tests;

/// Reach, the braced first strike and the weapon table: how far each fighter strikes
/// from, who strikes first when one closes on a longer reach, and the order of the weapons in a duel.
public class CombatReachTests
{
    static readonly AgentId P = AgentId.Player;
    static CombatUnit Me(Encounter e) => Combat.UnitOf(e, P);
    static CombatUnit B(Encounter e) => e.Units.First(u => u.Team == 1);

    static (World W, Encounter E) PlayerVs(Species species, int distanceM, string? weapon, World? world = null)
    {
        var (w, t) = CombatTests.Field(world ?? TestWorlds.New(1));
        var herd = CombatTests.Herd(w, species, 1, t);
        CombatTests.Normalize(w, herd);
        Gear? g = weapon == null ? null : Items.Make(weapon);
        if (g != null) Items.Pack(w, P).Gear.Add(g);
        var e = Combat.Create(w, [new Fighter(P) { Weapon = g }], [new Fighter(herd)], t, distanceM, Awareness.Engaged, Awareness.Engaged);
        return (w, e);
    }

    // ---- reach ----

    [Theory]
    [InlineData("wooden_spear", 2.5)]
    [InlineData("crude_spear", 2.5)]
    [InlineData("stone_axe", Combat.TouchingM)]
    [InlineData("flint_knife", Combat.TouchingM)]
    [InlineData(null, Combat.TouchingM)] // bare hands, 0.8 m: a neighbouring cell is as near as two fighters stand
    public void EachWeaponStrikesFromItsOwnReach(string? weapon, double rangeM)
    {
        var (w, e) = PlayerVs(Species.Wolf, 5, weapon);
        Assert.Equal(rangeM, Combat.StrikeRangeM(w, Me(e)), 9);
    }

    [Fact]
    public void ASpearReachesTwoMetres_HandsAndJawsDoNot()
    {
        var (w, e) = PlayerVs(Species.Wolf, 2, "wooden_spear");
        Assert.Equal(2, Me(e).Position.DistanceTo(B(e).Position), 9);
        Assert.Null(Combat.Refusal(w, e, Me(e), CombatAction.Attack));
        Assert.Equal("Out of reach.", Combat.Refusal(w, e, B(e), CombatAction.Attack)?.Reason);
        Me(e).Weapon = null;
        Assert.Equal("Out of reach.", Combat.Refusal(w, e, Me(e), CombatAction.Attack)?.Reason);
        var (w2, tusks) = PlayerVs(Species.Mammoth, 2, null);
        Assert.Null(Combat.Refusal(w2, tusks, B(tusks), CombatAction.Attack)); // 2.5 m of tusk
    }

    // ---- the braced first strike ----

    /// A wolf closing from 6 m on a person standing off with a spear meets the point before it bites:
    /// whenever both land, the wolf's wound comes first, and over many seeds the spear lands often.
    [Fact]
    public void ABracedSpear_StrikesFirst_OnAnAnimalThatClosesInsideItsReach()
    {
        int braced = 0;
        for (int seed = 1; seed <= 60; seed++)
        {
            var (w, e) = PlayerVs(Species.Wolf, 6, "wooden_spear", World.New(seed));
            var wolf = B(e);
            wolf.State = FighterState.Pressing;
            wolf.BoldnessModifier = 10; // it comes on whatever it meets
            Me(e).State = FighterState.Standoff;
            var events = new List<WorldEvent>();
            Combat.AiTurn(w, e, wolf, events);
            var wounds = events.OfType<Injured>().Select(i => i.Who).ToList();
            int wolfHurt = wounds.IndexOf(wolf.Agent), meHurt = wounds.IndexOf(P);
            if (wolfHurt < 0) continue;
            braced++;
            Assert.True(meHurt < 0 || wolfHurt < meHurt, $"seed {seed}: the bite landed before the braced spear");
        }
        Assert.InRange(braced, 20, 60);
    }

    /// Nothing braces without the longer reach, or while pressing in: the wolf's turn draws no blood from it.
    [Theory]
    [InlineData(null, FighterState.Standoff)]
    [InlineData("flint_knife", FighterState.Standoff)]
    [InlineData("wooden_spear", FighterState.Pressing)]
    public void NoBracedStrike_WithoutTheLongerReach_OrWhilePressing(string? weapon, FighterState mine)
    {
        for (int seed = 1; seed <= 30; seed++)
        {
            var (w, e) = PlayerVs(Species.Wolf, 6, weapon, World.New(seed));
            var wolf = B(e);
            wolf.State = FighterState.Pressing;
            wolf.BoldnessModifier = 10;
            Me(e).State = mine;
            var events = new List<WorldEvent>();
            Combat.AiTurn(w, e, wolf, events);
            Assert.DoesNotContain(events.OfType<Injured>(), i => i.Who == wolf.Agent);
        }
    }

    /// Reach decides it, not who holds it: a mammoth standing off meets a person stepping inside its tusks.
    [Fact]
    public void ALongerReach_BracesForAnimalsToo()
    {
        int struck = 0;
        for (int seed = 1; seed <= 30; seed++)
        {
            var (w, e) = PlayerVs(Species.Mammoth, 4, null, World.New(seed));
            B(e).State = FighterState.Standoff;
            var events = new List<WorldEvent>();
            Combat.Apply(w, e, Me(e), CombatAction.Advance, events);
            if (events.OfType<Injured>().Any(i => i.Who == P)) struck++;
        }
        Assert.InRange(struck, 5, 30);
    }

    // ---- the weapon table ----

    [Fact]
    public void WhatIsNotMadeToBeThrown_IsNotThrown_AndEachThrowCarriesItsOwnRange()
    {
        var (w, e) = PlayerVs(Species.Bison, 8, "staff");
        Assert.Equal("That is not made to be thrown.", Combat.Refusal(w, e, Me(e), CombatAction.Throw)?.Reason);
        var (w2, knife) = PlayerVs(Species.Bison, 12, "flint_knife");
        Assert.Equal("Beyond weapon range.", Combat.Refusal(w2, knife, Me(knife), CombatAction.Throw)?.Reason);
        var (w3, stone) = PlayerVs(Species.Bison, 6, "stone");
        Assert.Equal(0.50 * (1 - 6.0 / 15), Combat.ThrowChance(w3, Me(stone), B(stone), Me(stone).Weapon!), 9);
        Assert.Equal(Combat.ThrowChance(w3, Me(stone), B(stone), Me(stone).Weapon!), Combat.StoneChance(w3, Me(stone), B(stone)), 9);
    }

    /// An average fresh member of a kind strikes with its kind's own number: what the tables say is
    /// what lands, before luck.
    [Theory]
    [InlineData(Species.Wolf)]
    [InlineData(Species.Bear)]
    [InlineData(Species.Mammoth)]
    public void AnAverageAnimal_StrikesWithItsTablesDamage(Species species)
    {
        var (w, e) = PlayerVs(species, 5, null);
        Assert.Equal(SpeciesTable.Of(species).AttackDamage, Combat.AverageBlow(w, B(e)).Amount, 9);
    }

    /// Whoever picks a weapon from a pack picks by its worth, and the table ranks the spear first: the
    /// axe hits harder but strikes from contact.
    [Fact]
    public void ThePackPick_RanksTheSpearFirst()
    {
        double Worth(string kind) => Combat.WeaponWorth(Items.GearKinds[kind].Weapon!);
        Assert.True(Worth("wooden_spear") > Worth("stone_axe"));
        Assert.True(Worth("stone_axe") > Worth("flint_knife"));
        Assert.True(Worth("flint_knife") > Worth("stone"));
        Assert.True(Worth("stone") > Worth("staff"));
        Assert.True(Worth("wooden_spear") > Worth("crude_spear"));
    }

    /// Standing off with its nerve going, an animal backs away in proportion, never through anyone.
    [Fact]
    public void InStandoff_AnAnimalLosingItsNerve_StepsBack()
    {
        var (w, e) = PlayerVs(Species.Wolf, 6, "wooden_spear");
        var wolf = B(e);
        wolf.State = FighterState.Standoff;
        wolf.BoldnessModifier += -0.25 - Combat.Resolve(w, e, wolf, Me(e)); // resolve -0.25: inside Standoff, past the deadband
        double gap = wolf.Position.DistanceTo(Me(e).Position);
        var (action, to, _) = Combat.ChooseAction(w, e, wolf);
        Assert.Equal(CombatAction.Move, action);
        Assert.True(to!.Value.DistanceTo(Me(e).Position) > gap + 1, $"to {to}");
    }
}
