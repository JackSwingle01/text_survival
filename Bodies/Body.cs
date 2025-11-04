
using text_survival.Effects;
using text_survival.IO;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.Bodies;

/// <summary>
/// External context that the body needs to update
/// </summary>
public class SurvivalContext
{
    public double LocationTemperature;
    public double ClothingInsulation;
    public double ActivityLevel;
}

public class Body
{
    // Root part and core properties
    public readonly bool IsPlayer = false;
    public readonly List<BodyRegion> Parts;
    public double Health => CalculateOverallHealth();

    private double CalculateOverallHealth()
    {
        // simple avg for now
        double health = Parts.Average(p => p.Condition);
        health = Parts.SelectMany(p => p.Organs.Select(o => o.Condition)).ToList().Append(health).Min();
        return health;
    }


    public double MaxHealth => 1;
    public bool IsDestroyed => Health <= 0;

    public bool IsTired => Energy > 60; // can sleep for at least 1 hr

    public readonly EffectRegistry EffectRegistry;

    private readonly double _baseWeight;

    public Body(string ownerName, BodyCreationInfo stats, EffectRegistry effectRegistry)
    {
        OwnerName = ownerName;
        IsPlayer = stats.IsPlayer;
        EffectRegistry = effectRegistry;
        Parts = BodyPartFactory.CreateBody(stats.type);

        // Initialize physical composition
        BodyFat = stats.overallWeight * stats.fatPercent;
        Muscle = stats.overallWeight * stats.musclePercent;
        _baseWeight = stats.overallWeight - BodyFat - Muscle;

        BodyTemperature = 98.6;
    }

    public double BodyFat;
    public double Muscle;

    public readonly string OwnerName;
    public double BodyFatPercentage => BodyFat / Weight;
    public double MusclePercentage => Muscle / Weight;
    public double Weight => _baseWeight + BodyFat + Muscle;
    public double BodyTemperature { get; set; }

    private double CalorieStore = 1500; // 75% of MAX_CALORIES (2000), up from 50% for better early-game survival
    private double Energy = 800;
    private double Hydration = 3000;

    // Body composition limits
    private const double MIN_FAT = 0.03;      // 3% essential fat (survival minimum)
    private const double MIN_MUSCLE = 0.15;   // 15% minimum muscle (critical weakness)

    // Calorie conversion rates (realistic)
    private const double CALORIES_PER_LB_FAT = 3500;     // Well-established
    private const double CALORIES_PER_LB_MUSCLE = 600;   // Protein catabolism
    private const double LB_TO_KG = 0.454;

    // Track time at critical levels for progressive damage
    private int _minutesStarving = 0;      // Time at 0% calories
    private int _minutesDehydrated = 0;    // Time at 0% hydration
    private int _minutesExhausted = 0;     // Time at 0% energy

    /// <summary>
    /// Damage application rules: 
    /// 1. Body.Damage() is the only way to apply damage 
    /// 2. Body handles all targeting resolution (string -> IBodyPart)
    /// 3. Body handles damage distribution and penetration logic
    /// 4. Effects should create Damage info and pass it here 
    /// </summary>
    public void Damage(DamageInfo damageInfo)
    {
        DamageProcessor.DamageBody(damageInfo, this);
    }


    public void Heal(HealingInfo healingInfo)
    {
        // Distribute healing across damaged parts
        if (healingInfo.TargetPart != null)
        {
            var targetPart = Parts.FirstOrDefault(p => p.Name == healingInfo.TargetPart);
            if (targetPart != null)
            {
                HealBodyPart(targetPart, healingInfo);
                return;
            }
        }

        // Heal most damaged parts first
        var damagedParts = Parts
            .Where(p => p.Condition < 1.0)
            .OrderBy(p => p.Condition)
            .ToList();

        if (damagedParts.Count > 0)
        {
            HealBodyPart(damagedParts[0], healingInfo);
        }
    }

