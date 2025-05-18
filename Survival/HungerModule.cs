
using text_survival.Bodies;
using text_survival.IO;

namespace text_survival.Survival
{
	public class HungerModule
	{
		private const double MAX_CALORIES = 2000.0; // Maximum calories stored before fat conversion
		private const double CALORIES_PER_KG_FAT = 7700.0; // Standard calories stored in 1kg of fat
		private const double CALORIES_PER_KG_MUSCLE = 5500.0; // Calories in 1kg of muscle (less than fat)

		public bool IsStarving => CurrentCalories <= 0;
		private double CurrentCalories { get; set; }
		private Body body;
		public HungerModule(Body body)
		{
			CurrentCalories = 0;
			this.body = body;
		}

		public void Update()
		{
			// todo, actually update with activity level
			UpdateMetabolism(2);
		}

		private void UpdateMetabolism(double activityLevel)
		{
			// todo have this account for temp too
			// Calculate calorie burn based on BMR, activity, and time
			double hourlyBurn = body.CalculateMetabolicRate() / 24.0 * activityLevel;
			double calories = hourlyBurn / 60;  //* timePassed.TotalHours;


			CurrentCalories -= calories;
			if (CurrentCalories < 0)
			{
				// If calories are negative, burn fat
				double excessCalories = -CurrentCalories;
				CurrentCalories = 0;

				double fatBurnRate = excessCalories / 7700.0; // ~7700 calories per kg of fat
				body.BodyFat -= fatBurnRate;

				// If completely out of fat, burn muscle
				if (body.BodyFat <= 0 && body.Muscle > 0)
				{
					double muscleBurnRate = excessCalories / 7700.0 * 1.2; // Muscle burns less efficiently
					body.Muscle -= muscleBurnRate;
				}
			}
		}

		public void AddCalories(double calories)
		{
			CurrentCalories += calories;

			if (CurrentCalories > MAX_CALORIES)
			{
				double excessCalories = CurrentCalories - MAX_CALORIES;
				double fatGain = excessCalories / CALORIES_PER_KG_FAT;

				body.BodyFat += fatGain;
				CurrentCalories = MAX_CALORIES;

				int gFatGain = (int)(fatGain * 1000);
				Output.WriteLine($"You gain {fatGain}g of body fat from excess calories.");
			}
		}

		public void Describe()
		{
			double percent = (int)((CurrentCalories / MAX_CALORIES) * 100);
			Output.WriteLine("Hunger: ", percent, "%");
		}
	}
}


