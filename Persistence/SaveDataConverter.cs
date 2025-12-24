using text_survival.Actions;
using text_survival.Actions.Tensions;
using text_survival.Actors.Player;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Persistence;

/// <summary>
/// Converts between game objects and save data DTOs.
/// </summary>
public static class SaveDataConverter
{
    #region To Save Data

    /// <summary>
    /// Convert entire game context to save data.
    /// </summary>
    public static GameSaveData ToSaveData(GameContext ctx)
    {
        return new GameSaveData
        {
            SaveVersion = 1,
            GameTime = ctx.GameTime,
            Player = ToSaveData(ctx.player),
            PlayerInventory = ToSaveData(ctx.Inventory),
            CampStorage = ToSaveData(ctx.Camp.Storage),
            Zone = ToSaveData(ctx.CurrentLocation.ParentZone),
            CampLocationName = ctx.Camp.Location.Name,
            Tensions = ctx.Tensions.All.Select(ToSaveData).ToList(),
            NarrativeLog = ctx.Log.GetVisible()
                .Select(e => new LogEntrySaveData(e.Text, e.Level.ToString()))
                .ToList(),
            EventTriggerTimes = GameEventRegistry.GetTriggerTimes()
        };
    }

    private static PlayerSaveData ToSaveData(Player player)
    {
        return new PlayerSaveData
        {
            Body = ToSaveData(player.Body),
            Effects = player.EffectRegistry.GetAll().Select(ToSaveData).ToList()
        };
    }

    private static BodySaveData ToSaveData(Body body)
    {
        var parts = body.Parts.Select(p => new BodyPartSaveData(
            p.Name,
            p.Skin.Condition,
            p.Muscle.Condition,
            p.Bone.Condition
        )).ToList();

        var organs = body.Parts
            .SelectMany(p => p.Organs)
            .Select(o => new OrganSaveData(o.Name, o.Condition))
            .ToList();

        return new BodySaveData
        {
            CalorieStore = body.CalorieStore,
            Energy = body.Energy,
            Hydration = body.Hydration,
            BodyTemperature = body.BodyTemperature,
            BodyFatKG = body.BodyFatKG,
            MuscleKG = body.MuscleKG,
            BloodCondition = body.Blood.Condition,
            Parts = parts,
            Organs = organs
        };
    }

    private static EffectSaveData ToSaveData(Effect effect)
    {
        return new EffectSaveData
        {
            EffectKind = effect.EffectKind,
            Severity = effect.Severity,
            HourlySeverityChange = effect.HourlySeverityChange,
            TargetBodyPart = effect.TargetBodyPart,
            RequiresTreatment = effect.RequiresTreatment
        };
    }

    private static InventorySaveData ToSaveData(Inventory inv)
    {
        return new InventorySaveData
        {
            MaxWeightKg = inv.MaxWeightKg,
            Logs = inv.Logs.ToList(),
            Sticks = inv.Sticks.ToList(),
            Tinder = inv.Tinder.ToList(),
            CookedMeat = inv.CookedMeat.ToList(),
            RawMeat = inv.RawMeat.ToList(),
            Berries = inv.Berries.ToList(),
            WaterLiters = inv.WaterLiters,
            Stone = inv.Stone.ToList(),
            Bone = inv.Bone.ToList(),
            Hide = inv.Hide.ToList(),
            PlantFiber = inv.PlantFiber.ToList(),
            Sinew = inv.Sinew.ToList(),
            Tools = inv.Tools.Select(ToSaveData).ToList(),
            Head = inv.Head != null ? ToSaveData(inv.Head) : null,
            Chest = inv.Chest != null ? ToSaveData(inv.Chest) : null,
            Legs = inv.Legs != null ? ToSaveData(inv.Legs) : null,
            Feet = inv.Feet != null ? ToSaveData(inv.Feet) : null,
            Hands = inv.Hands != null ? ToSaveData(inv.Hands) : null,
            Weapon = inv.Weapon != null ? ToSaveData(inv.Weapon) : null
        };
    }