    private static void HealBodyPart(BodyRegion part, HealingInfo healingInfo)
    {
        double healingAmount = healingInfo.Amount * healingInfo.Quality;

        // Heal materials first, then organs
        var materials = new[] { part.Skin, part.Muscle, part.Bone }.Where(m => m != null);
        foreach (var material in materials)
        {
            if (material!.Condition < 1.0 && healingAmount > 0)
            {
                double heal = Math.Min(healingAmount, (1.0 - material.Condition) * material.Toughness);
                material.Condition = Math.Min(1.0, material.Condition + heal / material.Toughness);
                healingAmount -= heal;
            }
        }

        // Heal organs
        foreach (var organ in part.Organs.Where(o => o.Condition < 1.0))
        {
            if (healingAmount > 0)
            {
                double heal = Math.Min(healingAmount, (1.0 - organ.Condition) * organ.Toughness);
                organ.Condition = Math.Min(1.0, organ.Condition + heal / organ.Toughness);
                healingAmount -= heal;
            }
        }
    }

    public void Update(TimeSpan timePassed, SurvivalContext context)
    {
        var data = BundleSurvivalData();
        data.environmentalTemp = context.LocationTemperature;
        data.ColdResistance = context.ClothingInsulation;
        data.activityLevel = context.ActivityLevel;

        var result = SurvivalProcessor.Process(data, (int)timePassed.TotalMinutes, EffectRegistry.GetAll());
        UpdateBodyBasedOnResult(result);
    }


    private void UpdateBodyBasedOnResult(SurvivalProcessorResult result)
    {
        var resultData = result.Data;
        BodyTemperature = resultData.Temperature;
        CalorieStore = resultData.Calories;
        Hydration = resultData.Hydration;
        Energy = resultData.Energy;

        result.Effects.ForEach(EffectRegistry.AddEffect);

        // Process survival consequences (starvation, dehydration, exhaustion)
        int minutesElapsed = 1; // Body.Update always called with 1 minute intervals
        ProcessSurvivalConsequences(result, minutesElapsed);

        foreach (string message in result.Messages)
        {
            string formattedMessage = message.Replace("{target}", OwnerName);
            Output.WriteLine(formattedMessage);
        }
    }

    // helper for baseline male human stats
    public static BodyCreationInfo BaselineHumanStats => new BodyCreationInfo
    {
        type = BodyTypes.Human,
        overallWeight = 75, // KG ~165 lbs
        fatPercent = .15, // pretty lean
        musclePercent = .30 // low end of athletic
    };
    public static BodyCreationInfo BaselinePlayerStats
    {
        get
        {
            var stats = BaselineHumanStats;
            stats.IsPlayer = true;
            return stats;
        }
    }
    public bool Rest(int minutes)
    {
        var data = BundleSurvivalData();
        data.activityLevel = .5; // half metabolism
        int minutesSlept = 0;
        var result = SurvivalProcessor.Sleep(data, minutes);
        UpdateBodyBasedOnResult(result);

        // just heal once at the end
        HealingInfo healing = new HealingInfo()
        {
            Amount = minutesSlept / 10,
            Type = "natural",
            Quality = Energy <= 0 ? 1 : .7, // healing quality is better after a full night's sleep
        };
        Heal(healing);

        // Note: Calling action is responsible for updating World.Update(minutes)
        // to avoid double time updates

        return Energy <= 0;
    }
    public void Consume(FoodItem food)
    {
        CalorieStore += food.Calories;
        Hydration += food.WaterContent;

        if (food.HealthEffect != null)
        {
            Heal(food.HealthEffect);
        }
        if (food.DamageEffect != null)
        {
            Damage(food.DamageEffect);
        }
    }

    public SurvivalData BundleSurvivalData() => new SurvivalData()
    {
        Temperature = BodyTemperature,
        Calories = CalorieStore,
        Hydration = Hydration,
        Energy = Energy,
        BodyStats = GetBodyStats(),
    };

    public BodyStats GetBodyStats() => new BodyStats
    {
        BodyWeight = Weight,
        MuscleWeight = Muscle,
        FatWeight = BodyFat,
        HealthPercent = Health,
    };

    #region Survival Consequences System

