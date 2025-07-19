namespace text_survival.Bodies;

public class DamageResult
{
    public double DamageAbsorbed { get; set; }
    public string HitPartName { get; set; } = "";
    public double HitPartHealthBefore { get; set; }
    public double HitPartHealthAfter { get; set; }
    public double TotalDamageDealt => HitPartHealthBefore - HitPartHealthAfter;
    public List<(string TissueName, double DamageTaken)> TissuesDamaged { get; set; } = [];
    public bool OrganHit { get; set; }
    public string? OrganHitName { get; set; }
    public bool WasPenetrating { get; set; } // Did damage go through layers
}


public static class DamageProcessor
{
    public static DamageResult DamageBody(DamageInfo damageInfo, Body body)
    {
        var result = new DamageResult();

        // If targeting specific part, find it
        BodyRegion hitPart;
        if (damageInfo.TargetPartName != null)
        {
            hitPart = BodyTargetHelper.GetPartByName(body, damageInfo.TargetPartName)
                     ?? BodyTargetHelper.GetRandomMajorPartByCoverage(body);
        }
        else
        {
            // Otherwise, distribute based on coverage
            hitPart = BodyTargetHelper.GetRandomMajorPartByCoverage(body);
        }

        result.HitPartName = hitPart.Name;
        result.HitPartHealthBefore = hitPart.Condition;

        DamagePart(hitPart, damageInfo, result);

        result.HitPartHealthAfter = hitPart.Condition;
        return result;
    }
    private static void DamagePart(BodyRegion part, DamageInfo damageInfo, DamageResult result)
    {
        double remainingDamage = PenetrateLayers(part, damageInfo, result);
        if (remainingDamage <= 0) return;

        var hitOrgan = BodyTargetHelper.SelectRandomOrganToHit(part, damageInfo.Amount);
        if (hitOrgan != null)
        {
            result.OrganHit = true;
            result.OrganHitName = hitOrgan.Name;
            // Create new damage info for the organ with remaining damage
            var organDamage = new DamageInfo
            {
                Amount = remainingDamage,
                Type = damageInfo.Type,
                TargetPartName = hitOrgan.Name
            };
            double organHealthBefore = hitOrgan.Condition;
            DamageTissue(hitOrgan, organDamage);
            result.TissuesDamaged.Add((hitOrgan.Name, organHealthBefore - hitOrgan.Condition));
        }
        else if (part.Muscle != null)
        {
            // Deep tissue damage to muscle
            var muscleDamage = new DamageInfo
            {
                Amount = remainingDamage * 0.5,
                Type = damageInfo.Type
            };
            
            double muscleHealthBefore = part.Muscle.Condition;
            DamageTissue(part.Muscle, muscleDamage);
            double muscleDamageAmount = muscleHealthBefore - part.Muscle.Condition;
            
            if (muscleDamageAmount > 0)
                result.TissuesDamaged.Add((part.Muscle.Name, muscleDamageAmount));
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

    private static double PenetrateLayers(BodyRegion part, DamageInfo damageInfo, DamageResult result)
    {
        double remainingDamage = damageInfo.Amount;
        var layers = new[] { part.Skin, part.Muscle, part.Bone }.Where(l => l != null);

        foreach (var layer in layers)
        {
            if (remainingDamage <= 0) break;

            double protection = layer!.GetProtection(damageInfo.Type);
            double maxAbsorption = remainingDamage * 0.7;
            double actualAbsorption = Math.Min(maxAbsorption, protection);
            
            if (actualAbsorption > 0)
            {
                var layerDamage = new DamageInfo
                {
                    Amount = actualAbsorption,
                    Type = damageInfo.Type
                };
                
                double layerHealthBefore = layer.Condition;
                DamageTissue(layer, layerDamage);
                double layerDamageAmount = layerHealthBefore - layer.Condition;
                
                result.DamageAbsorbed += actualAbsorption;
                
                if (layerDamageAmount > 0)
                    result.TissuesDamaged.Add((layer.Name, layerDamageAmount));
            }
            
            remainingDamage -= actualAbsorption;
        }

        return Math.Max(0, remainingDamage);
    }

}