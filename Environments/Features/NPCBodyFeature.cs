using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// The body of a deceased NPC ally. Can be looted for belongings or buried.
/// Discovery-based: body is hidden until player arrives at the tile.
/// </summary>
public class NPCBodyFeature : LocationFeature, IWorkableFeature
{
    // Only show icon if discovered and not buried
    public override string? MapIcon => IsDiscovered && !IsBuried ? "body" : null;
    public override int IconPriority => 4;  // High priority - this is important

    public string NPCName { get; set; } = "";
    public string DeathCause { get; set; } = "";
    public DateTime TimeOfDeath { get; set; }
    public Inventory Belongings { get; set; } = new();
    public bool IsBuried { get; set; }

    // Discovery - body is hidden until player arrives
    public bool IsDiscovered { get; set; } = false;

    // Decay tracking (pattern from CarcassFeature)
    public double HoursSinceDeath { get; set; } = 0;
    public double LastKnownTemperatureF { get; set; } = 32;

    /// <summary>
    /// Decay level from 0 (fresh) to 1 (skeletal).
    /// Temperature affects decay rate - cold preserves.
    /// </summary>
    public double DecayLevel
    {
        get
        {
            double tempMultiplier = LastKnownTemperatureF switch
            {
                < 20 => 0.1,   // frozen - very slow
                < 32 => 0.3,   // cold - slow
                < 50 => 0.7,   // cool
                _ => 1.5       // warm - fast
            };
            double effectiveHours = HoursSinceDeath * tempMultiplier;
            // Fresh: 0-24h, Decomposing: 24-72h, Skeletal: 72h+
            return Math.Min(1.0, effectiveHours / 72.0);
        }
    }

    public string DecayDescription => DecayLevel switch
    {
        < 0.15 => "is fresh",
        < 0.4 => "has begun to decay",
        < 0.7 => "is bloated and rotting",
        _ => "only bones remain"
    };

    /// <summary>
    /// Apply temperature-aware decay over time.
    /// Called from Location.Update() each tick.
    /// </summary>
    public void ApplyTemperatureDecay(double temperatureF, int minutes)
    {
        if (IsBuried) return;

        HoursSinceDeath += minutes / 60.0;
        LastKnownTemperatureF = temperatureF;
    }

    // For JSON deserialization
    public NPCBodyFeature() : base("body") { }

    public NPCBodyFeature(string npcName, string deathCause, DateTime timeOfDeath, Inventory belongings)
        : base($"Remains of {npcName}")
    {
        NPCName = npcName;
        DeathCause = deathCause;
        TimeOfDeath = timeOfDeath;
        Belongings = belongings;
    }

    public override FeatureUIInfo? GetUIInfo()
    {
        // Don't show in UI until discovered
        if (!IsDiscovered)
            return null;

        if (IsBuried)
            return new FeatureUIInfo("grave", $"{NPCName}'s grave", "buried", null);

        return new FeatureUIInfo("body", NPCName, DecayDescription, null);
    }

    public override List<Resource> ProvidedResources() => [];

    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        // Can't interact until discovered
        if (!IsDiscovered || IsBuried)
            yield break;

        // Always offer burial
        yield return new WorkOption($"Bury {NPCName}", "bury", new BuryStrategy(this));

        // Offer to take belongings if they have any
        if (Belongings.CurrentWeightKg > 0 || Belongings.Tools.Count > 0 || Belongings.Accessories.Count > 0)
        {
            yield return new WorkOption($"Take {NPCName}'s belongings", "loot-body", new LootBodyStrategy(this));
        }
    }


    public static string DetermineDeathCause(NPC npc)
    {
        // Check effects for cause of death
        if (npc.EffectRegistry.HasEffect("Hypothermia"))
            return "The cold took them.";

        if (npc.EffectRegistry.GetSeverity("Bleeding") > 0.5)
            return "Blood loss.";

        // Check body stats
        if (npc.Body.Blood.Condition < 0.2)
            return "They bled out.";

        if (npc.Body.CalorieStore < 100)
            return "Starvation.";

        if (npc.Body.Hydration < 500) // Hydration is in mL
            return "Dehydration.";

        if (npc.Body.BodyTemperature < 85)
            return "They froze to death.";

        // Check for major organ failure
        var allOrgans = BodyTargetHelper.GetAllOrgans(npc.Body);
        var heart = allOrgans.FirstOrDefault(o => o.Name == OrganNames.Heart);
        if (heart != null && heart.Condition < 0.1)
            return "Heart failure.";

        var brain = allOrgans.FirstOrDefault(o => o.Name == OrganNames.Brain);
        if (brain != null && brain.Condition < 0.1)
            return "Head trauma.";

        var leftLung = allOrgans.FirstOrDefault(o => o.Name == OrganNames.LeftLung);
        var rightLung = allOrgans.FirstOrDefault(o => o.Name == OrganNames.RightLung);
        if ((leftLung != null && leftLung.Condition < 0.1) || (rightLung != null && rightLung.Condition < 0.1))
            return "They couldn't breathe.";

        return "Their body gave out.";
    }

    public string DeathDiscoveryText()
    {
        string text = DecayLevel switch
        {
            < 0.15 => $"You find {NPCName}. They aren't breathing. {DeathCause}",
            < 0.4 => $"{NPCName} lies still. Cold to the touch. {DeathCause}",
            < 0.7 => $"The smell reaches you first. {NPCName}'s body has begun to rot.",
            _ => $"Scattered bones. A torn piece of cloth you recognize. {NPCName} is gone."
        };
        return text;
    }
}
