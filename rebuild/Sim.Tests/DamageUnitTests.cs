namespace Survival.Tests;

/// The damage unit (GLOSSARY.md, Damage): what a blow reaches and destroys comes from the toughness
/// of the tissues in its way, the size of the body and the kind of blow. These check capability and
/// order, never a balance number.
public class DamageUnitTests
{
    /// An average spear thrust gets through a wolf's hide, muscle and ribs to what is behind them; the
    /// hardest bite a wolf lands (luck's best, half again, on an unaware target) never gets through a
    /// mammoth's, though it gets through a fox's.
    [Fact]
    public void ASpearToTheTorso_CanReachAWolfsOrgans_AWolfBiteCannotReachAMammoths()
    {
        double spear = Items.Def(Items.Make("wooden_spear")).Weapon!.Damage;
        double hardestBite = SpeciesTable.Of(Species.Wolf).AttackDamage * 1.5 * Combat.AwarenessDamageFactor(Awareness.Unaware);
        Assert.True(TorsoOrganHits(Species.Wolf, spear) > 0);
        Assert.Equal(0, TorsoOrganHits(Species.Mammoth, hardestBite));
        Assert.True(TorsoOrganHits(Species.Fox, hardestBite) > 0);
    }

    /// Of 200 blows that land on the torso, how many reach an organ.
    static int TorsoOrganHits(Species species, double amount)
    {
        int hits = 0;
        var rng = Rng.ForSystem(1, "unit");
        for (int landed = 0; landed < 200;)
        {
            var hit = Body.Damage(SpeciesTable.Average(species), new DamageInfo(amount, DamageType.Pierce), rng);
            if (hit.HitRegion != "Torso") continue;
            landed++;
            if (hit.OrganHit != null) hits++;
        }
        return hits;
    }

    /// The same blow harms a small animal more than a big one: a bigger body's tissues are tougher.
    [Fact]
    public void TheSameBlow_HarmsASmallAnimalMoreThanABigOne()
    {
        var fox = SpeciesTable.Average(Species.Fox);
        var bear = SpeciesTable.Average(Species.Bear);
        Body.Damage(fox, new DamageInfo(0.8, DamageType.Sharp, BodyTarget.AnyLeg), Rng.ForSystem(1, "unit"));
        Body.Damage(bear, new DamageInfo(0.8, DamageType.Sharp, BodyTarget.AnyLeg), Rng.ForSystem(1, "unit"));
        double foxLost = fox.Regions.Sum(r => r.Layers.Sum(t => 1 - t.Condition));
        double bearLost = bear.Regions.Sum(r => r.Layers.Sum(t => 1 - t.Condition));
        Assert.True(foxLost > bearLost, $"fox lost {foxLost}, bear {bearLost}");
    }

    /// Soft tissue gives little to a club, which carries its force on to the bone and breaks it; an
    /// edge of the same weight spends itself in the flesh.
    [Fact]
    public void Blunt_ReachesBoneThroughSoftTissue_MoreThanSharpOfTheSameValue()
    {
        var clubbed = BodyState.BaselineHuman();
        var cut = BodyState.BaselineHuman();
        clubbed.Worn.Clear();
        cut.Worn.Clear();
        Body.Damage(clubbed, new DamageInfo(1.5, DamageType.Blunt, BodyTarget.LeftLeg), Rng.ForSystem(1, "unit"));
        Body.Damage(cut, new DamageInfo(1.5, DamageType.Sharp, BodyTarget.LeftLeg), Rng.ForSystem(1, "unit"));
        var clubbedLeg = clubbed.Regions.Single(r => r.Name == "Left Leg");
        var cutLeg = cut.Regions.Single(r => r.Name == "Left Leg");
        Assert.True(clubbedLeg.Bone.Condition < cutLeg.Bone.Condition,
            $"bone after a club {clubbedLeg.Bone.Condition}, after an edge {cutLeg.Bone.Condition}");
    }

