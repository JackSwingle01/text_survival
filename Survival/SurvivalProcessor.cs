using System.Net.Http.Headers;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.IO;

namespace text_survival.Survival;



public static class SurvivalProcessor
{
	private const double BASE_EXHAUSTION_RATE = 1;
	private const double MAX_EXHAUSTION_MINUTES = 960.0F; // minutes (16 hours)
	private const double BASE_DEHYDRATION_RATE = 4000F / (24F * 60F); // mL per minute
	private const double MAX_HYDRATION = 4000.0F; // mL
	private const double MAX_CALORIES = 2000.0; // Maximum calories stored before fat conversion

	private const double BaseBodyTemperature = 98.6F;
	private const double SevereHypothermiaThreshold = 89.6; // °F
	private const double HypothermiaThreshold = 95.0;  // °F
	private const double ShiveringThreshold = 97.0; // °F
	private const double HyperthermiaThreshold = 100.0; // °F  
	private const double SweatingThreshold = 99.00; // °F

	private enum TemperatureEnum
	{
		Warm,
		Cool,
		Cold,
		Freezing,
		Hot,
	}


	public static SurvivalProcessorResult Process(SurvivalData data, int minutesElapsed, List<Effect> activeEffects)
	{
		data.Exhaustion = Math.Min(1, data.Exhaustion + (BASE_EXHAUSTION_RATE * minutesElapsed));
		data.Hydration = Math.Max(0, data.Hydration - (BASE_DEHYDRATION_RATE * minutesElapsed));

		// Hunger update
		// todo, actually update with activity level
		// todo have this account for temp too
		double currentMetabolism = GetCurrentMetabolism(data);
		double caloriesBurned = currentMetabolism / 24 / 60 * minutesElapsed;
		bool wasStarving = data.Calories <= 0;
		data.Calories -= caloriesBurned;

		data.Temperature += caloriesBurned / 24000;

		if (data.Calories <= 0)
		{
			double excessCalories = -data.Calories;
			data.Calories = 0;
			// EventBus.Publish(new StarvingEvent(owner, excessCalories, isNew: !wasStarving));
		}
		//else if (wasStarving) // wasStarving but is no longer // TODO this will never be hit anymore, move to eat method
		//{
		//EventBus.Publish(new StoppedStarvingEvent(owner));
		//}

		// Temperature Update
		TemperatureEnum oldTemperature = GetTemperatureEnum(data.Temperature); // todo update message when temp changes
		double naturalInsulation = Math.Clamp(data.ColdResistance, 0, 1); // 0-1
		double totalInsulation = Math.Clamp(naturalInsulation + data.equipmentInsulation, 0, 0.95);

		double skinTemp = data.Temperature - 8.4;
		double tempDifferential = data.environmentalTemp - skinTemp;
		double insulatedDiff = tempDifferential * (1 - totalInsulation);
		double tempDiffMagnitude = Math.Abs(insulatedDiff);
		double baseRate = 1.0 / 120.0;
		double exponentialFactor = 1.0 + (tempDiffMagnitude / 40.0);
		double rate = baseRate * exponentialFactor;

		// double surfaceAreaFactor = Math.Pow(body.Weight / 70.0, -0.2);

		double tempChange = insulatedDiff * rate;
		data.Temperature += tempChange;
		SurvivalProcessorResult result = new(data);

		HandleActiveEffects(data, activeEffects);

		AddTemperatureEffects(data, oldTemperature, result);

		return result;
	}

	private static double GetCurrentMetabolism(SurvivalData data)
	{
		// Base BMR uses the Harris-Benedict equation (simplified)
		double bmr = 370 + (21.6 * data.MuscleWeight) + (6.17 * data.FatWeight); // bigger creature more calories
		bmr *= 0.7 + (0.3 * data.HealthPercent); // Injured bodies need more energy to heal
		return bmr * data.activityLevel;
	}