    private static ToolSaveData ToSaveData(Tool tool)
    {
        return new ToolSaveData
        {
            Name = tool.Name,
            Type = tool.Type.ToString(),
            Weight = tool.Weight,
            Durability = tool.Durability,
            Damage = tool.Damage,
            BlockChance = tool.BlockChance,
            WeaponClass = tool.WeaponClass?.ToString()
        };
    }

    private static EquipmentSaveData ToSaveData(Equipment equip)
    {
        return new EquipmentSaveData
        {
            Name = equip.Name,
            Slot = equip.Slot.ToString(),
            Weight = equip.Weight,
            Insulation = equip.Insulation
        };
    }

    private static ZoneSaveData ToSaveData(Zone zone)
    {
        return new ZoneSaveData
        {
            Name = zone.Name,
            Description = zone.Description,
            Weather = ToSaveData(zone.Weather),
            Locations = zone.Graph.All.Select(ToSaveData).ToList(),
            UnrevealedLocations = [] // TODO: Handle unrevealed locations if needed
        };
    }

    private static WeatherSaveData ToSaveData(ZoneWeather weather)
    {
        return new WeatherSaveData
        {
            BaseTemperature = weather.BaseTemperature,
            CurrentCondition = weather.CurrentCondition.ToString(),
            Precipitation = weather.Precipitation,
            WindSpeed = weather.WindSpeed,
            CloudCover = weather.CloudCover,
            CurrentSeason = weather.CurrentSeason.ToString()
        };
    }

    private static LocationSaveData ToSaveData(Location loc)
    {
        return new LocationSaveData
        {
            Name = loc.Name,
            Tags = loc.Tags,
            Explored = loc.Explored,
            ConnectionNames = loc.Connections.Select(c => c.Name).ToList(),
            BaseTraversalMinutes = loc.BaseTraversalMinutes,
            TerrainHazardLevel = loc.TerrainHazardLevel,
            WindFactor = loc.WindFactor,
            OverheadCoverLevel = loc.OverheadCoverLevel,
            VisibilityFactor = loc.VisibilityFactor,
            IsDark = loc.IsDark,
            Features = loc.Features.Select(ToSaveData).ToList()
        };
    }

    private static FeatureSaveData ToSaveData(LocationFeature feature)
    {
        var data = new FeatureSaveData
        {
            FeatureType = feature.GetType().Name,
            Name = feature.Name
        };

        switch (feature)
        {
            case HeatSourceFeature fire:
                return data with
                {
                    HasEmbers = fire.HasEmbers,
                    UnburnedMassKg = fire.UnburnedMassKg,
                    BurningMassKg = fire.BurningMassKg,
                    MaxFuelCapacityKg = fire.MaxFuelCapacityKg,
                    EmberTimeRemaining = fire.EmberTimeRemaining,
                    EmberDuration = fire.EmberDuration,
                    EmberStartTemperature = fire.EmberStartTemperature,
                    LastBurningTemperature = fire.LastBurningTemperature,
                    UnburnedMixture = fire.UnburnedMixture.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                    BurningMixture = fire.BurningMixture.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
                };

            case ForageFeature forage:
                return data with
                {
                    BaseResourceDensity = forage.BaseResourceDensity,
                    NumberOfHoursForaged = forage.NumberOfHoursForaged,
                    HoursSinceLastForage = forage.HoursSinceLastForage,
                    HasForagedBefore = forage.HasForagedBefore,
                    ForageResources = forage.Resources.Select(r => new ForageResourceSaveData(
                        r.Name, r.Abundance, r.MinWeight, r.MaxWeight
                    )).ToList()
                };

            case AnimalTerritoryFeature territory:
                return data with
                {
                    BaseGameDensity = territory.BaseGameDensity,
                    GameDensity = territory.GameDensity,
                    InitialDepletedDensity = territory.InitialDepletedDensity,
                    HoursSinceLastHunt = territory.HoursSinceLastHunt,
                    HasBeenHunted = territory.HasBeenHunted,
                    PossibleAnimals = territory.PossibleAnimals.Select(a =>
                        new AnimalSpawnSaveData(a.AnimalType, a.SpawnWeight)
                    ).ToList()
                };

            case ShelterFeature shelter:
                return data with
                {
                    TemperatureInsulation = shelter.TemperatureInsulation,
                    OverheadCoverage = shelter.OverheadCoverage,
                    WindCoverage = shelter.WindCoverage
                };

            case SnareLineFeature snareLine:
                return data with
                {
                    Snares = snareLine.GetSnares().Select(ToSaveData).ToList()
                };

            default:
                return data;
        }
    }

