using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Tests.Environments;

public class WoodedAreaFeatureTests
{
    #region Progress Tracking

    [Fact]
    public void AddProgress_IncreasesMinutesWorked()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150);

        // Act
        feature.AddProgress(30);

        // Assert
        Assert.Equal(30, feature.MinutesWorked);
        Assert.Equal(0.2, feature.ProgressPct, precision: 2); // 30/150 = 0.2
    }

    [Fact]
    public void AddProgress_AccumulatesAcrossMultipleCalls()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150);

        // Act
        feature.AddProgress(30);
        feature.AddProgress(45);
        feature.AddProgress(25);

        // Assert
        Assert.Equal(100, feature.MinutesWorked);
    }

    [Fact]
    public void IsTreeReady_FalseWhenProgressIncomplete()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150);
        feature.AddProgress(100);

        // Assert
        Assert.False(feature.IsTreeReady);
    }

    [Fact]
    public void IsTreeReady_TrueWhenProgressComplete()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150);
        feature.AddProgress(150);

        // Assert
        Assert.True(feature.IsTreeReady);
    }

    [Fact]
    public void IsTreeReady_TrueWhenProgressExceedsRequired()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150);
        feature.AddProgress(200);

        // Assert
        Assert.True(feature.IsTreeReady);
    }

    #endregion

    #region Tree Felling

    [Fact]
    public void FellTree_ResetsMinutesWorked()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150);
        feature.AddProgress(150);

        // Act
        feature.FellTree();

        // Assert
        Assert.Equal(0, feature.MinutesWorked);
        Assert.Equal(0, feature.ProgressPct);
    }

    [Fact]
    public void FellTree_ReturnsInventoryWithLogs()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150);
        feature.AddProgress(150);

        // Act
        var yield = feature.FellTree();

        // Assert - should yield 8-10 logs
        int logCount = yield.Stacks[Resource.Pine].Count;
        Assert.InRange(logCount, 8, 10);
    }

    [Fact]
    public void FellTree_ReturnsInventoryWithSticks()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150);
        feature.AddProgress(150);

        // Act
        var yield = feature.FellTree();

        // Assert - should yield 4-6 sticks
        int stickCount = yield.Stacks[Resource.Stick].Count;
        Assert.InRange(stickCount, 4, 6);
    }

    [Fact]
    public void FellTree_ReturnsInventoryWithTinder()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150);
        feature.AddProgress(150);

        // Act
        var yield = feature.FellTree();

        // Assert - should yield 2-3 tinder
        int tinderCount = yield.Stacks[Resource.Tinder].Count;
        Assert.InRange(tinderCount, 2, 3);
    }

    [Fact]
    public void FellTree_YieldsCorrectWoodType()
    {
        // Arrange
        var oakFeature = new WoodedAreaFeature("Oak Stand", Resource.Oak, 180);
        oakFeature.AddProgress(180);

        // Act
        var yield = oakFeature.FellTree();

        // Assert - should yield oak logs specifically
        int oakCount = yield.Stacks[Resource.Oak].Count;
        Assert.InRange(oakCount, 8, 10);
        Assert.Equal(0, yield.Stacks[Resource.Pine].Count);
        Assert.Equal(0, yield.Stacks[Resource.Birch].Count);
    }

    [Fact]
    public void FellTree_MixedWoodYieldsOneOfThreeTypes()
    {
        // Arrange - null WoodType means mixed
        var mixedFeature = new WoodedAreaFeature("Mixed Forest", null, 150);

        // Act - fell multiple trees and collect wood types
        var woodTypesFound = new HashSet<Resource>();
        for (int i = 0; i < 20; i++)
        {
            mixedFeature.AddProgress(150);
            var yield = mixedFeature.FellTree();

            if (yield.Stacks[Resource.Pine].Count > 0) woodTypesFound.Add(Resource.Pine);
            if (yield.Stacks[Resource.Birch].Count > 0) woodTypesFound.Add(Resource.Birch);
            if (yield.Stacks[Resource.Oak].Count > 0) woodTypesFound.Add(Resource.Oak);
        }

        // Assert - over 20 trees, should see variety (statistically very unlikely to get only one type)
        Assert.True(woodTypesFound.Count >= 2,
            $"Expected variety in mixed wood, but only got: {string.Join(", ", woodTypesFound)}");
    }

    #endregion

    #region Limited Trees

    [Fact]
    public void HasTrees_TrueWhenUnlimited()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150, treesAvailable: null);

        // Assert
        Assert.True(feature.HasTrees);
    }

    [Fact]
    public void HasTrees_TrueWhenTreesRemain()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150, treesAvailable: 5);

        // Assert
        Assert.True(feature.HasTrees);
    }

    [Fact]
    public void HasTrees_FalseWhenDepleted()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150, treesAvailable: 0);

        // Assert
        Assert.False(feature.HasTrees);
    }

    [Fact]
    public void FellTree_DecrementsTreeCount()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150, treesAvailable: 3);
        feature.AddProgress(150);

        // Act
        feature.FellTree();

        // Assert
        Assert.Equal(2, feature.TreesAvailable);
    }

    [Fact]
    public void FellTree_DoesNotDecrementWhenUnlimited()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150, treesAvailable: null);
        feature.AddProgress(150);

        // Act
        feature.FellTree();

        // Assert
        Assert.Null(feature.TreesAvailable);
        Assert.True(feature.HasTrees);
    }

    #endregion

    #region Respawn

    [Fact]
    public void Update_RespawnsTreesWhenDepleted()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150, treesAvailable: 0)
        {
            RespawnHoursPerTree = 24 // 24 hours to respawn one tree
        };

        // Act - simulate 24 hours passing (1440 minutes)
        feature.Update(new FeatureUpdateContext(1440, 32));

        // Assert
        Assert.Equal(1, feature.TreesAvailable);
        Assert.True(feature.HasTrees);
    }

    [Fact]
    public void Update_DoesNotRespawnWhenTreesAvailable()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150, treesAvailable: 2)
        {
            RespawnHoursPerTree = 24
        };

        // Act
        feature.Update(new FeatureUpdateContext(1440, 32));

        // Assert - should stay at 2, not increase
        Assert.Equal(2, feature.TreesAvailable);
    }

    [Fact]
    public void Update_DoesNotRespawnUnlimitedTrees()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test Timber", Resource.Pine, 150, treesAvailable: null)
        {
            RespawnHoursPerTree = 24
        };

        // Act
        feature.Update(new FeatureUpdateContext(1440, 32));

        // Assert
        Assert.Null(feature.TreesAvailable);
    }

    #endregion

    #region Status Description

    [Fact]
    public void GetStatusDescription_ReturnsCleared_WhenNoTrees()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test", Resource.Pine, 150, treesAvailable: 0);

        // Assert
        Assert.Equal("cleared", feature.GetStatusDescription());
    }

    [Fact]
    public void GetStatusDescription_ReturnsProgress_WhenWorking()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test", Resource.Pine, 150);
        feature.AddProgress(75); // 50%

        // Assert
        Assert.Contains("50%", feature.GetStatusDescription());
    }

    [Fact]
    public void GetStatusDescription_ReturnsTreeCount_WhenLimited()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test", Resource.Pine, 150, treesAvailable: 5);

        // Assert
        Assert.Contains("5 trees", feature.GetStatusDescription());
    }

    [Fact]
    public void GetStatusDescription_ReturnsStandingTimber_WhenUnlimited()
    {
        // Arrange
        var feature = new WoodedAreaFeature("Test", Resource.Pine, 150, treesAvailable: null);

        // Assert
        Assert.Equal("standing timber", feature.GetStatusDescription());
    }

    #endregion
}
