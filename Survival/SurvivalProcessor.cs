// SurvivalProcessor.cs
using text_survival.Bodies;
using text_survival.Effects;

namespace text_survival.Survival;


public static class SurvivalProcessor
{
	private const double BASE_EXHAUSTION_RATE = 1;
	public const double MAX_ENERGY_MINUTES = 960.0;
	private const double BASE_DEHYDRATION_RATE = 4000.0 / (24.0 * 60.0);
	public const double MAX_HYDRATION = 4000.0;
	public const double MAX_CALORIES = 2000.0;

	private const double BaseBodyTemperature = 98.6;
	private const double SevereHypothermiaThreshold = 89.6;
	private const double HypothermiaThreshold = 95.0;
	private const double ShiveringThreshold = 97.0;
	private const double HyperthermiaThreshold = 100.0;
	private const double SweatingThreshold = 99.0;

	private const double MIN_FAT_PERCENT = 0.03;
	private const double MIN_MUSCLE_PERCENT = 0.15;
	private const double CALORIES_PER_KG_FAT = 7700;
	private const double CALORIES_PER_KG_MUSCLE = 1320;

	private const double REGEN_MIN_CALORIES_PERCENT = 0.10;
	private const double REGEN_MIN_HYDRATION_PERCENT = 0.10;
	private const double REGEN_MAX_ENERGY_PERCENT = 0.50;
	private const double BASE_HEALING_PER_HOUR = 0.1;

	private enum TemperatureStage { Warm, Cool, Cold, Freezing, Hot }

	/// <summary>
	/// Pure function - processes survival simulation and returns result.
	/// Does not mutate body or context.
	/// </summary>
	public static SurvivalProcessorResult Process(Body body, SurvivalContext context, int minutesElapsed)
	{
		var result = ProcessBaseNeeds(body, context, minutesElapsed);
		result.Combine(ProcessTemperature(body, context));

		// Project stats after delta to check consequences
		double projectedCalories = body.CalorieStore + result.StatsDelta.CalorieDelta;
		double projectedHydration = body.Hydration + result.StatsDelta.HydrationDelta;
		double projectedTemp = body.BodyTemperature + result.StatsDelta.TemperatureDelta;

		result.Combine(ProcessStarvation(body, projectedCalories, minutesElapsed));
		result.Combine(ProcessDehydration(projectedHydration, minutesElapsed));
		result.Combine(ProcessHypothermia(projectedTemp, minutesElapsed));
		result.Combine(ProcessRegeneration(body, projectedCalories, projectedHydration, minutesElapsed));
		result.Combine(ProcessWarningMessages(body, projectedCalories, projectedHydration));

		return result;
	}

	private static SurvivalProcessorResult ProcessBaseNeeds(Body body, SurvivalContext context, int minutesElapsed)
	{
		double currentMetabolism = GetCurrentMetabolism(body, context.ActivityLevel);
		double caloriesBurned = currentMetabolism / 24.0 / 60.0 * minutesElapsed;

		return new SurvivalProcessorResult
		{
			StatsDelta = new SurvivalStatsDelta
			{
				EnergyDelta = -(BASE_EXHAUSTION_RATE * minutesElapsed),
				HydrationDelta = -(BASE_DEHYDRATION_RATE * minutesElapsed),
				CalorieDelta = -caloriesBurned,
				TemperatureDelta = caloriesBurned / 24000.0,
			}
		};
	}

	private static SurvivalProcessorResult ProcessTemperature(Body body, SurvivalContext context)
	{
		double coldResistance = AbilityCalculator.CalculateColdResistance(body);
		double naturalInsulation = Math.Clamp(coldResistance, 0, 1);
		double totalInsulation = Math.Clamp(naturalInsulation + context.ClothingInsulation, 0, 0.95);

		double skinTemp = body.BodyTemperature - 8.4;
		double tempDifferential = context.LocationTemperature - skinTemp;
		double insulatedDiff = tempDifferential * (1 - totalInsulation);

		double tempDiffMagnitude = Math.Abs(insulatedDiff);
		double baseRate = 1.0 / 120.0;
		double exponentialFactor = 1.0 + (tempDiffMagnitude / 40.0);
		double rate = baseRate * exponentialFactor;

		return new SurvivalProcessorResult
		{
			StatsDelta = new SurvivalStatsDelta
			{
				TemperatureDelta = insulatedDiff * rate,
			},
			Effects = GetTemperatureEffects(body),
		};
	}