	private static void AddTemperatureEffects(SurvivalData data, TemperatureEnum oldTemperature, SurvivalProcessorResult result)
	{
		List<Effect> effects = [];
		TemperatureEnum temperatureStage = GetTemperatureEnum(data.Temperature);

		// Handle cold effects
		if (temperatureStage == TemperatureEnum.Cold || temperatureStage == TemperatureEnum.Freezing)
		{
			bool tempChanged = oldTemperature == temperatureStage;
			GenerateColdEffects(data, tempChanged, result);
		}

		else if (temperatureStage == TemperatureEnum.Hot)
		{
			double severity = Math.Clamp((data.Temperature - HyperthermiaThreshold) / 10.0, 0.01, 1.00);

			// Apply to whole body (will handle stacking through EffectRegistry)
			var hyperthermia = EffectBuilderExtensions
				.CreateEffect("Heat Exposure")
				.Temperature(TemperatureType.Hyperthermia)
				.WithSeverity(severity)
				.Build(); // todo add messages
			effects.Add(hyperthermia);
		}

		if (data.Temperature > SweatingThreshold)
		{
			// Calculate severity based on temperature
			double severity = Math.Clamp((data.Temperature - SweatingThreshold) / 4.0, 0.10, 1.00);

			// Apply to whole body (will handle stacking through EffectRegistry)
			var sweatingEffect = EffectBuilderExtensions
				.CreateEffect("Sweating")
				.CausesDehydration(1000 / 60 * severity) // up to 1L per hour
				.WithSeverity(severity)
				.Build(); // todo add notification messages
			effects.Add(sweatingEffect);
		}

		result.Effects.AddRange(effects);
	}

	private static void HandleActiveEffects(SurvivalData data, List<Effect> effects)
	{
		SurvivalStatsUpdate update = new();
		foreach (Effect effect in effects)
		{
			var effectWithSeverity = effect.SurvivalStatsEffect.ApplyMultiplier(effect.Severity);
			update = update.Add(effectWithSeverity);
		}
		data.Calories += update.Calories;
		data.Hydration += update.Hydration;
		data.Temperature += update.Temperature;
		data.Exhaustion += update.Exhaustion;
	}


	private static void GenerateColdEffects(SurvivalData data, bool isNewTemperatureChange, SurvivalProcessorResult result)
	{
		// Generate cold messages
		if (isNewTemperatureChange)
		{
			result.Messages.Add("DANGER:{target} is cold!");
		}
		else if (Utils.DetermineSuccess(Config.NOTIFY_EXISTING_STATUS_CHANCE))
		{
			result.Messages.Add("WARNING:{target} is still cold.");
		}

		// Generate shivering effect if cold enough
		if (data.Temperature < ShiveringThreshold)
		{
			double intensity = (ShiveringThreshold - data.Temperature) / 5.0;
			intensity = Math.Clamp(intensity, 0.01, 1.0);

			var shiveringEffect = EffectBuilderExtensions
				.CreateEffect("Shivering")
				.WithSeverity(intensity)
				.ReducesCapacity(CapacityNames.Manipulation, .2)
				.AllowMultiple(false)
				.AffectsTemperature(3) // at highest rate increases 
				.WithHourlySeverityChange(-2) // resolves in 30 min by default
				.Build();
			result.Effects.Add(shiveringEffect);
		}

		// Generate hypothermia effect if cold enough
		if (data.Temperature < HypothermiaThreshold)
		{
			double severity = Math.Clamp((HypothermiaThreshold - data.Temperature) / 10.0, 0.01, 1.0);

			string applicationMessage;
			string removalMessage;
			if (data.IsPlayer)
			{
				applicationMessage = $"Your core is getting very cold, you feel like you're starting to get hypothermia... Severity = {severity}";
				removalMessage = $"You're warming up enough and starting to feel better, the hypothermia has passed...";
			}
			else
			{
				applicationMessage = $"DEBUG: {{target}} has hypothermia. Severity = {severity}";
				removalMessage = "DEBUG: {target} no longer has hypothermia.";
			}

			var hypothermia = EffectBuilderExtensions
				.CreateEffect("Hypothermia")
				.Temperature(TemperatureType.Hypothermia)
				.WithApplyMessage(applicationMessage)
				.WithSeverity(severity)
				.AllowMultiple(false)
				.WithRemoveMessage(removalMessage)
				.Build();

			result.Effects.Add(hypothermia);
		}

		// Generate frostbite effects if severely cold
		if (data.Temperature < SevereHypothermiaThreshold)
		{
			// Note: This creates frostbite effects for arms and legs
			// The caller will need to apply these to the appropriate body parts
			var extremityNames = new[] { "Left Arm", "Right Arm", "Left Leg", "Right Leg" };

			foreach (var extremityName in extremityNames)
			{
				double severity = Math.Clamp((SevereHypothermiaThreshold - data.Temperature) / 5.0, 0.01, 1.0);

				string applicationMessage;
				string removalMessage;

				if (data.IsPlayer)
				{
					applicationMessage = $"Your {extremityName.ToLower()} is getting dangerously cold, you're developing frostbite! Severity = {severity}";
					removalMessage = $"The feeling is returning to your {extremityName.ToLower()}, the frostbite is healing...";
				}
				else
				{
					applicationMessage = $"DEBUG: {{target}} has frostbite on {extremityName}. Severity = {severity}";
					removalMessage = $"DEBUG: {{target}} no longer has frostbite on {extremityName}.";
				}

				var frostbite = EffectBuilderExtensions
					.CreateEffect("Frostbite")
					.Temperature(TemperatureType.Frostbite)
					.WithApplyMessage(applicationMessage)
					.WithSeverity(severity)
					.AllowMultiple(true)
					.WithRemoveMessage(removalMessage)
					.Targeting(extremityName)
					.Build();

				result.Effects.Add(frostbite);
			}
		}
	}