    static BodyState Bare()
    {
        var b = BodyState.BaselineHuman();
        b.Worn.Clear();
        return b;
    }

    /// A wound bleeds from what it destroyed, deeper tissue more: a deeper stab bleeds more than a
    /// shallow one, and an organ torn under skin that held bleeds more than the same stab through an
    /// arm, which has no organ behind its bone.
    [Fact]
    public void Bleeding_GrowsWithWoundDepth_AndAnOrganBleedsUnderWholeSkin()
    {
        double Bleeds(double amount, BodyTarget target)
        {
            var b = Bare();
            Body.Damage(b, new DamageInfo(amount, DamageType.Pierce, target), Rng.ForSystem(1, "unit"));
            return Effects.BleedMlPerHour(b);
        }
        double shallow = Bleeds(0.3, BodyTarget.Abdomen), deeper = Bleeds(0.8, BodyTarget.Abdomen), deepest = Bleeds(2.5, BodyTarget.Abdomen);
        Assert.True(shallow < deeper && deeper < deepest, $"{shallow} {deeper} {deepest}");

        var stabbed = Bare();
        var hit = Body.Damage(stabbed, new DamageInfo(2.5, DamageType.Pierce, BodyTarget.Abdomen), Rng.ForSystem(1, "unit"));
        Assert.NotNull(hit.OrganHit);
        Assert.True(stabbed.Regions.Single(r => r.Name == "Abdomen").Skin.Condition > 0, "the skin held");
        Assert.True(Effects.BleedMlPerHour(stabbed) > Bleeds(2.5, BodyTarget.LeftArm));
    }

    /// A cut opens the skin and bleeds, however small; a bruise of the same size does not.
    [Fact]
    public void ACut_Bleeds_ABruiseDoesNot()
    {
        var cut = Bare();
        var bruised = Bare();
        Body.Damage(cut, new DamageInfo(0.08, DamageType.Sharp, BodyTarget.LeftArm), Rng.ForSystem(1, "unit"));
        Body.Damage(bruised, new DamageInfo(0.08, DamageType.Blunt, BodyTarget.LeftArm), Rng.ForSystem(1, "unit"));
        Assert.True(Effects.Has(cut, EffectKind.Bleeding));
        Assert.False(Effects.Has(bruised, EffectKind.Bleeding));
    }

    /// A narrow blow cannot destroy a whole layer at once, but blow after blow it does.
    [Fact]
    public void OneStab_LeavesTheSkin_ButStabAfterStabDestroysTheRegion()
    {
        var b = Bare();
        var arm = b.Regions.Single(r => r.Name == "Left Arm");
        Body.Damage(b, new DamageInfo(5, DamageType.Pierce, BodyTarget.LeftArm), Rng.ForSystem(1, "unit"));
        Assert.True(arm.Skin.Condition > 0);
        for (int i = 0; i < 10 && !arm.IsDestroyed; i++)
            Body.Damage(b, new DamageInfo(5, DamageType.Pierce, BodyTarget.LeftArm), Rng.ForSystem(i, "unit"));
        Assert.True(arm.IsDestroyed);
    }

    /// Blood is 70 ml a kilo, and a wound of the same severity drains a big body's share of it slower.
    [Fact]
    public void ABigAnimal_BleedsOutSlowerFromTheSameSeverity()
    {
        var wolf = SpeciesTable.Average(Species.Wolf);
        var bison = SpeciesTable.Average(Species.Bison);
        Assert.Equal(70 * wolf.WeightKg, Body.BloodVolumeMl(wolf), 6);
        Effects.Add(wolf, EffectKind.Bleeding, 0.5);
        Effects.Add(bison, EffectKind.Bleeding, 0.5);
        Assert.True(Effects.BleedMlPerHour(wolf) / Body.BloodVolumeMl(wolf) > Effects.BleedMlPerHour(bison) / Body.BloodVolumeMl(bison));
    }
}
