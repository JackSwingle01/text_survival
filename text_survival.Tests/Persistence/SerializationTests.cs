using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.Persistence;

namespace text_survival.Tests.Persistence;

public class SerializationTests
{
    [Fact]
    public void SerializeDeserialize_NewGame_RoundTrips()
    {
        // Arrange
        var original = GameContext.CreateNewGame();

        // Act - Try to serialize
        string json = JsonSerializer.Serialize(original, GetSerializerOptions());

        // Should get here without exceptions if serialization works
        Assert.NotNull(json);
        Assert.NotEmpty(json);

        // Try to deserialize
        var deserialized = JsonSerializer.Deserialize<GameContext>(json, GetSerializerOptions());

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.GameTime, deserialized.GameTime);
        Assert.Equal(original.Camp.Name, deserialized.Camp.Name);
    }

    [Fact]
    public void Serialize_NewGame_DoesNotThrow()
    {
        // Arrange
        var ctx = GameContext.CreateNewGame();

        // Act & Assert - should not throw
        var exception = Record.Exception(() =>
        {
            string json = JsonSerializer.Serialize(ctx, GetSerializerOptions());
        });

        Assert.Null(exception);
    }

    [Fact]
    public void SerializeDeserialize_LocationExplored_PreservesState()
    {
        // Arrange
        var ctx = GameContext.CreateNewGame();
        Assert.True(ctx.Camp.Explored, "Camp should start explored");

        // Act
        string json = JsonSerializer.Serialize(ctx, GetSerializerOptions());
        var deserialized = JsonSerializer.Deserialize<GameContext>(json, GetSerializerOptions());

        // Assert
        Assert.NotNull(deserialized);
        Assert.True(deserialized.Camp.Explored, "Camp Explored state should survive serialization");
    }

    [Fact]
    public void SerializeDeserialize_FullGameState_PreservesAllCriticalData()
    {
        // Arrange - Create game with diverse state
        var original = GameContext.CreateNewGame();

        // Add inventory diversity (test Stack<double>)
        original.Inventory.Add(Resource.Stick, 0.3);
        original.Inventory.Add(Resource.Stick, 0.25);  // Multiple stacks
        original.Inventory.Add(Resource.Pine, 1.2);
        original.Inventory.Add(Resource.Berries, 0.15);
        original.Inventory.WaterLiters = 1.5;

        // Add tool (test discrete items)
        original.Inventory.Tools.Add(Gear.Knife());

        // Count critical data before serialization
        int originalNamedLocationCount = original.Map!.NamedLocations.Count();
        int originalCampFeatureCount = original.Camp.Features.Count;
        bool hadSticks = original.Inventory.Count(Resource.Stick) > 0;
        bool hadTools = original.Inventory.Tools.Count > 0;

        // Act - Serialize and deserialize
        string json = JsonSerializer.Serialize(original, GetSerializerOptions());

        // Save for manual inspection
        File.WriteAllText(
            Path.Combine(Path.GetTempPath(), "test_game_full.json"),
            json
        );

        var deserialized = JsonSerializer.Deserialize<GameContext>(json, GetSerializerOptions());

        // Assert - Critical data categories preserved
        Assert.NotNull(deserialized);

        // Player exists and is valid
        Assert.NotNull(deserialized.player);
        Assert.NotNull(deserialized.player.Body);
        Assert.True(deserialized.player.Body.CalorieStore > 0, "Player has calories");
        Assert.True(deserialized.player.Body.Hydration >= 0, "Player hydration valid");

        // Map and named locations intact
        Assert.NotNull(deserialized.Map);
        Assert.True(deserialized.Map.NamedLocations.Count() >= 1, "Has named locations");
        Assert.NotNull(deserialized.Camp);
        Assert.True(deserialized.Map.Contains(deserialized.Camp), "Camp is on map");

        // Camp features preserved (tests polymorphism)
        Assert.Equal(originalCampFeatureCount, deserialized.Camp.Features.Count);
        Assert.NotNull(deserialized.Camp.GetFeature<HeatSourceFeature>());
        Assert.NotNull(deserialized.Camp.GetFeature<CacheFeature>());

        // Inventory preserved
        Assert.Equal(hadSticks, deserialized.Inventory.Count(Resource.Stick) > 0);
        Assert.Equal(hadTools, deserialized.Inventory.Tools.Count > 0);
        Assert.True(deserialized.Inventory.WaterLiters > 0, "Water preserved");

        // Weather exists
        Assert.NotNull(deserialized.Weather);

        // Time advanced (state changes persisted)
        Assert.True(deserialized.GameTime > original.GameTime.AddMinutes(-1),
            "GameTime in reasonable range");
    }

    [Fact]
    public void SerializeDeserialize_MapTravelOptions_CanBeResolved()
    {
        // Arrange
        var ctx = GameContext.CreateNewGame();

        // Get camp connections before serialization
        var originalConnections = ctx.Map!.GetTravelOptionsFrom(ctx.Camp);
        int originalConnectionCount = originalConnections.Count;

        // Record first connection name (if exists)
        string? firstConnectionName = originalConnections.FirstOrDefault()?.Name;

        // Act
        string json = JsonSerializer.Serialize(ctx, GetSerializerOptions());
        var deserialized = JsonSerializer.Deserialize<GameContext>(json, GetSerializerOptions());
        // deserialized!.RestoreAfterDeserialization();

        // Assert - Connections can be resolved
        var deserializedConnections = deserialized.Map!.GetTravelOptionsFrom(deserialized.Camp);

        Assert.Equal(originalConnectionCount, deserializedConnections.Count);

        if (firstConnectionName != null)
        {
            // Verify connection resolves to actual location
            var firstConnection = deserializedConnections.First();
            Assert.Equal(firstConnectionName, firstConnection.Name);
            Assert.True(deserialized.Map.Contains(firstConnection),
                "Connection is real location on map");
        }
    }

    [Fact]
    public void SerializeDeserialize_ResourceStacks_PreserveLIFOOrder()
    {
        // Arrange
        var ctx = GameContext.CreateNewGame();

        // Clear any existing sticks and add in known order
        while (ctx.Inventory.Count(Resource.Stick) > 0)
        {
            ctx.Inventory.Pop(Resource.Stick);
        }

        ctx.Inventory.Add(Resource.Stick, 0.1);  // Bottom
        ctx.Inventory.Add(Resource.Stick, 0.2);  // Middle
        ctx.Inventory.Add(Resource.Stick, 0.3);  // Top

        // Act
        string json = JsonSerializer.Serialize(ctx, GetSerializerOptions());
        var deserialized = JsonSerializer.Deserialize<GameContext>(json, GetSerializerOptions());

        // Assert - LIFO order preserved
        Assert.Equal(3, deserialized!.Inventory.Count(Resource.Stick));

        // Pop in reverse order (LIFO)
        double first = deserialized.Inventory.Pop(Resource.Stick);
        double second = deserialized.Inventory.Pop(Resource.Stick);
        double third = deserialized.Inventory.Pop(Resource.Stick);

        // Allow small floating point tolerance
        Assert.Equal(0.3, first, precision: 2);   // Last in, first out
        Assert.Equal(0.2, second, precision: 2);
        Assert.Equal(0.1, third, precision: 2);   // First in, last out
    }

    [Fact]
    public void SerializeDeserialize_LocationFeatures_PreserveConcreteTypes()
    {
        // Arrange
        var ctx = GameContext.CreateNewGame();

        // Add multiple feature types to test polymorphism
        ctx.Camp.Features.Add(new ForageFeature(1.0).AddSticks());
        ctx.Camp.Features.Add(new AnimalTerritoryFeature());

        int featureCountBefore = ctx.Camp.Features.Count;

        // Act
        string json = JsonSerializer.Serialize(ctx, GetSerializerOptions());
        var deserialized = JsonSerializer.Deserialize<GameContext>(json, GetSerializerOptions());

        // Assert - All feature types preserved (not just base LocationFeature)
        Assert.Equal(featureCountBefore, deserialized!.Camp.Features.Count);

        // Verify each type can be retrieved (tests polymorphism worked)
        Assert.NotNull(deserialized.Camp.GetFeature<HeatSourceFeature>());
        Assert.NotNull(deserialized.Camp.GetFeature<CacheFeature>());
        Assert.NotNull(deserialized.Camp.GetFeature<ForageFeature>());
        Assert.NotNull(deserialized.Camp.GetFeature<AnimalTerritoryFeature>());

        // Verify feature data preserved (ForageFeature resources list)
        var forageFeature = deserialized.Camp.GetFeature<ForageFeature>();
        var resourceTypes = forageFeature?.GetAvailableResourceTypes();
        Assert.NotNull(resourceTypes);
        Assert.Contains("kindling", resourceTypes);  // AddSticks() adds kindling
    }

    [Fact]
    public void SerializeDeserialize_DeserializedGame_CanContinuePlaying()
    {
        // Arrange
        var ctx = GameContext.CreateNewGame();

        // Act - Serialize and deserialize
        string json = JsonSerializer.Serialize(ctx, GetSerializerOptions());
        var deserialized = JsonSerializer.Deserialize<GameContext>(json, GetSerializerOptions());

        // // Restore transient state after deserialization
        // deserialized!.RestoreAfterDeserialization();

        // Assert - Game can continue (functional API works)

        // Can advance time
        var updateException = Record.Exception(() => deserialized.Update(10, ActivityType.Idle));
        Assert.Null(updateException);

        // Can access inventory
        var inventoryException = Record.Exception(() => deserialized.Inventory.Count(Resource.Stick));
        Assert.Null(inventoryException);

        // Can query travel options
        var travelException = Record.Exception(() => deserialized.Map!.GetTravelOptions());
        Assert.Null(travelException);

        // Can check player body properties
        var playerException = Record.Exception(() => _ = deserialized.player.Body.CalorieStore);
        Assert.Null(playerException);

        // Can check tensions
        var tensionsException = Record.Exception(() => deserialized.Tensions.HasTension("Stalked"));
        Assert.Null(tensionsException);
    }

    [Fact]
    public void Serialize_FullGame_CompletesQuickly()
    {
        // Arrange
        var ctx = GameContext.CreateNewGame();

        // Act
        var sw = Stopwatch.StartNew();
        string json = JsonSerializer.Serialize(ctx, GetSerializerOptions());
        sw.Stop();

        // Assert - Reasonable performance (< 200ms for new game)
        Assert.True(sw.ElapsedMilliseconds < 200,
            $"Serialization took {sw.ElapsedMilliseconds}ms (expected < 200ms)");

        // Verify JSON is reasonable size (not absurdly large)
        // Grid-based map with 1024 locations produces ~3MB compact JSON
        Assert.True(json.Length < 6_000_000,
            $"JSON is {json.Length} chars (expected < 6MB)");
    }

    [Fact]
    public void SerializeDeserialize_AllLocationFeatureTypes_PreserveState()
    {
        // Arrange - Create a location with all feature types
        var ctx = GameContext.CreateNewGame();

        // Use camp for testing
        var testLocation = ctx.Camp;

        // Add all feature types with meaningful state
        // Keep existing features, add new ones
        var originalFeatureCount = testLocation.Features.Count;

        // ShelterFeature with partial damage
        var shelter = new ShelterFeature("lean-to", 0.8, 0.7, 0.6);
        shelter.Damage(0.2); // Simulate some wear
        testLocation.Features.Add(shelter);

        // WaterFeature - frozen with ice hole
        var water = new WaterFeature("frozen_creek", "Frozen Creek")
            .AsThinIce()
            .WithExistingHole();
        testLocation.Features.Add(water);

        // HarvestableFeature
        var harvestable = new HarvestableFeature("berry_bush", "Berry Bush");
        testLocation.Features.Add(harvestable);

        // SalvageFeature
        var salvage = new SalvageFeature("abandoned_camp", "Abandoned Camp");
        testLocation.Features.Add(salvage);

        // SnareLineFeature with territory
        var territory = new AnimalTerritoryFeature(0.8).AddRabbit();
        var snareLine = new SnareLineFeature(territory);
        testLocation.Features.Add(snareLine);

        // CuringRackFeature
        var curingRack = new CuringRackFeature();
        testLocation.Features.Add(curingRack);

        int expectedFeatureCount = originalFeatureCount + 6;

        // Act
        string json = JsonSerializer.Serialize(ctx, GetSerializerOptions());
        var deserialized = JsonSerializer.Deserialize<GameContext>(json, GetSerializerOptions());

        // Assert - All features preserved with state
        Assert.Equal(expectedFeatureCount, deserialized!.Camp.Features.Count);

        // ShelterFeature state preserved
        var deserializedShelter = deserialized.Camp.GetFeature<ShelterFeature>();
        Assert.NotNull(deserializedShelter);
        Assert.True(deserializedShelter.Quality < 0.7, "Shelter damage preserved");

        // WaterFeature state preserved
        var deserializedWater = deserialized.Camp.GetFeature<WaterFeature>();
        Assert.NotNull(deserializedWater);
        Assert.True(deserializedWater.IsFrozen, "Water is frozen");
        Assert.True(deserializedWater.HasIceHole, "Ice hole preserved");
        Assert.True(deserializedWater.HasThinIce, "Thin ice state preserved");

        // HarvestableFeature preserved
        var deserializedHarvestable = deserialized.Camp.GetFeature<HarvestableFeature>();
        Assert.NotNull(deserializedHarvestable);

        // SalvageFeature preserved
        var deserializedSalvage = deserialized.Camp.GetFeature<SalvageFeature>();
        Assert.NotNull(deserializedSalvage);

        // SnareLineFeature preserved
        var deserializedSnareLine = deserialized.Camp.GetFeature<SnareLineFeature>();
        Assert.NotNull(deserializedSnareLine);

        // CuringRackFeature preserved
        var deserializedCuringRack = deserialized.Camp.GetFeature<CuringRackFeature>();
        Assert.NotNull(deserializedCuringRack);
    }

    [Fact]
    public void SerializeDeserialize_HeatSourceFeature_WithFuel()
    {
        // Arrange - Create fire with some fuel
        var ctx = GameContext.CreateNewGame();
        var fire = ctx.Camp.GetFeature<HeatSourceFeature>();
        Assert.NotNull(fire);

        // Add some fuel (using correct API signature)
        fire.AddFuel(1.5, FuelType.Kindling);
        fire.AddFuel(2.0, FuelType.PineWood);

        double originalUnburned = fire.UnburnedMassKg;
        bool wasActive = fire.IsActive;

        // Act
        string json = JsonSerializer.Serialize(ctx, GetSerializerOptions());
        var deserialized = JsonSerializer.Deserialize<GameContext>(json, GetSerializerOptions());

        // Assert - Fire state preserved
        var deserializedFire = deserialized!.Camp.GetFeature<HeatSourceFeature>();
        Assert.NotNull(deserializedFire);
        Assert.Equal(wasActive, deserializedFire.IsActive);
        Assert.Equal(originalUnburned, deserializedFire.UnburnedMassKg, precision: 2);
    }

    [Fact]
    public void SerializeDeserialize_BodyParts_PreserveStructure()
    {
        // Arrange
        var ctx = GameContext.CreateNewGame();
        var body = ctx.player.Body;

        int originalPartCount = body.Parts.Count;
        double originalWeight = body.WeightKG;

        // Act
        string json = JsonSerializer.Serialize(ctx, GetSerializerOptions());
        var deserialized = JsonSerializer.Deserialize<GameContext>(json, GetSerializerOptions());

        // Assert - Body structure preserved
        Assert.Equal(originalPartCount, deserialized!.player.Body.Parts.Count);
        Assert.Equal(originalWeight, deserialized.player.Body.WeightKG, precision: 2);
        Assert.True(deserialized.player.Body.CalorieStore > 0, "Calorie store preserved");
    }

    [Fact]
    public void SerializeDeserialize_BodyOrgans_PreserveState()
    {
        // Arrange
        var ctx = GameContext.CreateNewGame();
        var body = ctx.player.Body;

        // Get organ data before serialization
        var headPart = body.Parts.First(p => p.Name == "Head");
        int originalOrganCount = headPart.Organs.Count;
        var originalOrganNames = headPart.Organs.Select(o => o.Name).ToList();
        var brainOrgan = headPart.Organs.FirstOrDefault(o => o.Name == "Brain");
        Assert.NotNull(brainOrgan);
        bool brainIsExternal = brainOrgan.IsExternal;

        // Act
        string json = JsonSerializer.Serialize(ctx, GetSerializerOptions());
        var deserialized = JsonSerializer.Deserialize<GameContext>(json, GetSerializerOptions());

        // Assert - Organs preserved
        var deserializedHead = deserialized!.player.Body.Parts.First(p => p.Name == "Head");
        Assert.Equal(originalOrganCount, deserializedHead.Organs.Count);

        // Verify organ names preserved
        var deserializedOrganNames = deserializedHead.Organs.Select(o => o.Name).ToList();
        Assert.Equal(originalOrganNames.OrderBy(n => n), deserializedOrganNames.OrderBy(n => n));

        // Verify Brain organ can be found by name lookup (this is what dehydration damage does)
        var deserializedBrain = deserializedHead.Organs.FirstOrDefault(o => o.Name == "Brain");
        Assert.NotNull(deserializedBrain);
        Assert.Equal(brainIsExternal, deserializedBrain.IsExternal);

        // Verify all body organs survive (critical for dehydration targeting)
        var allOrgans = deserialized.player.Body.Parts.SelectMany(p => p.Organs).ToList();
        Assert.Contains(allOrgans, o => o.Name == "Brain");
        Assert.Contains(allOrgans, o => o.Name == "Heart");
        Assert.Contains(allOrgans, o => o.Name == "Liver");
    }

    [Fact]
    public void SerializeDeserialize_Stack_PreservesOrder()
    {
        // Arrange - Create a stack with known order
        var original = new Stack<string>();
        original.Push("bottom");
        original.Push("middle");
        original.Push("top");

        // Verify initial state
        Assert.Equal("top", original.Peek());

        // Act - Serialize and deserialize
        string json = JsonSerializer.Serialize(original, GetSerializerOptions());
        var deserialized = JsonSerializer.Deserialize<Stack<string>>(json, GetSerializerOptions());

        // Assert - Order preserved
        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized.Count);
        Assert.Equal("top", deserialized.Pop());
        Assert.Equal("middle", deserialized.Pop());
        Assert.Equal("bottom", deserialized.Pop());
    }

    [Fact]
    public void SerializeDeserialize_Stack_MultipleRoundTrips_NoAlternation()
    {
        // This test catches the bug where Stack order reverses on each serialize/deserialize cycle
        // causing alternating behavior (correct -> wrong -> correct -> wrong)

        // Arrange
        var stack = new Stack<int>();
        stack.Push(1);  // bottom
        stack.Push(2);
        stack.Push(3);  // top

        // Act - Multiple round-trips
        for (int i = 0; i < 5; i++)
        {
            string json = JsonSerializer.Serialize(stack, GetSerializerOptions());
            stack = JsonSerializer.Deserialize<Stack<int>>(json, GetSerializerOptions())!;

            // Assert - Top element is always 3, never alternates
            Assert.Equal(3, stack.Peek());
            Assert.Equal(3, stack.Count);
        }
    }

    [Fact]
    public void SerializeDeserialize_PlayerEffects_PreserveState()
    {
        // Arrange - Create game with player effects
        var ctx = GameContext.CreateNewGame();

        // Add multiple effect types with known capacity modifiers
        ctx.player.EffectRegistry.AddEffect(EffectFactory.Pain(0.5));
        ctx.player.EffectRegistry.AddEffect(EffectFactory.Fear(0.7));
        ctx.player.EffectRegistry.AddEffect(EffectFactory.Bleeding(0.3));
        ctx.player.EffectRegistry.AddEffect(EffectFactory.Hypothermia(0.6));

        int originalEffectCount = ctx.player.EffectRegistry.GetAll().Count;
        Assert.True(originalEffectCount >= 4, "Should have at least 4 effects");

        // Capture original capacity modifiers
        var originalModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();
        double originalConsciousnessMod = originalModifiers.GetCapacityModifier(CapacityNames.Consciousness);
        double originalManipulationMod = originalModifiers.GetCapacityModifier(CapacityNames.Manipulation);

        // Hypothermia at 0.6 severity should have Consciousness modifier of -0.5 * 0.6 = -0.3
        Assert.True(originalConsciousnessMod < -0.25,
            $"Original Consciousness modifier should be negative (Hypothermia + Pain), got {originalConsciousnessMod}");

        // Act
        string json = JsonSerializer.Serialize(ctx, GetSerializerOptions());
        var deserialized = JsonSerializer.Deserialize<GameContext>(json, GetSerializerOptions());

        // Assert - Effects preserved
        Assert.NotNull(deserialized);
        var deserializedEffects = deserialized.player.EffectRegistry.GetAll();
        Assert.Equal(originalEffectCount, deserializedEffects.Count);

        // Verify specific effects exist with correct state
        Assert.True(deserialized.player.EffectRegistry.HasEffect("Pain"), "Pain effect should survive serialization");
        Assert.True(deserialized.player.EffectRegistry.HasEffect("Fear"), "Fear effect should survive serialization");
        Assert.True(deserialized.player.EffectRegistry.HasEffect("Bleeding"), "Bleeding effect should survive serialization");
        Assert.True(deserialized.player.EffectRegistry.HasEffect("Hypothermia"), "Hypothermia effect should survive serialization");

        // Verify severity preserved
        var painSeverity = deserialized.player.EffectRegistry.GetSeverity("Pain");
        Assert.True(painSeverity > 0.4, $"Pain severity should be ~0.5, got {painSeverity}");

        // CRITICAL: Verify capacity modifiers are preserved
        var deserializedModifiers = deserialized.player.EffectRegistry.GetCapacityModifiers();
        double deserializedConsciousnessMod = deserializedModifiers.GetCapacityModifier(CapacityNames.Consciousness);
        double deserializedManipulationMod = deserializedModifiers.GetCapacityModifier(CapacityNames.Manipulation);

        Assert.Equal(originalConsciousnessMod, deserializedConsciousnessMod, precision: 2);
        Assert.Equal(originalManipulationMod, deserializedManipulationMod, precision: 2);

        // Verify modifiers are actually non-zero (the bug we're fixing)
        Assert.True(deserializedConsciousnessMod < -0.1,
            $"Deserialized Consciousness modifier should be negative, got {deserializedConsciousnessMod}");
    }

    [Fact]
    public void SerializeDeserialize_Herds_PreserveState()
    {
        // Arrange - Create game with herds (game already spawns some herds)
        var ctx = GameContext.CreateNewGame();

        // Record original counts (including game-spawned herds)
        int originalHerdCount = ctx.Herds.HerdCount;
        int originalAnimalCount = ctx.Herds.TotalAnimalCount;

        // Verify game spawns herds by default
        Assert.True(originalHerdCount > 0, "Game should spawn some herds at start");

        // Pick a specific herd to verify state preservation
        var firstHerd = ctx.Herds.GetPredatorHerds().FirstOrDefault()
            ?? ctx.Herds.GetPreyHerds().First();
        var firstHerdPosition = firstHerd.Position;
        var firstHerdType = firstHerd.AnimalType;
        var firstHerdMemberCount = firstHerd.MemberCount;
        var firstHerdBehaviorType = firstHerd.BehaviorType;
        firstHerd.Hunger = 0.75;  // Set specific state to verify

        // Act - Serialize and deserialize
        string json = JsonSerializer.Serialize(ctx, GetSerializerOptions());
        var deserialized = JsonSerializer.Deserialize<GameContext>(json, GetSerializerOptions());

        // Post-load restoration (mimics SaveManager.Load)
        deserialized?.Herds.RecreateAllMembers(deserialized.Map!);

        // Assert - Herd registry preserved
        Assert.NotNull(deserialized);
        Assert.Equal(originalHerdCount, deserialized.Herds.HerdCount);
        Assert.Equal(originalAnimalCount, deserialized.Herds.TotalAnimalCount);

        // Verify the specific herd we tracked still exists with correct state
        var matchingHerds = deserialized.Herds.GetHerdsByType(firstHerdType)
            .Where(h => h.Position == firstHerdPosition && h.MemberCount == firstHerdMemberCount)
            .ToList();
        Assert.NotEmpty(matchingHerds);

        var deserializedHerd = matchingHerds.First();

        // Verify serialized state preserved
        Assert.Equal(firstHerdMemberCount, deserializedHerd.Count);
        Assert.Equal(firstHerdMemberCount, deserializedHerd.Members.Count);  // Members recreated
        Assert.Equal(firstHerdPosition, deserializedHerd.Position);
        Assert.Equal(0.75, deserializedHerd.Hunger, precision: 2);  // State we set
        Assert.Equal(firstHerdBehaviorType, deserializedHerd.BehaviorType);
        Assert.NotNull(deserializedHerd.Behavior);  // Behavior recreated
        Assert.True(deserializedHerd.HomeTerritory.Count > 0, "HomeTerritory should be preserved");
    }

    private static JsonSerializerOptions GetSerializerOptions()
    {
        return SaveManager.Options;
    }
}
