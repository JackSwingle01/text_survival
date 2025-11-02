namespace text_survival.Tests.TestHelpers;

/// <summary>
/// Constants for use in unit tests
/// </summary>
public static class TestConstants
{
    // Baseline human body stats (from Body.BaselineHumanStats)
    public static class BaselineHuman
    {
        public const double Weight = 75.0;
        public const double FatPercent = 0.15;
        public const double MusclePercent = 0.30;
        public const double FatWeight = Weight * FatPercent;      // 11.25 kg
        public const double MuscleWeight = Weight * MusclePercent; // 22.5 kg
        public const double StructuralWeight = Weight - FatWeight - MuscleWeight; // 41.25 kg
    }

    // Normal temperature ranges
    public static class Temperature
    {
        public const double NormalBodyTemp = 98.6;
        public const double FreezingThreshold = 89.6;
        public const double ColdThreshold = 95.0;
        public const double CoolThreshold = 98.6;
        public const double WarmThreshold = 100.0;
        public const double RoomTemp = 70.0;
        public const double ExtremeCold = -40.0;
        public const double ExtremeHeat = 120.0;
    }

    // Survival constants
    public static class Survival
    {
        public const double MaxCalories = 2000.0;
        public const double MaxHydration = 4000.0;
        public const double MaxEnergyMinutes = 960.0; // 16 hours
        public const double BaseExhaustionRate = 1.0;
        public const double BaseDehydrationRate = 4000.0 / (24 * 60); // ~2.78 mL/min
    }

    // Capacity ranges
    public static class Capacities
    {
        public const double Perfect = 1.0;
        public const double Good = 0.8;
        public const double Moderate = 0.5;
        public const double Poor = 0.3;
        public const double Critical = 0.1;
        public const double Zero = 0.0;
    }

    // Damage amounts
    public static class Damage
    {
        public const double Light = 5.0;
        public const double Moderate = 15.0;
        public const double Heavy = 30.0;
        public const double Severe = 60.0;
        public const double Massive = 100.0;
    }
}