    /// <summary>
    /// Consume body fat to meet calorie deficit. Called when Calories = 0.
    /// </summary>
    /// <param name="calorieDeficit">How many calories below 0 (negative calories)</param>
    /// <param name="messages">List to add messages to</param>
    /// <returns>Remaining calorie deficit after consuming available fat</returns>
    private double ConsumeFat(double calorieDeficit, List<string> messages)
    {
        if (BodyFat <= MIN_FAT * Weight)
        {
            return calorieDeficit; // No fat left to burn
        }

        // Calculate how much fat we can burn (don't go below minimum)
        double fatAvailable = BodyFat - (MIN_FAT * Weight);
        double caloriesFromFat = fatAvailable * (CALORIES_PER_LB_FAT / LB_TO_KG); // Convert kg to lb, then to calories

        if (caloriesFromFat >= calorieDeficit)
        {
            // Enough fat to cover deficit
            double fatToBurn = calorieDeficit / CALORIES_PER_LB_FAT * LB_TO_KG;
            BodyFat -= fatToBurn;

            // Message based on severity
            double fatPercent = BodyFatPercentage;
            if (IsPlayer)
            {
                if (fatPercent < 0.08)
                    messages.Add("Your body is consuming the last of your fat reserves... You're becoming dangerously thin.");
                else if (fatPercent < 0.12)
                    messages.Add("Your body is burning fat reserves. You're noticeably thinner.");
            }

            return 0; // Deficit covered
        }
        else
        {
            // Burn all available fat, still have deficit
            BodyFat = MIN_FAT * Weight;
            if (IsPlayer)
            {
                messages.Add("Your body has exhausted all available fat reserves!");
            }
            return calorieDeficit - caloriesFromFat;
        }
    }

    /// <summary>
    /// Catabolize muscle tissue when fat reserves depleted. Reduces strength/speed.
    /// </summary>
    /// <param name="calorieDeficit">Remaining calorie deficit after fat consumption</param>
    /// <param name="messages">List to add messages to</param>
    /// <returns>Remaining deficit after consuming available muscle</returns>
    private double ConsumeMuscle(double calorieDeficit, List<string> messages)
    {
        if (Muscle <= MIN_MUSCLE * Weight)
        {
            return calorieDeficit; // At critical weakness
        }

        // Muscle catabolism is less efficient than fat burning
        double muscleAvailable = Muscle - (MIN_MUSCLE * Weight);
        double caloriesFromMuscle = muscleAvailable * (CALORIES_PER_LB_MUSCLE / LB_TO_KG);

        if (caloriesFromMuscle >= calorieDeficit)
        {
            double muscleToBurn = calorieDeficit / CALORIES_PER_LB_MUSCLE * LB_TO_KG;
            Muscle -= muscleToBurn;

            // Critical warnings - muscle loss is serious
            double musclePercent = MusclePercentage;
            if (IsPlayer)
            {
                if (musclePercent < 0.18)
                    messages.Add("Your body is cannibalizing muscle tissue! You feel extremely weak.");
                else if (musclePercent < 0.25)
                    messages.Add("Your muscles are wasting away. You're losing strength rapidly.");
            }

            return 0;
        }
        else
        {
            Muscle = MIN_MUSCLE * Weight;
            if (IsPlayer)
            {
                messages.Add("Your body has consumed almost all muscle tissue. Organ damage imminent!");
            }
            return calorieDeficit - caloriesFromMuscle;
        }
    }

    /// <summary>
    /// Apply organ damage from extreme starvation. Occurs when fat and muscle depleted.
    /// </summary>
    /// <param name="minutesElapsed">Minutes at critical starvation</param>
    private void ApplyStarvationOrganDamage(int minutesElapsed)
    {
        // Progressive damage over ~5-7 days (7200-10080 minutes)
        // Target: 0.1 HP per hour = death in ~10 hours of extreme starvation
        double damagePerMinute = 0.1 / 60.0;
        double totalDamage = damagePerMinute * minutesElapsed;

        // Target random vital organs
        var vitalOrgans = new[] { "Heart", "Liver", "Brain", "Lungs" };
        string targetOrgan = vitalOrgans[Random.Shared.Next(vitalOrgans.Length)];

        Damage(new DamageInfo
        {
            Amount = totalDamage,
            Type = DamageType.Internal, // Internal damage bypasses armor
            TargetPartName = targetOrgan,
            Source = "Starvation"
        });
    }