	private static TemperatureEnum GetTemperatureEnum(double temperature)
	{
		if (temperature < SevereHypothermiaThreshold)
		{
			return TemperatureEnum.Freezing;
		}
		else if (temperature >= SevereHypothermiaThreshold && temperature < HypothermiaThreshold)
		{
			return TemperatureEnum.Cold;
		}
		else if (temperature >= HypothermiaThreshold && temperature < BaseBodyTemperature)
		{
			return TemperatureEnum.Cool;
		}
		else if (temperature >= BaseBodyTemperature && temperature <= HyperthermiaThreshold)
		{
			return TemperatureEnum.Warm;
		}
		else // temperature > HyperthermiaThreshold
		{
			return TemperatureEnum.Hot;
		}
	}

	public static SurvivalProcessorResult Sleep(SurvivalData data, int minutes)
	{
		// starve at 1/2 rate - handled in GetCurrentMetabolism
		data.activityLevel = .5;
		// rest restores exhaustion at 2x the rate that you gain it while awake, so 16 hours of wakefulness creates only 8 hours of sleep debt
		data.Exhaustion = Math.Max(0, data.Exhaustion - (BASE_EXHAUSTION_RATE * 2 * minutes));
		data.Hydration = Math.Max(0, data.Hydration - (BASE_DEHYDRATION_RATE * .7 * minutes)); // dehydrate at reduced rate while asleep
		data.Calories = data.Calories -= GetCurrentMetabolism(data) / 24 / 60 * minutes;
		return new SurvivalProcessorResult(data);
	}

	public static void Describe(SurvivalData data)
	{
		Output.WriteLine("\n----------------------------------------------------\n| Survival Stats:");
		// calories
		double percent = (int)(data.Calories / MAX_CALORIES * 100);
		Output.WriteLine("| Calorie Store: ", percent, "%");
		// hydration
		percent = (int)((data.Hydration / MAX_HYDRATION) * 100);
		Output.WriteLine("| Hydration: ", percent, "%");
		// exhaustion
		percent = (int)((data.Exhaustion / MAX_EXHAUSTION_MINUTES) * 100);
		Output.WriteLine("| Exhaustion: ", percent, "%");
		// temp
		//string tempChange = IsWarming ? "Warming up" : "Getting colder";
		Output.WriteLine("| Body Temperature: ", data.Temperature, "°F");//(", TemperatureEffect, "), ", tempChange);
		Output.WriteLine("----------------------------------------------------");
	}
}
