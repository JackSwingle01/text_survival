using text_survival.Effects;

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
    public List<Effect> TriggeredEffects { get; set; } = [];  // e.g., Bleeding, Pain from damage
}


public static class DamageProcessor
{
    /// <summary>
    /// Applies armor reduction to incoming damage before body processing.
    /// Cushioning absorbs Blunt damage, Toughness resists Sharp/Pierce.
    /// Internal, Bleed, Burn, and Poison damage bypass armor.
    /// </summary>
    private static void ApplyArmorReduction(DamageInfo damageInfo)
    {
        // Skip armor for damage types that bypass it
        if (damageInfo.Type == DamageType.Internal ||
            damageInfo.Type == DamageType.Bleed ||
            damageInfo.Type == DamageType.Burn ||
            damageInfo.Type == DamageType.Poison)
        {
            return;
        }

        // No armor equipped - skip calculation
        if (damageInfo.ArmorCushioning <= 0 && damageInfo.ArmorToughness <= 0)
        {
            return;
        }

        // Calculate armor reduction based on damage type
        double armorReduction = damageInfo.Type switch
        {
            DamageType.Blunt => damageInfo.ArmorCushioning,                    // Pure cushioning
            DamageType.Sharp => damageInfo.ArmorToughness,                     // Pure toughness
            DamageType.Pierce => damageInfo.ArmorToughness * 0.7,              // Mostly toughness (penetrates padding)
            _ => (damageInfo.ArmorCushioning + damageInfo.ArmorToughness) / 2  // Average for other types
        };

        // Clamp reduction to 0-0.8 (max 80% damage reduction from armor)
        armorReduction = Math.Clamp(armorReduction, 0, 0.8);

        // Apply reduction
        damageInfo.Amount *= (1 - armorReduction);
    }

    public static DamageResult DamageBody(DamageInfo damageInfo, Body body)
    {
        // Apply armor reduction before any damage processing
        ApplyArmorReduction(damageInfo);

        // Resolve enum to actual part name
        damageInfo.TargetPartName = BodyTargetResolver.ResolveTargetName(damageInfo.Target, body);

        var result = new DamageResult();

        // Handle Blood targeting for Bleed/Internal damage (blood loss bypasses body parts)
        if (damageInfo.TargetPartName == "Blood" &&
            (damageInfo.Type == DamageType.Bleed || damageInfo.Type == DamageType.Internal))
        {
            double healthBefore = body.Blood.Condition;
            DamageTissue(body.Blood, damageInfo);
            result.HitPartName = "Blood";
            result.TissuesDamaged.Add(("Blood", healthBefore - body.Blood.Condition));
            return result;
        }

        // Check if targeting a specific organ by name
        Organ? targetOrgan = null;
        BodyRegion hitPart;

        if (damageInfo.TargetPartName != null)
        {
            // Try to find organ first (only for Internal damage to prevent armor bypass exploits)
            if (damageInfo.Type == DamageType.Internal)
            {
                var allOrgans = BodyTargetHelper.GetAllOrgans(body);
                targetOrgan = allOrgans.FirstOrDefault(o => o.Name == damageInfo.TargetPartName);
            }

            if (targetOrgan != null)
            {
                // Find the body part containing this organ
                hitPart = body.Parts.First(p => p.Organs.Contains(targetOrgan));
            }
            else
            {
                // Fall back to body part targeting
                hitPart = BodyTargetHelper.GetPartByName(body, damageInfo.TargetPartName)
                         ?? BodyTargetHelper.GetRandomMajorPartByCoverage(body);
            }
        }
        else
        {
            // Otherwise, distribute based on coverage
            hitPart = BodyTargetHelper.GetRandomMajorPartByCoverage(body);
        }

        result.HitPartName = hitPart.Name;
        result.HitPartHealthBefore = hitPart.Condition;

        // If we have a specific organ target, damage it directly
        if (targetOrgan != null)
        {
            result.OrganHit = true;
            result.OrganHitName = targetOrgan.Name;
            double organHealthBefore = targetOrgan.Condition;
            DamageTissue(targetOrgan, damageInfo);
            result.TissuesDamaged.Add((targetOrgan.Name, organHealthBefore - targetOrgan.Condition));
        }
        else
        {
            DamagePart(hitPart, damageInfo, result);
        }

        result.HitPartHealthAfter = hitPart.Condition;

        // Check for bleeding trigger - sharp/pierce damage that broke skin
        // Threshold: 3% of skin condition lost triggers bleeding
        if ((damageInfo.Type == DamageType.Sharp || damageInfo.Type == DamageType.Pierce)
            && result.TissuesDamaged.Any(t => t.TissueName == "Skin" && t.DamageTaken > 0.03))
        {
            double bleedSeverity = Math.Clamp(result.TotalDamageDealt * 0.5, 0.15, 1.0);
            result.TriggeredEffects.Add(EffectFactory.Bleeding(bleedSeverity));
        }

        // Blunt trauma causing internal bleeding (severe muscle/bone damage)
        // Threshold: 20% total tissue damage triggers internal bleeding
        if (damageInfo.Type == DamageType.Blunt)
        {
            double totalTissueDamage = result.TissuesDamaged.Sum(t => t.DamageTaken);
            if (totalTissueDamage > 0.20)
            {
                double bleedSeverity = Math.Clamp(totalTissueDamage * 1.5, 0.2, 0.8);
                result.TriggeredEffects.Add(EffectFactory.Bleeding(bleedSeverity));
            }
        }

        // Shock from massive trauma
        // Threshold: 25% of a limb's worth of damage triggers shock
        if (result.TotalDamageDealt > 0.25)
        {
            double shockSeverity = Math.Clamp((result.TotalDamageDealt - 0.25) * 2.0, 0.2, 0.8);
            result.TriggeredEffects.Add(EffectFactory.Shock(shockSeverity));
        }

        // Check for pain trigger - any external damage type
        // Threshold: 2% damage triggers pain
        if ((damageInfo.Type == DamageType.Blunt ||
             damageInfo.Type == DamageType.Sharp ||
             damageInfo.Type == DamageType.Pierce ||
             damageInfo.Type == DamageType.Burn)
            && result.TotalDamageDealt > 0.02)
        {
            double painSeverity = Math.Clamp(result.TotalDamageDealt * 1.0, 0.1, 0.8);
            result.TriggeredEffects.Add(EffectFactory.Pain(painSeverity));
        }

        // Check for dazed trigger - blunt damage to head
        // Threshold: 8% damage to head triggers dazed
        if (damageInfo.Type == DamageType.Blunt
            && result.HitPartName.Equals("Head", StringComparison.OrdinalIgnoreCase)
            && result.TotalDamageDealt > 0.08)
        {
            double dazedSeverity = Math.Clamp(result.TotalDamageDealt * 1.5, 0.2, 0.8);
            result.TriggeredEffects.Add(EffectFactory.Dazed(dazedSeverity));
        }

        return result;
    }
    private static void DamagePart(BodyRegion part, DamageInfo damageInfo, DamageResult result)
    {
        // Internal damage bypasses armor layers (starvation, dehydration, disease)
        double remainingDamage = damageInfo.Type == DamageType.Internal
            ? damageInfo.Amount
            : PenetrateLayers(part, damageInfo, result);
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