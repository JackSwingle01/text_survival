namespace text_survival
{
    public static class Physics
    {
        public static float DeltaCelsiusToDeltaFahrenheit(float celsius)
        {
            return (celsius * (9.0F / 5.0F));
        }
        public static float TempChange(float mass, float specificHeat, float joules)
        {
            float deltaT = joules / (mass * specificHeat);
            return deltaT;
        }
        public static float CaloriesToJoules(float calories)
        {
            return calories * 4184.0F;
        }

    }
}
