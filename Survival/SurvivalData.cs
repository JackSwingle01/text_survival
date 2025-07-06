
using text_survival.Bodies;

namespace text_survival.Survival;

public class SurvivalData
{
	// Primary stats
	public double Calories;
	public double Hydration;
	public double Energy;
	public double Temperature;
	public double ColdResistance;

	// body stats
	public BodyStats BodyStats;

	// Secondary stats
	public double equipmentInsulation;
	public double environmentalTemp;
	public double activityLevel;
	public bool IsPlayer;
}