    private static SnareSaveData ToSaveData(PlacedSnare snare)
    {
        return new SnareSaveData
        {
            State = snare.State.ToString(),
            MinutesSet = snare.MinutesSet,
            Bait = snare.Bait.ToString(),
            BaitFreshness = snare.BaitFreshness,
            CaughtAnimalType = snare.CaughtAnimalType,
            CaughtAnimalWeightKg = snare.CaughtAnimalWeightKg,
            MinutesSinceCatch = snare.MinutesSinceCatch,
            DurabilityRemaining = snare.DurabilityRemaining,
            IsReinforced = snare.IsReinforced
        };
    }

    private static TensionSaveData ToSaveData(ActiveTension tension)
    {
        return new TensionSaveData
        {
            Type = tension.Type,
            Severity = tension.Severity,
            DecayPerHour = tension.DecayPerHour,
            DecaysAtCamp = tension.DecaysAtCamp,
            RelevantLocationName = tension.RelevantLocation?.Name,
            SourceLocationName = tension.SourceLocation?.Name,
            AnimalType = tension.AnimalType,
            Direction = tension.Direction,
            Description = tension.Description
        };
    }

    #endregion

    #region From Save Data

    /// <summary>
    /// Restore game context from save data.
    /// </summary>
    public static void RestoreFromSaveData(GameContext ctx, GameSaveData saveData)
    {
        // Restore game time
        ctx.GameTime = saveData.GameTime;

        // Restore zone (locations, weather, features)
        RestoreZone(ctx.CurrentLocation.ParentZone, saveData.Zone);

        // Find and set camp location
        var campLocation = ctx.CurrentLocation.ParentZone.Graph.All
            .FirstOrDefault(l => l.Name == saveData.CampLocationName);
        if (campLocation != null)
        {
            ctx.Camp.SetLocation(campLocation);
        }

        // Restore player
        RestorePlayer(ctx.player, saveData.Player);

        // Restore inventories
        RestoreInventory(ctx.Inventory, saveData.PlayerInventory);
        RestoreInventory(ctx.Camp.Storage, saveData.CampStorage);

        // Restore tensions
        ctx.Tensions.Clear();
        foreach (var tensionData in saveData.Tensions)
        {
            var tension = RestoreTension(tensionData, ctx.CurrentLocation.ParentZone);
            ctx.Tensions.AddRestoredTension(tension);
        }

        // Restore narrative log
        ctx.Log.Clear();
        foreach (var logEntry in saveData.NarrativeLog)
        {
            var level = Enum.Parse<LogLevel>(logEntry.Level);
            ctx.Log.Add(logEntry.Text, level);
        }

        // Restore event cooldown tracking
        GameEventRegistry.LoadTriggerTimes(saveData.EventTriggerTimes);
    }

    private static void RestorePlayer(Player player, PlayerSaveData data)
    {
        // Restore body
        RestoreBody(player.Body, data.Body);

        // Restore effects
        player.EffectRegistry.ClearEffects();
        foreach (var effectData in data.Effects)
        {
            var effect = RestoreEffect(effectData);
            player.EffectRegistry.AddRestoredEffect(effect);
        }
    }

