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
	public const double HypothermiaThreshold = 95.0;
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

	private const double ThermalMassFactorFPerKg = 2.0;  // °F capacity per kg of clothing

	private enum TemperatureStage { Warm, Cool, Cold, Freezing, Hot }

	public static SurvivalProcessorResult Process(Body body, SurvivalContext context, int minutesElapsed)
	{
		var result = ProcessBaseNeeds(body, context, minutesElapsed);
		result.Combine(ProcessTemperature(body, context, minutesElapsed));
		result.Combine(ProcessWetness(context, minutesElapsed));
		result.Combine(ProcessBloody(context, minutesElapsed));

		// Project stats after delta to check consequences
		double projectedCalories = body.CalorieStore + result.StatsDelta.CalorieDelta;
		double projectedHydration = body.Hydration + result.StatsDelta.HydrationDelta;
		double projectedTemp = body.BodyTemperature + result.StatsDelta.TemperatureDelta;

		result.Combine(ProcessStarvation(body, projectedCalories, minutesElapsed));
		result.Combine(ProcessDehydration(projectedHydration, minutesElapsed));
		result.Combine(ProcessHypothermia(projectedTemp, minutesElapsed));
		result.Combine(ProcessRegeneration(body, projectedCalories, projectedHydration, minutesElapsed));
		result.Combine(ProcessWarningMessages(body, projectedCalories, projectedHydration));

		double projectedEnergy = body.Energy + result.StatsDelta.EnergyDelta;
		result.Combine(ProcessSurvivalEffects(projectedCalories, projectedHydration, projectedEnergy));

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
				TemperatureDelta = 0 // caloriesBurned / 24000.0, - handled in ProcessTemperature
			}
		};
	}

	/// <summary>
	/// Calculate temperature change per hour for given conditions.
	/// Positive = warming, negative = cooling. Units: °F/hour.
	/// </summary>
	public static double CalculateTemperatureChangePerHour(Body body, SurvivalContext context)
	{
		// heat_capacity = mass * specific heat
		// dT/dt = (heat_in - heat_out) / heat_capacity
		// Q_loss = h * surface_area * deltaT * (1 - insulation) | h = heat transfer coef: air -> 7, wind -> 20, water -> 400
		// Human body: surface_area = 1.8m^2, specific heat = 3.5 J/KG*C or .83 kcal/kg*F

		double specificHeat = 0.83; // for calories in F
		double surfaceArea = 1.8; // m^2
		double heatCapacity = body.WeightKG * specificHeat;

		// Clothing provides wind protection proportional to insulation
		double clothingWindProtection = context.ClothingInsulation * 0.5;
		double effectiveWind = context.WindSpeedLevel * (1 - clothingWindProtection);
		double windFactor = 1.0 + (effectiveWind * 2.0);
		double h = 5.0 * windFactor;

		double coldResistance = AbilityCalculator.CalculateColdResistance(body);
		double naturalInsulation = Math.Clamp(coldResistance, 0, 1);
		double totalInsulation = Math.Clamp(naturalInsulation + context.ClothingInsulation, 0, 0.95);

		double skinTemp = body.BodyTemperature - 8.4;
		double effectiveTemp = context.LocationTemperature + context.FireProximityBonus;
		double tempDifferential = skinTemp - effectiveTemp;
		double deltaT = tempDifferential * (5.0 / 9.0);

		double heatLossW = h * surfaceArea * deltaT * (1 - totalInsulation);
		double heatLossHr = heatLossW * 0.86;
		double heatGainHr = GetCurrentMetabolism(body, context.ActivityLevel) / 24;

		double netHeatHr = heatGainHr - heatLossHr;
		return netHeatHr / heatCapacity; // °F/hr
	}

	private static SurvivalProcessorResult ProcessTemperature(Body body, SurvivalContext context, int minutes)
	{
		double tempChange = CalculateTemperatureChangePerHour(body, context);

		// Clothing thermal mass buffer
		double clothingCapacityF = context.ClothingWeightKg * ThermalMassFactorFPerKg;
		double bufferDelta = 0;
		double bodyTempDelta = tempChange / 60 * minutes;  // Original calculation

		if (clothingCapacityF > 0)
		{
			if (bodyTempDelta < 0)  // COOLING
			{
				if (context.ClothingHeatBuffer > 0)
				{
					// Buffer absorbs cooling first
					double lossF = Math.Abs(bodyTempDelta);
					double bufferHeatF = context.ClothingHeatBuffer * clothingCapacityF;

					if (bufferHeatF >= lossF)
					{
						bufferDelta = -lossF / clothingCapacityF;
						bodyTempDelta = 0;
					}
					else
					{
						bufferDelta = -context.ClothingHeatBuffer;
						bodyTempDelta = -(lossF - bufferHeatF);
					}
				}
				// else: buffer empty, normal cooling
			}
			else if (context.FireProximityBonus > 0 && context.ClothingHeatBuffer < 1.0)
			{
				// NEAR FIRE: Fill buffer based on fire intensity
				// Physics: 6.0 * FireProximityBonus kcal/hr heat transfer, 2% efficiency
				double fillRate = (context.FireProximityBonus / 200.0) / clothingCapacityF;
				bufferDelta = Math.Min(fillRate * minutes, 1.0 - context.ClothingHeatBuffer);
				// bodyTempDelta unchanged (fire already reduces heat loss)
			}
			else if (bodyTempDelta > 0)  // Actually gaining heat (rare)
			{
				if (context.ClothingHeatBuffer < 1.0)
				{
					// Split 50/50 between body and buffer
					double halfGain = bodyTempDelta / 2;
					double spaceF = (1.0 - context.ClothingHeatBuffer) * clothingCapacityF;
					double toBufferF = Math.Min(halfGain, spaceF);

					bufferDelta = toBufferF / clothingCapacityF;
					bodyTempDelta = halfGain + (halfGain - toBufferF);  // body gets half + overflow
				}
				// else: buffer full, all heat to body
			}
		}

		return new SurvivalProcessorResult
		{
			StatsDelta = new SurvivalStatsDelta
			{
				TemperatureDelta = bodyTempDelta,
			},
			ClothingHeatBufferDelta = bufferDelta,
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
		double minFat = MIN_FAT_PERCENT * body.WeightKG;
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
		double minMuscle = MIN_MUSCLE_PERCENT * body.WeightKG;
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
			double damagePerMinute = 0.5 / 60.0;  // 0.5/hour = death in ~10 hours
			double damage = damagePerMinute * minutesElapsed;

			var vitalOrgans = new[] { BodyTarget.Heart, BodyTarget.Liver, BodyTarget.Brain, BodyTarget.Lungs };
			BodyTarget target = vitalOrgans[Random.Shared.Next(vitalOrgans.Length)];

			result.DamageEvents.Add(new DamageInfo(damage, DamageType.Internal, target));
		}

		return result;
	}

	private static SurvivalProcessorResult ProcessDehydration(double projectedHydration, int minutesElapsed)
	{
		if (projectedHydration > 0)
			return new SurvivalProcessorResult();

		double damagePerMinute = 1.0 / 60.0;  // 1.0/hour = death in ~5 hours
		double damage = damagePerMinute * minutesElapsed;

		var affectedOrgans = new[] { BodyTarget.Brain, BodyTarget.Heart, BodyTarget.Liver };
		BodyTarget target = affectedOrgans[Random.Shared.Next(affectedOrgans.Length)];

		return new SurvivalProcessorResult
		{
			DamageEvents = [
				new DamageInfo(damage, DamageType.Internal, target)
			],
			Messages = ["Your organs are failing from dehydration!"],
		};
	}

	private static SurvivalProcessorResult ProcessHypothermia(double projectedTemp, int minutesElapsed)
	{
		if (projectedTemp >= HypothermiaThreshold)
			return new SurvivalProcessorResult();

		// Severity reaches 100% at ~80°F (realistic lethal threshold)
		double severityFactor = Math.Min(1.0, (HypothermiaThreshold - projectedTemp) / 15.0);

		// Damage scales more aggressively at high severity
		// Low severity (just below 95°F): ~0.5/hour
		// High severity (~80°F): ~8/hour = death in ~45 minutes
		double damagePerHour = severityFactor < 0.5
			? 0.5 + (1.0 * severityFactor)              // 0.5-1.0/hour for mild
			: 1.0 + (14.0 * (severityFactor - 0.5));    // 1.0-8.0/hour for severe
		double damage = (damagePerHour / 60.0) * minutesElapsed;

		var coreOrgans = new[] { BodyTarget.Heart, BodyTarget.Brain, BodyTarget.Lungs };
		BodyTarget target = coreOrgans[Random.Shared.Next(coreOrgans.Length)];

		// Escalating message based on severity - always shown so player knows they're dying
		string urgency = severityFactor switch
		{
			>= 0.7 => "Your organs are shutting down!",
			>= 0.4 => "You're dying from the cold...",
			_ => "Your core is dangerously cold..."
		};

		return new SurvivalProcessorResult
		{
			DamageEvents = [
				new DamageInfo(damage, DamageType.Internal, target)
			],
			Messages = [urgency],
		};
	}

	private static SurvivalProcessorResult ProcessRegeneration(Body body, double projectedCalories, double projectedHydration, int minutesElapsed)
	{
		bool wellFed = projectedCalories > MAX_CALORIES * REGEN_MIN_CALORIES_PERCENT;
		bool hydrated = projectedHydration > MAX_HYDRATION * REGEN_MIN_HYDRATION_PERCENT;
		bool rested = body.Energy < MAX_ENERGY_MINUTES * REGEN_MAX_ENERGY_PERCENT;

		// Check if any body parts or blood need healing
		bool fullyHealed = body.Parts.All(p => p.Condition >= 1.0) &&
						   body.Parts.SelectMany(p => p.Organs).All(o => o.Condition >= 1.0);
		bool bloodFull = body.Blood.Condition >= 1.0;

		if (!wellFed || !hydrated || !rested || (fullyHealed && bloodFull))
			return new SurvivalProcessorResult();

		// Digestion capacity affects how well nutrients support healing
		var capacities = CapacityCalculator.GetCapacities(body, new CapacityModifierContainer());
		double digestionQuality = capacities.Digestion;

		double nutritionQuality = Math.Min(1.0, projectedCalories / MAX_CALORIES);
		double healingAmount = (BASE_HEALING_PER_HOUR / 60.0) * minutesElapsed * nutritionQuality * digestionQuality;

		var result = new SurvivalProcessorResult();

		// Blood regenerates slowly when well-fed, hydrated, rested (at half rate of tissue healing)
		if (!bloodFull)
		{
			result.BloodHealing = healingAmount * 0.5;
		}

		// Body part healing
		if (!fullyHealed)
		{
			result.HealingEvents.Add(new HealingInfo
			{
				Amount = healingAmount,
				Type = "natural regeneration",
				Quality = nutritionQuality
			});
		}

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

	private const double SURVIVAL_EFFECT_THRESHOLD = 0.30;

	private static SurvivalProcessorResult ProcessSurvivalEffects(
		double projectedCalories, double projectedHydration, double projectedEnergy)
	{
		var effects = new List<Effect>();

		double caloriePercent = Math.Clamp(projectedCalories / MAX_CALORIES, 0, 1);
		double hydrationPercent = Math.Clamp(projectedHydration / MAX_HYDRATION, 0, 1);
		double energyPercent = Math.Clamp(projectedEnergy / MAX_ENERGY_MINUTES, 0, 1);

		// Hungry effect - below 30% calories
		if (caloriePercent < SURVIVAL_EFFECT_THRESHOLD)
		{
			double severity = (SURVIVAL_EFFECT_THRESHOLD - caloriePercent) / SURVIVAL_EFFECT_THRESHOLD;
			effects.Add(EffectFactory.Hungry(severity));
		}

		// Thirsty effect - below 30% hydration
		if (hydrationPercent < SURVIVAL_EFFECT_THRESHOLD)
		{
			double severity = (SURVIVAL_EFFECT_THRESHOLD - hydrationPercent) / SURVIVAL_EFFECT_THRESHOLD;
			effects.Add(EffectFactory.Thirsty(severity));
		}

		// Tired effect - below 30% energy
		if (energyPercent < SURVIVAL_EFFECT_THRESHOLD)
		{
			double severity = (SURVIVAL_EFFECT_THRESHOLD - energyPercent) / SURVIVAL_EFFECT_THRESHOLD;
			effects.Add(EffectFactory.Tired(severity));
		}

		return new SurvivalProcessorResult { Effects = effects };
	}

	public static double GetCurrentMetabolism(Body body, double activityLevel)
	{
		double bmr = 370 + (21.6 * body.MuscleKG) + (6.17 * body.BodyFatKG);
		// Organ condition affects metabolism - damaged organs = less efficient
		double organCondition = body.Parts.SelectMany(p => p.Organs).Average(o => o.Condition);
		bmr *= 0.7 + (0.3 * organCondition);
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
			// Single consolidated frostbite effect with escalating messages
			effects.Add(EffectFactory.Frostbite(severity));
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

	private static double CalculateDryingRate(SurvivalContext context)
	{
		double baseRate = 0;

		if (context.FireProximityBonus > 0)
		{
			// Near fire: 2-5/hr (dry in 12-30 min)
			baseRate = 2.0 + (context.FireProximityBonus / 5.0);
		}
		else if (context.LocationTemperature > 32)
		{
			// Above freezing: slow natural drying
			baseRate = Math.Max(0, (context.LocationTemperature - 32) / 20.0);
		}
		// else: below freezing = 0 (clothes freeze wet)

		// Wind accelerates drying (but not during active precipitation)
		double windBonus = 0;
		if (!context.IsRaining && !context.IsSnowing && !context.IsBlizzard)
		{
			windBonus = context.WindSpeedLevel;
		}
		baseRate += windBonus;

		return baseRate;
	}

	private static SurvivalProcessorResult ProcessWetness(SurvivalContext context, int minutesElapsed)
	{
		var result = new SurvivalProcessorResult();

		// Calculate wetness accumulation per minute
		double wetnessDelta = 0;
		double exposureFactor = 1 - context.OverheadCoverLevel;

		// Apply waterproofing reduction (resin-treated equipment)
		double waterproofReduction = 1 - context.WaterproofingLevel;

		if (exposureFactor > 0)
		{
			if (context.IsRaining)
				wetnessDelta = 0.01 * context.PrecipitationPct * exposureFactor * waterproofReduction;
			else if (context.IsBlizzard)
				wetnessDelta = 0.005 * context.PrecipitationPct * exposureFactor * waterproofReduction;
			else if (context.IsSnowing)
				wetnessDelta = 0.003 * context.PrecipitationPct * exposureFactor * waterproofReduction;
		}

		// Calculate drying (reduction in wetness per minute)
		double dryingRate = CalculateDryingRate(context);
		double dryingDelta = (dryingRate / 60.0) * minutesElapsed; // Convert hourly rate to per-minute

		// Calculate new severity (accumulation - drying)
		double newSeverity = Math.Clamp(
			context.CurrentWetnessPct + wetnessDelta * minutesElapsed - dryingDelta,
			0, 1);

		// Create/update effect when wetness reaches 5%
		if (newSeverity >= 0.05)
		{
			result.Effects.Add(EffectFactory.Wet(newSeverity));
		}

		return result;
	}

	private static SurvivalProcessorResult ProcessBloody(SurvivalContext context, int minutesElapsed)
	{
		var result = new SurvivalProcessorResult();

		// No bleeding = no accumulation (let natural decay handle existing bloody)
		if (context.CurrentBleedingPct <= 0)
			return result;

		// Accumulation rate: +0.15/hour at full bleeding severity
		const double ACCUMULATION_RATE_PER_HOUR = 0.15;
		double accumulationPerMinute = ACCUMULATION_RATE_PER_HOUR / 60.0;
		double bloodyDelta = accumulationPerMinute * context.CurrentBleedingPct * minutesElapsed;

		// Calculate new severity
		double newSeverity = Math.Clamp(context.CurrentBloodyPct + bloodyDelta, 0, 1);

		// Create/update effect only when bloody reaches 5%
		if (newSeverity >= 0.05)
		{
			result.Effects.Add(EffectFactory.Bloody(newSeverity));
		}

		return result;
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