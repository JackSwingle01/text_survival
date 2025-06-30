namespace text_survival.Survival;
public class SurvivalData
{
  public double Hunger;
  public double Thirst;
  public double Exhaustion;
  public double Temperature;
}
public static class SurvivalProcessor
{
  private const double BASE_EXHAUSTION_RATE = 1;
  private const double MAX_EXHAUSTION_MINUTES  = 960.0F; // minutes (16 hours)
  
  public static SurvivalData Process(SurvivalData data, int minutesElapsed=1)
  {
    data.Exhaustion = Math.Min(1, data.Exhaustion + (BASE_EXHAUSTION_RATE * minutesElapsed));
    return data; 
  }

  public static SurvivalData Sleep(SurvivalData data, int minutes)
  {
    // rest restores exhaustion at 2x the rate that you gain it while awake, so 16 hours of wakefulness creates only 8 hours of sleep debt
    data.Exhaustion = Math.Max(0, data.Exhaustion - (BASE_EXHAUSTION_RATE * 2 * minutesElapsed)); 
    return data;
  }
  
}
