using text_survival;
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.IO;

public class PoisonEffect : Effect
{
    private float _damagePerHour;
    private string _poisonType;
    
    public PoisonEffect(string poisonType, string source, float severity, float damagePerHour, int durationMin = 60)
        : base("Poison", source, null, severity) // Poison is typically whole-body
    {
        _damagePerHour = damagePerHour;
        _poisonType = poisonType;
        
        // Configure effect properties
        SeverityChangeRate = -0.02f; // Slow natural detoxification
        IsStackable = true; // Multiple poison sources can stack
        
        // Configure capacity modifiers - affect whole body
        CapacityModifiers["Consciousness"] = 0.3f * severity;
        CapacityModifiers["Manipulation"] = 0.2f * severity;
        CapacityModifiers["Moving"] = 0.2f * severity;
        CapacityModifiers["BloodFiltration"] = 0.4f * severity;
    }
    
    protected override void OnApply(Actor target)
    {
        Output.WriteLine($"{target} has been poisoned with {_poisonType}!");
    }
    
    protected override void OnUpdate(Actor target)
    {
        // Calculate damage for one minute based on severity
        double damage = _damagePerHour / 60.0 * Severity;
        
        // Apply poison damage randomly to internal organs
        // Get list of internal parts
        var internalParts = target.Body.GetAllParts()
            .Where(p => p.IsInternal && !p.IsDestroyed)
            .ToList();
            
        if (internalParts.Count > 0)
        {
            // Target a random internal organ for damage
            var targetPart = internalParts[Utils.RandInt(0, internalParts.Count - 1)];
            
            var damageInfo = new DamageInfo
            {
                Amount = damage,
                Type = "poison",
                Source = Source,
                IsPenetrating = true, // Poison always penetrates
                TargetPart = targetPart.Name
            };
            
            target.Damage(damageInfo);
        }
        else
        {
            // Fallback to general damage
            var damageInfo = new DamageInfo
            {
                Amount = damage,
                Type = "poison",
                Source = Source,
                IsPenetrating = true
            };
            
            target.Damage(damageInfo);
        }
        
        // Symptoms based on severity
        if (Severity > 0.7 && Utils.DetermineSuccess(0.2))
        {
            Output.WriteLine($"{target} vomits violently from the {_poisonType} poisoning.");
        }
        else if (Severity > 0.4f && Utils.DetermineSuccess(0.1))
        {
            Output.WriteLine($"{target} trembles from the effects of the {_poisonType} poison.");
        }
    }
    
    protected override void OnSeverityChange(Actor target, double oldSeverity, double updatedSeverity)
    {
        // Update capacity modifiers based on new severity
        CapacityModifiers["Consciousness"] = 0.3 * updatedSeverity;
        CapacityModifiers["Manipulation"] = 0.2 * updatedSeverity;
        CapacityModifiers["Moving"] = 0.2 * updatedSeverity;
        
        if (updatedSeverity < 0.3 && oldSeverity >= 0.3)
        {
            Output.WriteLine($"The {_poisonType} poisoning is becoming less severe.");
        }
    }
    
    protected override void OnRemove(Actor target)
    {
        Output.WriteLine($"{target} has recovered from {_poisonType} poisoning.");
    }
}