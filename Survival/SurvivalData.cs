using text_survival.Bodies;

namespace text_survival.Survival;

public class SurvivalData
{
	// Primary stats
	public double Calories;
	public double Hydration;
	public double Exhaustion;
	public double Temperature;
	public double ColdResistance;

	// Body stats
	public double BodyWeight;
	public double MuscleWeight;
	public double FatWeight;
	public double HealthPercent; // 0-1

	// Secondary stats
	public double equipmentInsulation;
	public double environmentalTemp;
	public double activityLevel;
	public bool IsPlayer;
}