    private static void RestoreBody(Body body, BodySaveData data)
    {
        // Restore vital stats and composition
        body.Restore(
            data.CalorieStore,
            data.Energy,
            data.Hydration,
            data.BodyTemperature,
            data.BodyFatKG,
            data.MuscleKG,
            data.BloodCondition
        );

        // Restore body part conditions by name
        foreach (var partData in data.Parts)
        {
            var part = body.Parts.FirstOrDefault(p => p.Name == partData.Name);
            if (part != null)
            {
                part.Skin.Condition = partData.SkinCondition;
                part.Muscle.Condition = partData.MuscleCondition;
                part.Bone.Condition = partData.BoneCondition;
            }
        }

        // Restore organ conditions by name
        foreach (var organData in data.Organs)
        {
            var organ = body.Parts
                .SelectMany(p => p.Organs)
                .FirstOrDefault(o => o.Name == organData.Name);
            if (organ != null)
            {
                organ.Condition = organData.Condition;
            }
        }
    }

    private static Effect RestoreEffect(EffectSaveData data)
    {
        return new Effect
        {
            EffectKind = data.EffectKind,
            Severity = data.Severity,
            HourlySeverityChange = data.HourlySeverityChange,
            TargetBodyPart = data.TargetBodyPart,
            RequiresTreatment = data.RequiresTreatment,
            IsActive = true
        };
    }

    private static void RestoreInventory(Inventory inv, InventorySaveData data)
    {
        // Clear existing
        inv.Logs.Clear();
        inv.Sticks.Clear();
        inv.Tinder.Clear();
        inv.CookedMeat.Clear();
        inv.RawMeat.Clear();
        inv.Berries.Clear();
        inv.Stone.Clear();
        inv.Bone.Clear();
        inv.Hide.Clear();
        inv.PlantFiber.Clear();
        inv.Sinew.Clear();
        inv.Tools.Clear();

        // Restore resources
        foreach (var item in data.Logs) inv.Logs.Push(item);
        foreach (var item in data.Sticks) inv.Sticks.Push(item);
        foreach (var item in data.Tinder) inv.Tinder.Push(item);
        foreach (var item in data.CookedMeat) inv.CookedMeat.Push(item);
        foreach (var item in data.RawMeat) inv.RawMeat.Push(item);
        foreach (var item in data.Berries) inv.Berries.Push(item);
        inv.WaterLiters = data.WaterLiters;
        foreach (var item in data.Stone) inv.Stone.Push(item);
        foreach (var item in data.Bone) inv.Bone.Push(item);
        foreach (var item in data.Hide) inv.Hide.Push(item);
        foreach (var item in data.PlantFiber) inv.PlantFiber.Push(item);
        foreach (var item in data.Sinew) inv.Sinew.Push(item);

        // Restore tools
        foreach (var toolData in data.Tools)
        {
            inv.Tools.Add(RestoreTool(toolData));
        }

        // Restore equipment
        inv.Head = data.Head != null ? RestoreEquipment(data.Head) : null;
        inv.Chest = data.Chest != null ? RestoreEquipment(data.Chest) : null;
        inv.Legs = data.Legs != null ? RestoreEquipment(data.Legs) : null;
        inv.Feet = data.Feet != null ? RestoreEquipment(data.Feet) : null;
        inv.Hands = data.Hands != null ? RestoreEquipment(data.Hands) : null;
        inv.Weapon = data.Weapon != null ? RestoreTool(data.Weapon) : null;
    }

    private static Tool RestoreTool(ToolSaveData data)
    {
        var type = Enum.Parse<ToolType>(data.Type);
        var tool = new Tool(data.Name, type, data.Weight)
        {
            Durability = data.Durability,
            Damage = data.Damage,
            BlockChance = data.BlockChance,
            WeaponClass = data.WeaponClass != null ? Enum.Parse<WeaponClass>(data.WeaponClass) : null
        };
        return tool;
    }

    private static Equipment RestoreEquipment(EquipmentSaveData data)
    {
        var slot = Enum.Parse<EquipSlot>(data.Slot);
        return new Equipment(data.Name, slot, data.Weight, data.Insulation);
    }

