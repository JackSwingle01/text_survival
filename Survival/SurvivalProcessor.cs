using text_survival.Bodies;
using text_survival.Effects;

namespace text_survival.Survival;


public static class SurvivalProcessor
{
    private const double BASE_EXHAUSTION_RATE = 1;
    public const double MAX_ENERGY_MINUTES = 960.0;
    /// <summary>
    /// Water lost per day to everything that is not sweat: urine, breath, and the moisture
    /// that passes through skin without sweating. Cold dry air makes the breath term larger
    /// than it would be in a temperate climate, which is why this sits at the top of the
    /// real range rather than the middle.
    /// </summary>
    /// <remarks>
    /// This was 4000, which is a real adult's *total* daily water turnover - and that figure
    /// already includes sweat. Sweat is modelled separately in GetSweatResponse and added on
    /// top, so every survivor was paying for their sweat twice. Measured over 30 seeds, the
    /// double count was 73% of all water lost and dehydration killed 24-29 of 30 within a day.
    /// </remarks>
    public const double BaseWaterLossMlPerDay = 2500.0;

    /// <summary>Non-sweat water loss per minute, in millilitres.</summary>
    private const double BaseWaterLossMlPerMinute = BaseWaterLossMlPerDay / (24.0 * 60.0);
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

    /// <summary>Sweat production at full severity, in millilitres per hour.</summary>
    private const double MaxSweatRateMlPerHour = 1000.0;

    /// <summary>
    /// Water held by fully saturated clothing (Wet severity 1.0). Shared by the two sides of
    /// the wetness ledger - sweat soaking in, and drying taking it back out - so they cannot
    /// drift apart.
    /// </summary>
    private const double MlPerFullSoak = 2000.0;

    /// <summary>
    /// Latent heat of vaporisation of sweat: evaporating 1ml removes ~0.58 kcal. This is why
    /// sweating cools at all, and why sweat that cannot evaporate cools nothing.
    /// </summary>
    private const double EvaporativeCoolingKcalPerMl = 0.58;

    /// <summary>Core-to-skin temperature gap when the skin is fully vasoconstricted, in F.</summary>
    /// <remarks>
    /// Skin near 29C at a normal core - cold hands, pale fingers. This is the state a body
    /// holds all winter, and the reason bare skin can feel freezing while the core is fine.
    /// </remarks>
    private const double ConstrictedGradientF = 14.0;

    /// <summary>Core-to-skin temperature gap when the skin is fully vasodilated, in F.</summary>
    /// <remarks>Skin near 35C - flushed, hot to the touch, shedding heat as fast as it can.</remarks>
    private const double DilatedGradientF = 2.8;

    /// <summary>
    /// How open the skin circulation is: 0 fully constricted, 1 fully dilated. This is the
    /// body's first and cheapest response to a heat imbalance - it moves blood, not water -
    /// and it runs out before the expensive ones start.
    /// </summary>
    /// <remarks>
    /// The two ends are the game's existing temperature thresholds, which makes the three
    /// lines of defence read in order: constriction is exhausted exactly where shivering
    /// begins, and dilation is exhausted exactly where sweating begins. Neither effector
    /// switches on while the blood alone could still have handled it.
    ///
    /// The ramp is asymmetric because the setpoint is 98.6F, not the midpoint of the two
    /// thresholds. At the setpoint tone is 0.5 and the gap is 8.4F - which is the fixed
    /// constant this replaced, so the thermal model is unchanged for a body at rest at a
    /// normal temperature and only starts to differ once it has something to correct.
    /// </remarks>
    private static double VasomotorTone(double coreTemperatureF)
    {
        if (coreTemperatureF < BaseBodyTemperature)
            return Math.Clamp(
                0.5 * (coreTemperatureF - ShiveringThreshold) / (BaseBodyTemperature - ShiveringThreshold), 0, 0.5);

        return Math.Clamp(
            0.5 + 0.5 * (coreTemperatureF - BaseBodyTemperature) / (SweatingThreshold - BaseBodyTemperature), 0.5, 1);
    }

