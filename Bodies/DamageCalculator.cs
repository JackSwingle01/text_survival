namespace text_survival.Bodies;

public static class DamageProcessor
{
    public static void DamageBody(DamageInfo damageInfo, Body body)
    {
        // If targeting specific part, find it
        if (damageInfo.TargetPartName != null)
        {
            var targetPart = BodyTargetHelper.GetPartByName(body, damageInfo.TargetPartName);
            if (targetPart != null)
            {
                DamagePart(targetPart, damageInfo);
                return;
            }
        }

        // Otherwise, distribute based on coverage
        var hitPart = BodyTargetHelper.GetRandomMajorPartByCoverage(body);

        DamagePart(hitPart, damageInfo);
    }
    private static void DamagePart(BodyRegion part, DamageInfo damageInfo)
    {
        double remainingDamage = PenetrateLayers(part, damageInfo);
        if (remainingDamage <= 0) return;

        var hitOrgan = BodyTargetHelper.SelectRandomOrganToHit(part, damageInfo.Amount);
        if (hitOrgan != null)
        {
            // Create new damage info for the organ with remaining damage
            var organDamage = new DamageInfo
            {
                Amount = remainingDamage,
                Type = damageInfo.Type,
                TargetPartName = hitOrgan.Name
            };
            DamageTissue(hitOrgan, organDamage);
        }
        else
        {
            // No organ hit - damage goes to muscle tissue as deep tissue damage
            var muscleDamage = new DamageInfo
            {
                Amount = remainingDamage * 0.5, // Reduced effect for deep tissue
                Type = damageInfo.Type,
                TargetPartName = part.Muscle?.Name
            };
            if (part.Muscle != null)
            {
                DamageTissue(part.Muscle, muscleDamage);
            }
        }
        // DamageTissue(hitOrgan, damageInfo);
    }

    private static void DamageTissue(Tissue tissue, DamageInfo damageInfo)
    {
        double absorption = tissue.GetNaturalAbsorption(damageInfo.Type);
        damageInfo.Amount -= absorption;
        if (damageInfo.Amount <= 0)
        {
            return; // Natural squishiness absorbed it
        }

        double healthLoss = damageInfo.Amount / tissue.GetProtection(damageInfo.Type);
        tissue.Condition = Math.Max(0, tissue.Condition - healthLoss);
    }

    private static double PenetrateLayers(BodyRegion part, DamageInfo damageInfo)
    {
        DamageType damageType = damageInfo.Type;
        double damage = damageInfo.Amount;
        var layers = new[] { part.Skin, part.Muscle, part.Bone }.Where(l => l != null);

        foreach (var layer in layers)
        {
            double protection = layer!.GetProtection(damageType);
            double absorbed = Math.Min(damage * 0.7, protection); // Layer absorbs up to 70% of damage

            damageInfo.Amount -= absorbed;

            DamageTissue(layer, new DamageInfo(absorbed, damageInfo.Type, damageInfo.Source)); // Layer takes damage from absorbing

            if (damageInfo.Amount <= 0) break;
        }

        return Math.Max(0, damage);
    }

}