    private static void RestoreZone(Zone zone, ZoneSaveData data)
    {
        // Restore weather
        var condition = Enum.Parse<ZoneWeather.WeatherCondition>(data.Weather.CurrentCondition);
        var season = Enum.Parse<ZoneWeather.Season>(data.Weather.CurrentSeason);
        zone.Weather.RestoreState(
            data.Weather.BaseTemperature,
            condition,
            data.Weather.Precipitation,
            data.Weather.WindSpeed,
            data.Weather.CloudCover,
            season
        );

        // Build location name map for connection restoration
        var locationMap = zone.Graph.All.ToDictionary(l => l.Name);

        // Restore location states
        foreach (var locData in data.Locations)
        {
            if (!locationMap.TryGetValue(locData.Name, out var location))
                continue;

            // Restore explored state
            if (locData.Explored && !location.Explored)
            {
                location.Explore();
            }

            // Restore features
            RestoreLocationFeatures(location, locData.Features);
        }
    }

    private static void RestoreLocationFeatures(Location location, List<FeatureSaveData> featuresData)
    {
        foreach (var featureData in featuresData)
        {
            switch (featureData.FeatureType)
            {
                case "HeatSourceFeature":
                    var fire = location.GetFeature<HeatSourceFeature>();
                    if (fire != null)
                    {
                        var unburnedMix = ParseFuelMixture(featureData.UnburnedMixture);
                        var burningMix = ParseFuelMixture(featureData.BurningMixture);

                        fire.Restore(
                            featureData.HasEmbers ?? false,
                            featureData.UnburnedMassKg ?? 0,
                            featureData.BurningMassKg ?? 0,
                            featureData.MaxFuelCapacityKg ?? 10,
                            featureData.EmberTimeRemaining ?? 0,
                            featureData.EmberDuration ?? 0,
                            featureData.EmberStartTemperature ?? 0,
                            featureData.LastBurningTemperature ?? 0,
                            unburnedMix,
                            burningMix
                        );
                    }
                    break;

                case "ForageFeature":
                    var forage = location.GetFeature<ForageFeature>();
                    if (forage != null)
                    {
                        var resources = featureData.ForageResources?
                            .Select(r => new ForageResource(
                                r.Type,
                                GetForageAddCallback(r.Type),
                                r.Abundance,
                                r.MinWeight,
                                r.MaxWeight
                            ))
                            .ToList() ?? [];

                        forage.RestoreState(
                            featureData.NumberOfHoursForaged ?? 0,
                            featureData.HoursSinceLastForage ?? 0,
                            featureData.HasForagedBefore ?? false,
                            resources
                        );
                    }
                    break;

                case "AnimalTerritoryFeature":
                    var territory = location.GetFeature<AnimalTerritoryFeature>();
                    if (territory != null)
                    {
                        var animals = featureData.PossibleAnimals?
                            .Select(a => new AnimalSpawnEntry(a.AnimalType, a.SpawnWeight))
                            .ToList() ?? [];

                        territory.RestoreState(
                            featureData.GameDensity ?? 1.0,
                            featureData.InitialDepletedDensity ?? 1.0,
                            featureData.HoursSinceLastHunt ?? 0,
                            featureData.HasBeenHunted ?? false,
                            animals
                        );
                    }
                    break;

                case "ShelterFeature":
                    var shelter = location.GetFeature<ShelterFeature>();
                    shelter?.RestoreState(
                        featureData.TemperatureInsulation ?? 0,
                        featureData.OverheadCoverage ?? 0,
                        featureData.WindCoverage ?? 0
                    );
                    break;

                case "SnareLineFeature":
                    var snareLine = location.GetFeature<SnareLineFeature>();
                    if (snareLine != null && featureData.Snares != null)
                    {
                        snareLine.ClearSnares();
                        foreach (var snareData in featureData.Snares)
                        {
                            var snare = PlacedSnare.CreateRestored(
                                snareData.DurabilityRemaining,
                                snareData.IsReinforced
                            );
                            snare.RestoreState(
                                Enum.Parse<SnareState>(snareData.State),
                                snareData.MinutesSet,
                                Enum.Parse<BaitType>(snareData.Bait),
                                snareData.BaitFreshness,
                                snareData.CaughtAnimalType,
                                snareData.CaughtAnimalWeightKg,
                                snareData.MinutesSinceCatch,
                                snareData.DurabilityRemaining
                            );
                            snareLine.AddRestoredSnare(snare);
                        }
                    }
                    break;
            }
        }
    }