    /// <summary>
    /// Skin temperature, which is what the environment actually touches. Heat leaves the body
    /// across the skin-to-air gap, so raising or lowering the skin is by itself a way to shed
    /// or hold heat, and it costs nothing.
    /// </summary>
    public static double SkinTemperatureF(double coreTemperatureF)
    {
        double tone = VasomotorTone(coreTemperatureF);
        double gradientF = ConstrictedGradientF + tone * (DilatedGradientF - ConstrictedGradientF);
        return coreTemperatureF - gradientF;
    }

    /// <summary>
    /// The body's attempt to cool itself, resolved once and feeding three consequences:
    /// heat shed, water spent, and the sweat the clothing could not pass - which soaks into
    /// it as wetness. Computing it in one place is what lets evaporation depend on clothing;
    /// while the cooling lived on the Sweating effect it could not see what was being worn.
    /// </summary>
    private readonly record struct SweatResponse(
        double Severity,
        double ProducedMlPerHour,
        double EvaporatedMlPerHour,
        double CoolingKcalPerHour)
    {
        /// <summary>Sweat that never evaporated. It is still lost from the body, and it ends up in the clothing.</summary>
        public double SoakedMlPerHour => ProducedMlPerHour - EvaporatedMlPerHour;
    }

    /// <summary>
    /// Evaporation is limited by two things the player can act on: how sealed the clothing is
    /// (the same waterproofing that keeps rain out also keeps vapour in - there is no
    /// breathable-waterproof material in this world), and how saturated it already is.
    /// Both approaching zero is heatstroke: the body keeps spending water and gets no cooling.
    /// </summary>
    private static SweatResponse GetSweatResponse(Body body, SurvivalContext context)
    {
        // Sweating is the second line of defence against heat and the expensive one - it
        // spends water the player has to go and find. It opens only once the blood has done
        // all it can, which by construction is where vasodilation tops out.
        if (VasomotorTone(body.BodyTemperature) < 1.0) return default;

        // Ramping from zero rather than from a floor matters: the old 0.10 minimum meant
        // crossing the threshold by a hundredth of a degree cost 100ml/hr, which over a day
        // is 2.4L - most of a survivor's water budget, spent on nothing.
        double severity = Math.Clamp((body.BodyTemperature - SweatingThreshold) / 4.0, 0, 1.0);
        double producedMlPerHour = MaxSweatRateMlPerHour * severity;

        double permeability = Math.Clamp(1 - context.WaterproofingLevel, 0, 1);
        double saturationHeadroom = Math.Clamp(1 - context.CurrentWetnessPct, 0, 1);
        double evaporatedFraction = permeability * saturationHeadroom;

        double evaporatedMlPerHour = producedMlPerHour * evaporatedFraction;

        return new SweatResponse(
            severity,
            producedMlPerHour,
            evaporatedMlPerHour,
            evaporatedMlPerHour * EvaporativeCoolingKcalPerMl);
    }

    /// <summary>Thermal resistance of one clo, in m²K/W. The standard conversion.</summary>
    private const double MSquaredKPerWattPerClo = 0.155;

    /// <summary>
    /// Convective heat transfer coefficient for the air around the body, W/m²K. Wind strips
    /// the still-air layer that does most of the insulating when you are undressed.
    /// </summary>
    private static double ConvectiveCoefficient(SurvivalContext context)
        => 5.0 * (1.0 + context.WindSpeedLevel * 2.0);