    /// <summary>
    /// Process all survival stat consequences. Called from UpdateBodyBasedOnResult.
    /// </summary>
    /// <param name="result">Result from SurvivalProcessor containing updated stats</param>
    /// <param name="minutesElapsed">Minutes that passed this update</param>
    private void ProcessSurvivalConsequences(SurvivalProcessorResult result, int minutesElapsed)
    {
        var data = result.Data;

        // ===== STARVATION PROGRESSION =====
        if (data.Calories <= 0)
        {
            _minutesStarving += minutesElapsed;

            // Calculate how many calories we needed but didn't have
            // We need to recalculate based on metabolism
            double currentMetabolism = 370 + (21.6 * Muscle) + (6.17 * BodyFat);
            currentMetabolism *= 0.7 + (0.3 * Health); // Injured bodies need more
            currentMetabolism *= data.activityLevel;
            double calorieDeficit = (currentMetabolism / 24.0 / 60.0) * minutesElapsed;

            // Stage 1: Consume fat reserves
            double remainingDeficit = ConsumeFat(calorieDeficit, result.Messages);

            // Stage 2: Catabolize muscle (only if fat depleted)
            if (remainingDeficit > 0)
            {
                remainingDeficit = ConsumeMuscle(remainingDeficit, result.Messages);
            }

            // Stage 3: Organ damage (only if muscle at minimum)
            if (remainingDeficit > 0 && _minutesStarving > 60480) // 6 weeks
            {
                ApplyStarvationOrganDamage(minutesElapsed);

                if (IsPlayer && _minutesStarving % 60 == 0) // Every hour
                {
                    result.Messages.Add($"You are starving to death... ({(int)(_minutesStarving / 1440)} days without food)");
                }
            }
        }
        else
        {
            _minutesStarving = 0; // Reset timer when fed
        }

        // ===== DEHYDRATION PROGRESSION =====
        if (data.Hydration <= 0)
        {
            _minutesDehydrated += minutesElapsed;

            // Dehydration kills faster than starvation
            // Target: ~24 hours (1440 minutes) to death
            if (_minutesDehydrated > 60) // After 1 hour, start damage
            {
                double damagePerMinute = 0.2 / 60.0; // 0.2 HP per hour = death in ~5 hours of severe dehydration
                double totalDamage = damagePerMinute * minutesElapsed;

                // Dehydration affects kidneys, brain, heart
                var affectedOrgans = new[] { "Brain", "Heart", "Liver" };
                string target = affectedOrgans[Random.Shared.Next(affectedOrgans.Length)];

                Damage(new DamageInfo
                {
                    Amount = totalDamage,
                    Type = DamageType.Internal,
                    TargetPartName = target,
                    Source = "Dehydration"
                });

                if (IsPlayer && _minutesDehydrated % 60 == 0) // Every hour
                {
                    result.Messages.Add($"Your organs are failing from dehydration... ({_minutesDehydrated / 60} hours without water)");
                }
            }
        }
        else
        {
            _minutesDehydrated = 0;
        }

        // ===== EXHAUSTION PROGRESSION =====
        if (data.Energy <= 0)
        {
            _minutesExhausted += minutesElapsed;

            // Exhaustion doesn't directly kill, but creates vulnerability
            // Track for potential future features (hallucinations, forced sleep, etc.)
            if (IsPlayer && _minutesExhausted > 480 && _minutesExhausted % 120 == 0) // Every 2 hours after 8 hours
            {
                result.Messages.Add("You're so exhausted you can barely function...");
            }
        }
        else
        {
            _minutesExhausted = 0;
        }

        // TODO: Regeneration (Phase 4)
    }

    #endregion

}