    private static Dictionary<FuelType, double> ParseFuelMixture(Dictionary<string, double>? data)
    {
        if (data == null) return [];
        return data.ToDictionary(
            kv => Enum.Parse<FuelType>(kv.Key),
            kv => kv.Value
        );
    }

    private static ActiveTension RestoreTension(TensionSaveData data, Zone zone)
    {
        // Find referenced locations by name
        Location? relevantLocation = null;
        Location? sourceLocation = null;

        if (data.RelevantLocationName != null)
        {
            relevantLocation = zone.Graph.All.FirstOrDefault(l => l.Name == data.RelevantLocationName);
        }
        if (data.SourceLocationName != null)
        {
            sourceLocation = zone.Graph.All.FirstOrDefault(l => l.Name == data.SourceLocationName);
        }

        return ActiveTension.Custom(
            data.Type,
            data.Severity,
            data.DecayPerHour,
            data.DecaysAtCamp,
            relevantLocation,
            data.AnimalType,
            data.Direction,
            data.Description
        );
    }

    /// <summary>
    /// Get the appropriate inventory add callback for a forage resource name.
    /// </summary>
    private static Action<Items.Inventory, double> GetForageAddCallback(string name)
    {
        return name.ToLower() switch
        {
            // Original resources
            "firewood" => (inv, w) => inv.Logs.Push(w),
            "kindling" => (inv, w) => inv.Sticks.Push(w),
            "tinder" => (inv, w) => inv.Tinder.Push(w),
            "berries" => (inv, w) => inv.Berries.Push(w),
            "stone" => (inv, w) => inv.Stone.Push(w),
            "plant fiber" => (inv, w) => inv.PlantFiber.Push(w),
            "bones" => (inv, w) => inv.Bone.Push(w),
            "small game" => (inv, w) => inv.RawMeat.Push(w),
            "water" => (inv, w) => inv.WaterLiters += w,

            // Stone types
            "shale" => (inv, w) => inv.Shale.Push(w),
            "flint" => (inv, w) => inv.Flint.Push(w),
            "pyrite" => (inv, w) => inv.Pyrite += w,

            // Wood types
            "pine" => (inv, w) => inv.Pine.Push(w),
            "birch" => (inv, w) => inv.Birch.Push(w),
            "oak" => (inv, w) => inv.Oak.Push(w),
            "birch bark" => (inv, w) => inv.BirchBark.Push(w),

            // Fungi (year-round on trees)
            "birch polypore" => (inv, w) => inv.BirchPolypore.Push(w),
            "chaga" => (inv, w) => inv.Chaga.Push(w),
            "amadou" => (inv, w) => inv.Amadou.Push(w),

            // Persistent plants (winter-available)
            "rose hips" => (inv, w) => inv.RoseHips.Push(w),
            "juniper berries" => (inv, w) => inv.JuniperBerries.Push(w),
            "willow bark" => (inv, w) => inv.WillowBark.Push(w),
            "pine needles" => (inv, w) => inv.PineNeedles.Push(w),

            // Tree products
            "pine resin" => (inv, w) => inv.PineResin.Push(w),
            "usnea" => (inv, w) => inv.Usnea.Push(w),
            "sphagnum" => (inv, w) => inv.Sphagnum.Push(w),

            // Food expansion
            "nuts" => (inv, w) => inv.Nuts.Push(w),
            "roots" => (inv, w) => inv.Roots.Push(w),

            // Raw materials
            "raw fiber" => (inv, w) => inv.RawFiber.Push(w),

            _ => (inv, w) => { } // Unknown resource - ignore
        };
    }

    #endregion
}
