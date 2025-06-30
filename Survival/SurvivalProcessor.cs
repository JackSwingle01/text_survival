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
  private const double MAX_EXHAUSTION_MINUTES  = 480.0F; // minutes (8 hours)
  
  public static SurvivalData Process(SurvivalData data, int minutesElapsed=1)
  {
    data.Exhaustion = Math.Min(1, data.Exhaustion + BASE_EXHAUSTION_RATE * minutesElapsed);
    return data; 
  }
  
}