	private static SurvivalProcessorResult ProcessStarvation(Body body, double projectedCalories, int minutesElapsed)
	{
		if (projectedCalories >= 0)
			return new SurvivalProcessorResult();

		var result = new SurvivalProcessorResult();
		double deficit = Math.Abs(projectedCalories);

		result.StatsDelta.CalorieDelta = deficit;

		// Calculate available fat
		double minFat = MIN_FAT_PERCENT * body.Weight;
		double availableFat = Math.Max(0, body.BodyFatKG - minFat);
		double caloriesFromFat = availableFat * CALORIES_PER_KG_FAT;

		if (caloriesFromFat >= deficit)
		{
			result.FatToConsume = deficit / CALORIES_PER_KG_FAT;

			if (body.BodyFatPercentage < 0.08)
				result.Messages.Add("Your body is consuming the last of your fat reserves... You're becoming dangerously thin.");
			else if (body.BodyFatPercentage < 0.12)
				result.Messages.Add("Your body is burning fat reserves. You're noticeably thinner.");

			return result;
		}

		// Burn all available fat
		result.FatToConsume = availableFat;
		deficit -= caloriesFromFat;

		if (availableFat > 0)
			result.Messages.Add("Your body has exhausted all available fat reserves!");

		// Calculate available muscle
		double minMuscle = MIN_MUSCLE_PERCENT * body.Weight;
		double availableMuscle = Math.Max(0, body.MuscleKG - minMuscle);
		double caloriesFromMuscle = availableMuscle * CALORIES_PER_KG_MUSCLE;

		if (caloriesFromMuscle >= deficit)
		{
			result.MuscleToConsume = deficit / CALORIES_PER_KG_MUSCLE;

			if (body.MusclePercentage < 0.18)
				result.Messages.Add("Your body is cannibalizing muscle tissue! You feel extremely weak.");
			else if (body.MusclePercentage < 0.25)
				result.Messages.Add("Your muscles are wasting away. You're losing strength rapidly.");

			return result;
		}

		// Burn all available muscle
		result.MuscleToConsume = availableMuscle;
		deficit -= caloriesFromMuscle;

		if (availableMuscle > 0)
			result.Messages.Add("Your body has consumed almost all muscle tissue. Organ damage imminent!");

		// Organ damage - nothing left to burn
		if (deficit > 0)
		{
			double damagePerMinute = 0.1 / 60.0;
			double damage = damagePerMinute * minutesElapsed;

			var vitalOrgans = new[] { "Heart", "Liver", "Brain", "Lungs" };
			string target = vitalOrgans[Random.Shared.Next(vitalOrgans.Length)];

			result.DamageEvents.Add(new DamageInfo
			{
				Amount = damage,
				Type = DamageType.Internal,
				TargetPartName = target,
				Source = "Starvation"
			});
		}

		return result;
	}

	private static SurvivalProcessorResult ProcessDehydration(double projectedHydration, int minutesElapsed)
	{
		if (projectedHydration > 0)
			return new SurvivalProcessorResult();

		double damagePerMinute = 0.2 / 60.0;
		double damage = damagePerMinute * minutesElapsed;

		var affectedOrgans = new[] { "Brain", "Heart", "Liver" };
		string target = affectedOrgans[Random.Shared.Next(affectedOrgans.Length)];

		return new SurvivalProcessorResult
		{
			DamageEvents = [
				new DamageInfo
				{
					Amount = damage,
					Type = DamageType.Internal,
					TargetPartName = target,
					Source = "Dehydration"
				}
			],
			Messages = ["Your organs are failing from dehydration!"],
		};
	}

	private static SurvivalProcessorResult ProcessHypothermia(double projectedTemp, int minutesElapsed)
	{
		if (projectedTemp >= SevereHypothermiaThreshold)
			return new SurvivalProcessorResult();

		double severityFactor = Math.Min(1.0, (SevereHypothermiaThreshold - projectedTemp) / 50.0);
		double damagePerHour = 0.15 + (0.15 * severityFactor);
		double damage = (damagePerHour / 60.0) * minutesElapsed;

		var coreOrgans = new[] { "Heart", "Brain", "Lungs" };
		string target = coreOrgans[Random.Shared.Next(coreOrgans.Length)];

		return new SurvivalProcessorResult
		{
			DamageEvents = [
				new DamageInfo
				{
					Amount = damage,
					Type = DamageType.Internal,
					TargetPartName = target,
					Source = "Hypothermia"
				}
			],
			Messages = [$"Your core body temperature is dangerously low ({projectedTemp:F1}°F)... Your organs are failing..."],
		};
	}

	private static SurvivalProcessorResult ProcessRegeneration(Body body, double projectedCalories, double projectedHydration, int minutesElapsed)
	{
		bool wellFed = projectedCalories > MAX_CALORIES * REGEN_MIN_CALORIES_PERCENT;
		bool hydrated = projectedHydration > MAX_HYDRATION * REGEN_MIN_HYDRATION_PERCENT;
		bool rested = body.Energy < MAX_ENERGY_MINUTES * REGEN_MAX_ENERGY_PERCENT;

		if (!wellFed || !hydrated || !rested || body.Health >= 1.0)
			return new SurvivalProcessorResult();

		double nutritionQuality = Math.Min(1.0, projectedCalories / MAX_CALORIES);
		double healingAmount = (BASE_HEALING_PER_HOUR / 60.0) * minutesElapsed * nutritionQuality;

		var result = new SurvivalProcessorResult
		{
			HealingEvents = [
				new HealingInfo
				{
					Amount = healingAmount,
					Type = "natural regeneration",
					Quality = nutritionQuality
				}
			],
		};

		if (Random.Shared.NextDouble() < 0.01)
			result.Messages.Add("Your body is slowly healing...");

		return result;
	}

