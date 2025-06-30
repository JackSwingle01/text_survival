namespace text_survival.Survival;
public class SurvivalData
{
  public double Calories;
  public double MetabolicRate = 2500; // calories burned per day
  public double Hydration;
  public double Exhaustion;
  public double Temperature;
}
public static class SurvivalProcessor
{
  private const double BASE_EXHAUSTION_RATE = 1;
  private const double MAX_EXHAUSTION_MINUTES  = 960.0F; // minutes (16 hours)

  private const double BASE_DEHYDRATION_RATE = 4000F / (24F * 60F); // mL per minute
  private const double MAX_HYDRATION = 4000.0F; // mL

  private const double MAX_CALORIES = 2000.0; // Maximum calories stored before fat conversion
  
  
  public static SurvivalData Process(SurvivalData data, int minutesElapsed=1)
  {
    data.Exhaustion = Math.Min(1, data.Exhaustion + (BASE_EXHAUSTION_RATE * minutesElapsed));
    data.Hydration = Math.Max(0, data.Hydration - (BASE_DEHYDRATION_RATE * minutesElapsed));

    // todo, actually update with activity level
		// todo have this account for temp too
    bool wasStarving = data.Calories <= 0;
	data.Calories -= data.MetabolicRate / 24 / 60 * minutesElapsed;
	
	if (data.Calories <= 0)
	{
		double excessCalories = -data.Calories;
		data.Calories = 0;
		EventBus.Publish(new StarvingEvent(owner, excessCalories, isNew: !wasStarving));
	}
	//else if (wasStarving) // wasStarving but is no longer // TODO this will never be hit anymore, move to eat method
	//{
		//EventBus.Publish(new StoppedStarvingEvent(owner));
	//}
    return data; 
  }

  public static SurvivalData Sleep(SurvivalData data, int minutes)
  {
    // rest restores exhaustion at 2x the rate that you gain it while awake, so 16 hours of wakefulness creates only 8 hours of sleep debt
    data.Exhaustion = Math.Max(0, data.Exhaustion - (BASE_EXHAUSTION_RATE * 2 * minutesElapsed)); 
    data.Hydration = Math.Max(0, data.Hydration - (BASE_DEHYDRATION_RATE * .7 * minutesElapsed)); // dehydrate at reduced rate while asleep
    data.Calories = data.Calories -= data.MetabolicRate / 24 / 60 * minutesElapsed * .5;  // starve at 1/2 rate
    return data;
  }

	public static void Describe(SurvivalData data)
	{
		// calories
		double percent = (int)(data.Calories / MAX_CALORIES * 100);
		Output.WriteLine("| Calorie Store: ", percent, "%");
		// hydration
		 double percent = (int)((data.Hydration / Max) * 100);
            	Output.WriteLine("| Hydration: ", percent, "%");
		// exhaustion
		double percent = (int)((data.Exhaustion / Max) * 100);
            	Output.WriteLine("| Exhaustion: ", percent, "%");
		// temp
		//string tempChange = IsWarming ? "Warming up" : "Getting colder";
        	Output.WriteLine("| Body Temperature: ", data.Temperature, "°F");//(", TemperatureEffect, "), ", tempChange);
	}
  
}