    /// <summary>
    /// Total resistance between core and air: clothing, body fat, and the boundary layer of
    /// air, added in series the way real thermal resistances add.
    /// </summary>
    /// <remarks>
    /// This replaces <c>heatLoss * (1 - insulation)</c> with <c>heatLoss / R</c>. The old form
    /// treated insulation as a percentage of heat blocked, which cannot exceed 100% and so
    /// needed an arbitrary clamp at 0.95 - and heavy gear sat exactly on that clamp, where
    /// every additional garment did nothing and the remaining 5% could not vent metabolism.
    /// That clamp was the reason the best clothing in the game cooked people to 111.9F.
    ///
    /// In series form no clamp is needed: doubling your clothing never halves heat loss,
    /// because the air layer is always in series with it, and returns diminish on their own.
    /// It also reproduces the textbook definition of the unit - 1 clo keeps a resting person
    /// comfortable at 21C - which is what makes gear values checkable against reality rather
    /// than invented. See WarmthTests.
    /// </remarks>
    public static double TotalThermalResistance(Body body, SurvivalContext context)
    {
        // Wet clothing conducts: soaked layers lose most of their trapped air.
        double wetnessPenalty = 1 - context.CurrentWetnessPct * 0.7;
        double clothingClo = context.ClothingClo * Math.Clamp(wetnessPenalty, 0.1, 1.0);

        // Wind pushes through anything not sealed. The same waterproofing that keeps rain out
        // and sweat in is what keeps wind out, so it does that job here too.
        double sealed_ = Math.Clamp(context.WaterproofingLevel, 0, 1);
        double windPenetration = context.WindSpeedLevel * (1 - sealed_) * 0.4;
        clothingClo *= Math.Clamp(1 - windPenetration, 0.2, 1.0);

        // Body fat and species fur, on the same scale. CalculateColdResistance returns 0-1;
        // a very fat human is worth roughly a clo of subcutaneous insulation.
        double naturalClo = Math.Clamp(AbilityCalculator.CalculateColdResistance(body), 0, 1) * 1.5;

        double rAir = 1.0 / ConvectiveCoefficient(context);
        return (clothingClo + naturalClo) * MSquaredKPerWattPerClo + rAir;
    }

    /// <summary>
    /// Fraction of heat the insulation blocks relative to being naked in the same conditions.
    /// The old <c>totalInsulation</c> number, now derived from the resistances rather than
    /// being the primary model - kept because the clothing heat buffer is expressed in it.
    /// </summary>
    private static double InsulationFraction(Body body, SurvivalContext context)
    {
        double rAir = 1.0 / ConvectiveCoefficient(context);
        return Math.Clamp(1 - rAir / TotalThermalResistance(body, context), 0, 0.99);
    }

    /// <summary>
    /// The air temperature at which a resting person in this clothing is in heat balance -
    /// warmer than this and they must shed heat, colder and they lose it. This is the number
    /// worth showing a player: compare it to the temperature outside.
    /// </summary>
    public static double ComfortTemperatureF(Body body, SurvivalContext context)
    {
        // Comfort is by definition the neutral vasomotor state - neither shutting the skin
        // down nor opening it up - so the skin temperature comes from the same curve the
        // heat balance uses rather than being restated here.
        double neutralSkinF = SkinTemperatureF(BaseBodyTemperature);
        double sensibleLossW = GetCurrentMetabolism(body, 1.0) / 24 / 0.86 * 0.75;
        double deltaF = sensibleLossW / 1.8 * TotalThermalResistance(body, context) * 9.0 / 5.0;
        return neutralSkinF - deltaF;
    }

    private enum TemperatureStage { Warm, Cool, Cold, Freezing, Hot }

    public static SurvivalProcessorResult Process(Body body, SurvivalContext context, int minutesElapsed)
    {
        var result = ProcessBaseNeeds(body, context, minutesElapsed);
        result.Combine(ProcessTemperature(body, context, minutesElapsed));
        result.Combine(ProcessWetness(body, context, minutesElapsed));
        result.Combine(ProcessBloody(context, minutesElapsed));

        // Project stats after delta to check consequences
        double projectedCalories = body.CalorieStore + result.StatsDelta.CalorieDelta;
        double projectedHydration = body.Hydration + result.StatsDelta.HydrationDelta;
        double projectedTemp = body.BodyTemperature + result.StatsDelta.TemperatureDelta;

        result.Combine(ProcessStarvation(body, projectedCalories, minutesElapsed));
        result.Combine(ProcessDehydration(body, projectedHydration, minutesElapsed));
        result.Combine(ProcessHypothermia(projectedTemp, minutesElapsed));
        result.Combine(ProcessRegeneration(body, projectedCalories, projectedHydration, minutesElapsed));

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
                HydrationDelta = -(BaseWaterLossMlPerMinute * minutesElapsed),
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

        double h = ConvectiveCoefficient(context);

        // Skin temperature is regulated, not a fixed offset from the core: this is the
        // vasomotor loop, and it is negative feedback. A rising core opens the skin, which
        // widens the gap to the air and sheds more heat; a falling core shuts it down.
        double skinTemp = SkinTemperatureF(body.BodyTemperature);
        double effectiveTemp = context.LocationTemperature + context.FireProximityBonus;
        double tempDifferential = skinTemp - effectiveTemp;
        double deltaT = tempDifferential * (5.0 / 9.0);

        // Insulation is a thermal resistance in series with the air around you, not a
        // percentage of heat blocked. See ThermalResistance.
        double rTotal = TotalThermalResistance(body, context);

        double heatLossW = surfaceArea * deltaT / rTotal;
        double sensibleLossHr = heatLossW * 0.86;
        double heatGainHr = GetCurrentMetabolism(body, context.ActivityLevel) / 24;

        // One heat balance: metabolism in, conduction/convection out, evaporation out.
        // Evaporative loss belongs here beside the sensible loss because it depends on the
        // same things - what is being worn, and how wet it already is.
        double evaporativeLossHr = GetSweatResponse(body, context).CoolingKcalPerHour;

        double netHeatHr = heatGainHr - sensibleLossHr - evaporativeLossHr;
        return netHeatHr / heatCapacity; // °F/hr
    }

