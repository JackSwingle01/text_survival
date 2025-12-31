using text_survival.Bodies;
using text_survival.Effects;

namespace text_survival.Survival;

public class SurvivalProcessorResult
{
    public SurvivalStatsDelta StatsDelta = new();
    public List<Effect> Effects = [];
    public List<string> Messages = [];
    public List<DamageInfo> DamageEvents = [];
    public List<HealingInfo> HealingEvents = [];
    public double FatToConsume;
    public double MuscleToConsume;
    public double BloodHealing;  // Blood regeneration (0-1 condition units)
    public double ClothingHeatBufferDelta;  // Change to clothing thermal mass buffer

    public void Combine(SurvivalProcessorResult other)
    {
        StatsDelta = StatsDelta.Add(other.StatsDelta);
        Effects.AddRange(other.Effects);
        Messages.AddRange(other.Messages);
        DamageEvents.AddRange(other.DamageEvents);
        HealingEvents.AddRange(other.HealingEvents);
        FatToConsume += other.FatToConsume;
        MuscleToConsume += other.MuscleToConsume;
        BloodHealing += other.BloodHealing;
        ClothingHeatBufferDelta += other.ClothingHeatBufferDelta;
    }
}