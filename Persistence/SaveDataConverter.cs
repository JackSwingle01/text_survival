using System.Reflection;
using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Tensions;
using text_survival.Actors.Player;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;
using Mapster;

namespace text_survival.Persistence;

/// <summary>
/// Converts between game objects and save data DTOs.
/// </summary>
public static class SaveDataConverter
{
    static SaveDataConverter()
    {
        // Configure Mapster type mappings
        ConfigureMapsterMappings();
    }

    private static void ConfigureMapsterMappings()
    {
        // Simple types that auto-map with enum conversions
        TypeAdapterConfig<Tool, ToolSaveData>.NewConfig()
            .Map(dest => dest.Type, src => src.Type.ToString())
            .Map(dest => dest.WeaponClass, src => src.WeaponClass != null ? src.WeaponClass.ToString() : null);

        TypeAdapterConfig<ToolSaveData, Tool>.NewConfig()
            .MapWith(src => new Tool(src.Name, Enum.Parse<ToolType>(src.Type), src.Weight)
            {
                Durability = src.Durability,
                Damage = src.Damage,
                BlockChance = src.BlockChance,
                WeaponClass = src.WeaponClass != null ? Enum.Parse<WeaponClass>(src.WeaponClass) : null
            });

        TypeAdapterConfig<Equipment, EquipmentSaveData>.NewConfig()
            .Map(dest => dest.Slot, src => src.Slot.ToString());

        TypeAdapterConfig<EquipmentSaveData, Equipment>.NewConfig()
            .MapWith(src => new Equipment(src.Name, Enum.Parse<EquipSlot>(src.Slot), src.Weight, src.Insulation));

        TypeAdapterConfig<ZoneWeather, WeatherSaveData>.NewConfig()
            .Map(dest => dest.CurrentCondition, src => src.CurrentCondition.ToString())
            .Map(dest => dest.CurrentSeason, src => src.CurrentSeason.ToString());

        // Location - map Connections to ConnectionNames
        TypeAdapterConfig<Location, LocationSaveData>.NewConfig()
            .Map(dest => dest.ConnectionIds, src => src.Connections.Select(c => c.Id).ToList())
            .Map(dest => dest.Features, src => src.Features.Select(f => f.Adapt<FeatureSaveData>()).ToList())
            .Map(dest => dest.Explored, src => src.Explored)
            .Map(dest => dest.DiscoveryText, src => src.DiscoveryText);

        // Zone - map Graph.All to Locations
        TypeAdapterConfig<Zone, ZoneSaveData>.NewConfig()
            .Map(dest => dest.Locations, src => src.Graph.All.Select(l => l.Adapt<LocationSaveData>()).ToList())
            .Map(dest => dest.UnrevealedLocations, src => src.UnrevealedLocations.Select(l => l.Adapt<LocationSaveData>()).ToList())
            .Map(dest => dest.Weather, src => src.Weather.Adapt<WeatherSaveData>());

        // Player - simple mapping
        TypeAdapterConfig<Player, PlayerSaveData>.NewConfig()
            .Map(dest => dest.Body, src => src.Body.Adapt<BodySaveData>())
            .Map(dest => dest.Effects, src => src.EffectRegistry.GetAll().Select(e => e.Adapt<EffectSaveData>()).ToList());

        // PlacedSnare - simple with enum
        TypeAdapterConfig<PlacedSnare, SnareSaveData>.NewConfig()
            .Map(dest => dest.State, src => src.State.ToString())
            .Map(dest => dest.Bait, src => src.Bait.ToString());

        // ActiveTension - map location refs to names
        TypeAdapterConfig<ActiveTension, TensionSaveData>.NewConfig()
            .Map(dest => dest.Type, src => src.Type.ToString())
            .Map(dest => dest.RelevantLocationName, src => src.RelevantLocation != null ? src.RelevantLocation.Name : null)
            .Map(dest => dest.SourceLocationName, src => src.SourceLocation != null ? src.SourceLocation.Name : null)
            .Map(dest => dest.AnimalType, src => src.AnimalType != null ? src.AnimalType.ToString() : null)
            .Map(dest => dest.Direction, src => src.Direction != null ? src.Direction.ToString() : null);

        // Expedition - save travel history and state
        TypeAdapterConfig<Expedition, ExpeditionSaveData>.NewConfig()
            .Map(dest => dest.TravelHistoryLocationNames, src => src.TravelHistory.Select(loc => loc.Name).ToList())
            .Map(dest => dest.State, src => src.State.ToString())
            .Map(dest => dest.MinutesElapsedTotal, src => src.MinutesElapsedTotal)
            .Map(dest => dest.CollectionLog, src => src.CollectionLog.ToList());

        // EncounterConfig - simple mapping with null-safe modifiers
        TypeAdapterConfig<EncounterConfig, EncounterConfigSaveData>.NewConfig()
            .Map(dest => dest.Modifiers, src => src.Modifiers ?? new List<string>());

        // Inventory - Stack<double> properties auto-map now
        TypeAdapterConfig<Inventory, InventorySaveData>.NewConfig()
            .Map(dest => dest.Tools, src => src.Tools.Select(t => t.Adapt<ToolSaveData>()).ToList())
            .Map(dest => dest.Special, src => src.Special.Select(i => new ToolSaveData
            {
                Name = i.Name,
                Weight = i.Weight,
                Type = "Item"
            }).ToList());

        // Inventory reverse - InventorySaveData to Inventory
        // Stack<double> properties and Equipment auto-map now
        TypeAdapterConfig<InventorySaveData, Inventory>.NewConfig()
            .Ignore(dest => dest.Tools)    // Handled in AfterMapping
            .Ignore(dest => dest.Special)  // Handled in AfterMapping
            .AfterMapping((src, dest) =>
            {
                // Restore Tools
                dest.Tools = src.Tools.Select(t => t.Adapt<Tool>()).ToList();

                // Restore Special items
                dest.Special = src.Special.Select(i => new Item(i.Name, i.Weight)).ToList();
            });

        // Effect - bidirectional with conditional nested objects
        TypeAdapterConfig<Effect, EffectSaveData>.NewConfig()
            .Map(dest => dest.CapacityModifiers, src => src.CapacityModifiers.ToDictionary())
            .Map(dest => dest.StatsDelta, src =>
                src.StatsDelta.TemperatureDelta != 0 || src.StatsDelta.CalorieDelta != 0 ||
                src.StatsDelta.HydrationDelta != 0 || src.StatsDelta.EnergyDelta != 0
                    ? new StatsDeltaSaveData(
                        src.StatsDelta.TemperatureDelta,
                        src.StatsDelta.CalorieDelta,
                        src.StatsDelta.HydrationDelta,
                        src.StatsDelta.EnergyDelta)
                    : null)
            .Map(dest => dest.Damage, src =>
                src.Damage != null
                    ? new DamageOverTimeSaveData(src.Damage.PerHour, src.Damage.Type.ToString())
                    : null);

        TypeAdapterConfig<EffectSaveData, Effect>.NewConfig()
            .MapWith(src => new Effect
            {
                // Identity
                EffectKind = src.EffectKind,
                TargetBodyPart = src.TargetBodyPart,

                // State
                Severity = src.Severity,
                HourlySeverityChange = src.HourlySeverityChange,
                RequiresTreatment = src.RequiresTreatment,
                CanHaveMultiple = src.CanHaveMultiple,
                IsActive = true,

                // Effects
                CapacityModifiers = CapacityModifierContainer.FromDictionary(src.CapacityModifiers),
                StatsDelta = src.StatsDelta != null
                    ? new SurvivalStatsDelta
                    {
                        TemperatureDelta = src.StatsDelta.TemperatureDelta,
                        CalorieDelta = src.StatsDelta.CalorieDelta,
                        HydrationDelta = src.StatsDelta.HydrationDelta,
                        EnergyDelta = src.StatsDelta.EnergyDelta
                    }
                    : new SurvivalStatsDelta(),
                Damage = src.Damage != null
                    ? new Effect.DamageOverTime(src.Damage.PerHour, Enum.Parse<DamageType>(src.Damage.DamageType))
                    : null,

                // Messages
                ApplicationMessage = src.ApplicationMessage,
                RemovalMessage = src.RemovalMessage
            });

        // Body - bidirectional with hierarchical flattening
        TypeAdapterConfig<Body, BodySaveData>.NewConfig()
            .Map(dest => dest.Parts, src => src.Parts.Select(p => new BodyPartSaveData(
                p.Name,
                p.Skin.Condition,
                p.Muscle.Condition,
                p.Bone.Condition
            )).ToList())
            .Map(dest => dest.Organs, src => src.Parts
                .SelectMany(p => p.Organs)
                .Select(o => new OrganSaveData(o.Name, o.Condition))
                .ToList());

        TypeAdapterConfig<BodySaveData, Body>.NewConfig()
            .AfterMapping((src, dest) =>
            {
                // Restore simple properties via Body.Restore()
                dest.Restore(
                    src.CalorieStore,
                    src.Energy,
                    src.Hydration,
                    src.BodyTemperature,
                    src.BodyFatKG,
                    src.MuscleKG,
                    src.BloodCondition
                );

                // Restore body part conditions by name
                foreach (var partData in src.Parts)
                {
                    var part = dest.Parts.FirstOrDefault(p => p.Name == partData.Name);
                    if (part != null)
                    {
                        part.Skin.Condition = partData.SkinCondition;
                        part.Muscle.Condition = partData.MuscleCondition;
                        part.Bone.Condition = partData.BoneCondition;
                    }
                }

                // Restore organ conditions by name
                foreach (var organData in src.Organs)
                {
                    var organ = dest.Parts
                        .SelectMany(p => p.Organs)
                        .FirstOrDefault(o => o.Name == organData.Name);
                    if (organ != null)
                    {
                        organ.Condition = organData.Condition;
                    }
                }
            });
    }

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
            Expedition = ctx.Expedition?.Adapt<ExpeditionSaveData>(),
            CurrentActivity = ctx.CurrentActivity.ToString(),
            PendingEncounter = ctx.PendingEncounter?.Adapt<EncounterConfigSaveData>(),
            Tensions = ctx.Tensions.All.Select(ToSaveData).ToList(),
            NarrativeLog = ctx.Log.GetVisible()
                .Select(e => new LogEntrySaveData(e.Text, e.Level.ToString()))
                .ToList(),
            EventTriggerTimes = GameEventRegistry.GetTriggerTimes()
        };
    }

    private static PlayerSaveData ToSaveData(Player player) => player.Adapt<PlayerSaveData>();

    private static BodySaveData ToSaveData(Body body) => body.Adapt<BodySaveData>();

    private static EffectSaveData ToSaveData(Effect effect) => effect.Adapt<EffectSaveData>();

    private static InventorySaveData ToSaveData(Inventory inv) => inv.Adapt<InventorySaveData>();

    private static ToolSaveData ToSaveData(Tool tool) => tool.Adapt<ToolSaveData>();

    private static EquipmentSaveData ToSaveData(Equipment equip) => equip.Adapt<EquipmentSaveData>();

    private static ZoneSaveData ToSaveData(Zone zone) => zone.Adapt<ZoneSaveData>();

    private static WeatherSaveData ToSaveData(ZoneWeather weather) => weather.Adapt<WeatherSaveData>();

    private static LocationSaveData ToSaveData(Location loc) => loc.Adapt<LocationSaveData>();

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

            case CacheFeature cache:
                return data with
                {
                    CacheType = cache.Type.ToString(),
                    CacheCapacityKg = cache.CapacityKg,
                    CacheProtectsFromPredators = cache.ProtectsFromPredators,
                    CacheProtectsFromWeather = cache.ProtectsFromWeather,
                    CachePreservesFood = cache.PreservesFood,
                    CacheStorage = ToSaveData(cache.Storage)
                };

            case CuringRackFeature rack:
                return data with
                {
                    CuringRackCapacity = rack.Capacity,
                    CuringItems = rack.GetItemsForSave().Select(i => new CuringItemSaveData(
                        i.Type.ToString(),
                        i.WeightKg,
                        i.MinutesCured,
                        i.MinutesRequired
                    )).ToList()
                };

            default:
                return data;
        }
    }

    private static SnareSaveData ToSaveData(PlacedSnare snare) => snare.Adapt<SnareSaveData>();

    private static TensionSaveData ToSaveData(ActiveTension tension) => tension.Adapt<TensionSaveData>();

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

        // Restore expedition if player was away from camp
        if (saveData.Expedition != null && saveData.Expedition.TravelHistoryLocationNames.Count > 0)
        {
            // Get start location (first in history - the camp)
            var startLocationName = saveData.Expedition.TravelHistoryLocationNames[0];
            var startLocation = ctx.CurrentLocation.ParentZone.Graph.All
                .FirstOrDefault(l => l.Name == startLocationName) ?? ctx.Camp.Location;

            // Create expedition with start location
            ctx.Expedition = new Expedition(startLocation, ctx.player);

            // Restore the rest of the travel history by pushing locations
            for (int i = 1; i < saveData.Expedition.TravelHistoryLocationNames.Count; i++)
            {
                var locationName = saveData.Expedition.TravelHistoryLocationNames[i];
                var location = ctx.CurrentLocation.ParentZone.Graph.All
                    .FirstOrDefault(l => l.Name == locationName);
                if (location != null)
                {
                    ctx.Expedition.TravelHistory.Push(location);
                }
            }

            // Restore state
            ctx.Expedition.State = Enum.Parse<ExpeditionState>(saveData.Expedition.State);

            // Restore collection log
            ctx.Expedition.CollectionLog.AddRange(saveData.Expedition.CollectionLog);

            // Note: MinutesElapsedTotal has a private setter, so it will start from 0
            // This is acceptable as elapsed time resets between sessions
        }

        // Restore current activity
        if (Enum.TryParse<ActivityType>(saveData.CurrentActivity, out var activity))
        {
            // Use reflection to set private setter
            var activityProp = typeof(GameContext).GetProperty("CurrentActivity");
            activityProp?.SetValue(ctx, activity);
        }

        // Restore pending encounter
        if (saveData.PendingEncounter != null)
        {
            ctx.PendingEncounter = new EncounterConfig(
                saveData.PendingEncounter.AnimalType,
                saveData.PendingEncounter.InitialDistance,
                saveData.PendingEncounter.InitialBoldness,
                saveData.PendingEncounter.Modifiers.Count > 0 ? saveData.PendingEncounter.Modifiers : null
            );
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
        data.Adapt(body);
    }

    private static Effect RestoreEffect(EffectSaveData data) => data.Adapt<Effect>();

    private static void RestoreInventory(Inventory inv, InventorySaveData data)
    {
        data.Adapt(inv);
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

        // Clear existing graph/locations (should be empty if loaded correctly, but safe to clear)
        // Note: Graph.Clear() isn't available, but we can assume we're populating a fresh zone.
        // If we are reusing a zone, we should ensure it's empty first.
        
        // Dictionary to hold all restored locations for connection linking
        var restoredLocations = new Dictionary<Guid, Location>();

        // Helper to create and populate a location
        Location CreateAndRegister(LocationSaveData locData)
        {
            var loc = new Location(
                locData.Name,
                locData.Tags,
                zone,
                locData.BaseTraversalMinutes,
                locData.TerrainHazardLevel,
                locData.WindFactor,
                locData.OverheadCoverLevel,
                locData.VisibilityFactor
            )
            {
                Id = locData.Id,
                IsDark = locData.IsDark,
                DiscoveryText = locData.DiscoveryText
            };

            if (locData.Explored)
            {
                loc.Explore();
            }

            RestoreLocationFeatures(loc, locData.Features);
            restoredLocations[loc.Id] = loc;
            return loc;
        }

        // 1. Reconstruct Revealed Locations
        foreach (var locData in data.Locations)
        {
            var loc = CreateAndRegister(locData);
            zone.Graph.Add(loc);
        }

        // 2. Reconstruct Unrevealed Locations
        foreach (var locData in data.UnrevealedLocations)
        {
            var loc = CreateAndRegister(locData);
            zone.AddUnrevealedLocation(loc);
        }

        // 3. Re-establish Connections
        // We iterate through the save data again to link the instances we just created
        var allLocData = data.Locations.Concat(data.UnrevealedLocations);
        
        foreach (var locData in allLocData)
        {
            if (!restoredLocations.TryGetValue(locData.Id, out var sourceLoc)) continue;

            foreach (var connId in locData.ConnectionIds)
            {
                // We only need to link if target exists
                if (restoredLocations.TryGetValue(connId, out var targetLoc))
                {
                    sourceLoc.AddConnection(targetLoc);
                }
            }
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
                    if (fire == null)
                    {
                        fire = new HeatSourceFeature(featureData.MaxFuelCapacityKg ?? 10.0);
                        location.AddFeature(fire);
                    }

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
                    break;

                case "ForageFeature":
                    var forage = location.GetFeature<ForageFeature>();
                    if (forage == null)
                    {
                        forage = new ForageFeature();
                        location.AddFeature(forage);
                    }

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
                    break;

                case "AnimalTerritoryFeature":
                    var territory = location.GetFeature<AnimalTerritoryFeature>();
                    if (territory == null)
                    {
                        territory = new AnimalTerritoryFeature();
                        location.AddFeature(territory);
                    }

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
                    break;

                case "ShelterFeature":
                    var shelter = location.GetFeature<ShelterFeature>();
                    if (shelter == null)
                    {
                        shelter = new ShelterFeature(featureData.Name, 0, 0, 0);
                        location.AddFeature(shelter);
                    }
                    
                    shelter.RestoreState(
                        featureData.TemperatureInsulation ?? 0,
                        featureData.OverheadCoverage ?? 0,
                        featureData.WindCoverage ?? 0
                    );
                    break;

                case "SnareLineFeature":
                    var snareLine = location.GetFeature<SnareLineFeature>();
                    if (snareLine == null)
                    {
                        // SnareLineFeature requires an AnimalTerritoryFeature
                        var snareTerritory = location.GetFeature<AnimalTerritoryFeature>() ?? new AnimalTerritoryFeature();
                        if (location.GetFeature<AnimalTerritoryFeature>() == null)
                            location.AddFeature(snareTerritory);
                        snareLine = new SnareLineFeature(snareTerritory);
                        location.AddFeature(snareLine);
                    }

                    if (featureData.Snares != null)
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

                case "CacheFeature":
                    var cache = location.GetFeature<CacheFeature>();
                    if (cache == null && featureData.CacheType != null)
                    {
                         cache = new CacheFeature(
                             featureData.Name,
                             Enum.Parse<CacheType>(featureData.CacheType),
                             featureData.CacheCapacityKg ?? -1,
                             featureData.CacheProtectsFromPredators ?? false,
                             featureData.CacheProtectsFromWeather ?? false,
                             featureData.CachePreservesFood ?? false
                         );
                         location.AddFeature(cache);
                    }
                    
                    if (cache != null && featureData.CacheStorage != null)
                    {
                        RestoreInventory(cache.Storage, featureData.CacheStorage);
                    }
                    break;

                case "CuringRackFeature":
                    var rack = location.GetFeature<CuringRackFeature>();
                    if (rack == null)
                    {
                        rack = new CuringRackFeature { Capacity = featureData.CuringRackCapacity ?? 4 };
                        location.AddFeature(rack);
                    }

                    if (featureData.CuringItems != null)
                    {
                        var items = featureData.CuringItems.Select(i => (
                            Enum.Parse<CurableItemType>(i.Type),
                            i.WeightKg,
                            i.MinutesCured,
                            i.MinutesRequired
                        ));
                        rack.RestoreState(items);
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