    public static SurvivalProcessorResult ProcessTemperature(Body body, SurvivalContext context, int minutes)
    {
        double tempChange = CalculateTemperatureChangePerHour(body, context);

        // Clothing thermal mass buffer
        double clothingCapacityF = context.ClothingWeightKg * ThermalMassFactorFPerKg;
        double bufferDelta = 0;
        double bodyTempDelta = tempChange / 60 * minutes;

        if (clothingCapacityF > 0)
        {
            if (context.FireProximityBonus > 0 && context.ClothingHeatBuffer < 1.0)
            {
                // NEAR FIRE: Fill buffer based on fire intensity
                // Physics: 6.0 * FireProximityBonus kcal/hr heat transfer, 2% efficiency
                double fillRate = (context.FireProximityBonus / 150.0) / clothingCapacityF;
                bufferDelta = Math.Min(fillRate * minutes, 1.0 - context.ClothingHeatBuffer);
                // bodyTempDelta unchanged (fire already reduces heat loss)
            }

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
            else if (bodyTempDelta > 0) // WARMING
            {
                // Calculate heat blocked by insulation - goes to clothing buffer
                double totalInsulation = InsulationFraction(body, context);

                if (totalInsulation > 0 && clothingCapacityF > 0 && context.ClothingHeatBuffer < 1.0)
                {
                    // blockedDelta = rawDelta * insulation = bodyTempDelta * ins / (1 - ins)
                    double blockedHeat = bodyTempDelta * totalInsulation / (1 - totalInsulation);
                    double spaceF = (1.0 - context.ClothingHeatBuffer) * clothingCapacityF;
                    double toBufferF = Math.Min(blockedHeat, spaceF);
                    bufferDelta = toBufferF / clothingCapacityF;
                }
                // Body warming unchanged (already insulated rate)
            }
        }

        // The water cost of sweating is reported by the same computation that spent it,
        // rather than by the Sweating effect, so the two can never disagree about how hard
        // the body is working.
        double sweatHydrationDelta = -GetSweatResponse(body, context).ProducedMlPerHour / 60.0 * minutes;

        return new SurvivalProcessorResult
        {
            StatsDelta = new SurvivalStatsDelta
            {
                TemperatureDelta = bodyTempDelta,
                HydrationDelta = sweatHydrationDelta,
            },
            ClothingHeatBufferDelta = bufferDelta,
            Effects = GetTemperatureEffects(body, context),
        };
    }