	private static SurvivalProcessorResult ProcessWarningMessages(Body body, double projectedCalories, double projectedHydration)
	{
		var messages = new List<string>();

		double caloriePercent = projectedCalories / MAX_CALORIES;
		double hydrationPercent = projectedHydration / MAX_HYDRATION;
		double energyPercent = body.Energy / MAX_ENERGY_MINUTES;

		if (caloriePercent <= 0.01 && Utils.DetermineSuccess(0.1))
			messages.Add("You are starving to death!");
		else if (caloriePercent <= 0.20 && Utils.DetermineSuccess(0.05))
			messages.Add("You're desperately hungry.");
		else if (caloriePercent <= 0.50 && Utils.DetermineSuccess(0.02))
			messages.Add("You're getting very hungry.");

		if (hydrationPercent <= 0.01 && Utils.DetermineSuccess(0.1))
			messages.Add("You are dying of thirst!");
		else if (hydrationPercent <= 0.20 && Utils.DetermineSuccess(0.05))
			messages.Add("You're desperately thirsty.");
		else if (hydrationPercent <= 0.50 && Utils.DetermineSuccess(0.02))
			messages.Add("You're getting quite thirsty.");

		if (energyPercent <= 0.01 && Utils.DetermineSuccess(0.1))
			messages.Add("You're so exhausted you can barely stay awake.");
		else if (energyPercent <= 0.20 && Utils.DetermineSuccess(0.05))
			messages.Add("You're extremely tired.");
		else if (energyPercent <= 0.50 && Utils.DetermineSuccess(0.02))
			messages.Add("You're getting tired.");

		return new SurvivalProcessorResult { Messages = messages };
	}

	public static double GetCurrentMetabolism(Body body, double activityLevel)
	{
		double bmr = 370 + (21.6 * body.MuscleKG) + (6.17 * body.BodyFatKG);
		bmr *= 0.7 + (0.3 * body.Health);
		return bmr * activityLevel;
	}
	private static List<Effect> GetTemperatureEffects(Body body)
	{
		List<Effect> effects = [];
		var stage = GetTemperatureStage(body.BodyTemperature);

		if (stage == TemperatureStage.Cold || stage == TemperatureStage.Freezing)
			effects.AddRange(GetColdEffects(body));
		else if (stage == TemperatureStage.Hot)
		{
			double severity = Math.Clamp((body.BodyTemperature - HyperthermiaThreshold) / 10.0, 0.01, 1.0);
			effects.Add(EffectFactory.Hyperthermia(severity));
		}

		if (body.BodyTemperature > SweatingThreshold)
		{
			double severity = Math.Clamp((body.BodyTemperature - SweatingThreshold) / 4.0, 0.10, 1.0);
			effects.Add(EffectFactory.Sweating(severity));
		}

		return effects;
	}

	private static List<Effect> GetColdEffects(Body body)
	{
		List<Effect> effects = [];

		if (body.BodyTemperature < ShiveringThreshold)
		{
			double intensity = Math.Clamp((ShiveringThreshold - body.BodyTemperature) / 5.0, 0.01, 1.0);
			effects.Add(EffectFactory.Shivering(intensity));
		}

		if (body.BodyTemperature < HypothermiaThreshold)
		{
			double severity = Math.Clamp((HypothermiaThreshold - body.BodyTemperature) / 10.0, 0.01, 1.0);
			effects.Add(EffectFactory.Hypothermia(severity));
		}

		if (body.BodyTemperature < SevereHypothermiaThreshold)
		{
			double severity = Math.Clamp((SevereHypothermiaThreshold - body.BodyTemperature) / 10.0, 0.01, 1.0);
			foreach (var extremity in new[] { "Left Arm", "Right Arm", "Left Leg", "Right Leg" })
			{
				effects.Add(EffectFactory.Frostbite(extremity, severity));
			}
		}

		return effects;
	}

	private static TemperatureStage GetTemperatureStage(double temperature)
	{
		if (temperature < SevereHypothermiaThreshold) return TemperatureStage.Freezing;
		if (temperature < HypothermiaThreshold) return TemperatureStage.Cold;
		if (temperature < BaseBodyTemperature) return TemperatureStage.Cool;
		if (temperature <= HyperthermiaThreshold) return TemperatureStage.Warm;
		return TemperatureStage.Hot;
	}

	public static SurvivalProcessorResult Sleep(Body body, int minutes)
	{
		return new SurvivalProcessorResult
		{
			StatsDelta = new SurvivalStatsDelta
			{
				EnergyDelta = BASE_EXHAUSTION_RATE * 2 * minutes,
				HydrationDelta = -BASE_DEHYDRATION_RATE * 0.7 * minutes,
				CalorieDelta = -GetCurrentMetabolism(body, .5) / 24.0 / 60.0 * minutes,
			}
		};
	}
}