    /// <summary>
    /// Project temperature after duration away from fire, accounting for buffer depletion.
    /// </summary>
    /// <remarks>
    /// KNOWN GAP: this removes the fire but keeps <see cref="SurvivalContext.LocationTemperature"/>
    /// exactly as the caller measured it. If the caller built its context while resting in a
    /// sheltered camp, that figure still carries the shelter bonus - worth up to 60F - even
    /// though walking away forfeits it. So an NPC asking "can I survive out there" is
    /// answered about a warmer world than the one it is about to enter. Fixing it means
    /// deciding which tile and activity the projection is for, which changes NPC behavior,
    /// so it is deliberately left visible here rather than silently patched.
    /// </remarks>
    public static double ProjectTemperatureAwayFromFire(Body body, SurvivalContext context, int minutes)
    {
        // The counterfactual is a value, not a mutation of the caller's context. The old
        // version zeroed the field and restored it afterwards with no try/finally, so a
        // throw inside ProcessTemperature left the caller's context permanently altered.
        var awayFromFire = context with { FireProximityBonus = 0 };

        var result = ProcessTemperature(body, awayFromFire, minutes);

        return body.BodyTemperature + result.StatsDelta.TemperatureDelta;
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
            BodyTarget target = vitalOrgans[Utils.Rng.Next(vitalOrgans.Length)];

            result.DamageEvents.Add(new DamageInfo(damage, DamageType.Internal, target));
        }

        return result;
    }

    private static SurvivalProcessorResult ProcessDehydration(Body body, double projectedHydration, int minutesElapsed)
    {
        if (projectedHydration > 0)
        {
            // Reset critical flag when hydration recovers
            body.WasDehydrationCritical = false;
            return new SurvivalProcessorResult();
        }

        double damagePerMinute = 1.0 / 60.0;  // 1.0/hour = death in ~5 hours
        double damage = damagePerMinute * minutesElapsed;

        var affectedOrgans = new[] { BodyTarget.Brain, BodyTarget.Heart, BodyTarget.Liver };
        BodyTarget target = affectedOrgans[Utils.Rng.Next(affectedOrgans.Length)];

        var result = new SurvivalProcessorResult
        {
            DamageEvents = [
                new DamageInfo(damage, DamageType.Internal, target)
            ],
        };

        // Only show message once when entering critical state
        if (!body.WasDehydrationCritical)
        {
            result.Messages.Add("Your organs are failing from dehydration!");
            body.WasDehydrationCritical = true;
        }

        return result;
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
            ? 0.1 + (0.4 * severityFactor)              // .1-.5/hour for mild
            : 0.5 + (0.5 * (severityFactor - 0.5));    // 1.0-8.0/hour for severe
        double damage = (damagePerHour / 60.0) * minutesElapsed;

        var coreOrgans = new[] { BodyTarget.Heart, BodyTarget.Brain, BodyTarget.Lungs };
        BodyTarget target = coreOrgans[Utils.Rng.Next(coreOrgans.Length)];

        return new SurvivalProcessorResult
        {
            DamageEvents = [
                new DamageInfo(damage, DamageType.Internal, target)
            ]
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

        if (Utils.Rng.NextDouble() < 0.01)
            result.Messages.Add("Your body is slowly healing...");

        return result;
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

    /// <summary>
    /// Basal metabolic rate in kcal/day, by the Katch-McArdle formula.
    /// </summary>
    /// <remarks>
    /// Katch-McArdle is defined on LEAN BODY MASS - everything that is not fat, so bone,
    /// organs, blood and water as well as muscle. It used to be passed MuscleKG (22.5kg for
    /// a baseline human) where it wanted lean mass (63.8kg), which gave 925 kcal/day against
    /// a real adult's ~1800. Every survivor in this game was running on half a human's
    /// metabolism: burning half the food, and - because metabolism is also the body's only
    /// heat source - producing half the warmth, which is why comfort temperatures came out
    /// 10-25F above the textbook figures for the same clothing.
    ///
    /// Fat is deliberately excluded rather than given its own term: the formula is defined
    /// that way, adipose tissue is barely metabolically active, and its contribution is
    /// already inside the 370 constant.
    /// </remarks>
    public static double GetCurrentMetabolism(Body body, double activityLevel)
    {
        double leanBodyMassKg = Math.Max(0, body.WeightKG - body.BodyFatKG);
        double bmr = 370 + (21.6 * leanBodyMassKg);
        // Organ condition affects metabolism - damaged organs = less efficient
        double organCondition = body.Parts.SelectMany(p => p.Organs).Average(o => o.Condition);
        bmr *= 0.7 + (0.3 * organCondition);
        return bmr * activityLevel;
    }
    private static List<Effect> GetTemperatureEffects(Body body, SurvivalContext context)
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

        // The visible Sweating effect reports the same response that spends the water and
        // sheds the heat, rather than recomputing the severity and drifting from it.
        double sweatSeverity = GetSweatResponse(body, context).Severity;
        if (sweatSeverity > 0.01)
            effects.Add(EffectFactory.Sweating(sweatSeverity));

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

    /// <summary>
    /// How fast wet clothing dries, in Wet-severity per hour.
    /// </summary>
    /// <remarks>
    /// Derived from latent heat rather than picked: evaporating a millilitre costs ~0.58
    /// kcal, so taking fully soaked clothing (2000ml) back to dry needs ~1160 kcal - about
    /// half a day of a resting person's entire metabolism. That is precisely why wet clothing
    /// is dangerous and why a fire is the real answer to it, and it sets every timescale here.
    ///
    /// The previous rates were 5-8x too fast: fully soaked clothing dried in 86 minutes at
    /// 40F and in 7-10 minutes by a fire, which made both the Wet effect and sweat-soaking
    /// nearly inert. Now:
    ///
    ///     by a fire        1.0-1.4 h      50F breezy       4.4 h
    ///     60F breezy       2.9 h          40F breezy      10.0 h
    ///     below freezing   never (only a fire will do it)
    /// </remarks>
    private static double CalculateDryingRate(SurvivalContext context)
    {
        double kcalPerHour;

        if (context.FireProximityBonus > 0)
        {
            // A fire supplies the latent heat directly, which is why it dries you in about an
            // hour when the weather alone would take all day.
            kcalPerHour = 300 + context.FireProximityBonus * 25;
        }
        else if (context.LocationTemperature > 32)
        {
            // Ambient evaporation, scaling with how far above freezing the air is.
            kcalPerHour = (context.LocationTemperature - 32) * 10;
        }
        else
        {
            // Below freezing clothes freeze wet - only a fire will dry them. This used to be
            // the intent but not the behavior: wind was added after this branch, so a windy
            // day at 25F dried soaked clothing in two hours.
            return 0;
        }

        // Wind accelerates evaporation; it cannot create it. Multiplying rather than adding is
        // what keeps the freezing rule above true.
        if (!context.IsRaining && !context.IsSnowing && !context.IsBlizzard)
        {
            kcalPerHour *= 1 + context.WindSpeedLevel * 1.5;
        }

        return kcalPerHour / (MlPerFullSoak * EvaporativeCoolingKcalPerMl);
    }

    private static SurvivalProcessorResult ProcessWetness(Body body, SurvivalContext context, int minutesElapsed)
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

        // Sweat that could not evaporate soaks the clothing. This is the classic way to
        // die in the cold: work hard, soak your layers, then stop moving and freeze in them.
        wetnessDelta += GetSweatResponse(body, context).SoakedMlPerHour / 60.0 / MlPerFullSoak;

        // Calculate drying (reduction in wetness per minute)
        double dryingRate = CalculateDryingRate(context);
        double dryingDelta = (dryingRate / 60.0) * minutesElapsed; // Convert hourly rate to per-minute

        // Calculate new severity (accumulation - drying)
        double newSeverity = Math.Clamp(
            context.CurrentWetnessPct + wetnessDelta * minutesElapsed - dryingDelta,
            0, 1);

        // The Wet effect IS the stored wetness - SurvivalContext reads CurrentWetnessPct back
        // off it - so it has to be emitted whenever there is any wetness at all, not only
        // once it is worth mentioning. Gating emission at 5% meant the effect was never
        // created, so CurrentWetnessPct read 0 every tick and accumulation restarted from
        // zero each minute. Wetness could never exceed a single minute's delta (0.01 in the
        // heaviest rain), so in practice nobody in this game has ever got wet.
        //
        // EffectRegistry.SetEffectSeverity already withholds the "you're getting wet" message
        // below 0.05, which is where that threshold belongs.
        if (newSeverity > 0)
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
                HydrationDelta = -BaseWaterLossMlPerMinute * 0.7 * minutes,
                CalorieDelta = -GetCurrentMetabolism(body, .5) / 24.0 / 60.0 * minutes,
            }
        };
    }
}