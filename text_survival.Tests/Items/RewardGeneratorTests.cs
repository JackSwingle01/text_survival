using text_survival.Items;

namespace text_survival.Tests.Items;

public class RewardGeneratorTests
{
    [Fact]
    public void Generate_None_ReturnsEmptyResources()
    {
        // Act
        var result = RewardGenerator.Generate(RewardPool.None);

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Generate_BasicSupplies_ReturnsNonEmptyResources()
    {
        // Act
        var result = RewardGenerator.Generate(RewardPool.BasicSupplies);

        // Assert
        Assert.False(result.IsEmpty);
        Assert.False(string.IsNullOrEmpty(result.GetDescription()));
    }

    [Fact]
    public void Generate_CraftingMaterials_ReturnsCraftingResources()
    {
        // Act - run multiple times to test randomness
        bool hasStone = false, hasBone = false, hasPlantFiber = false;
        for (int i = 0; i < 50; i++)
        {
            var result = RewardGenerator.Generate(RewardPool.CraftingMaterials);
            if (result.Count(Resource.Stone) > 0) hasStone = true;
            if (result.Count(Resource.Bone) > 0) hasBone = true;
            if (result.Count(Resource.PlantFiber) > 0) hasPlantFiber = true;
        }

        // Assert - over 50 runs, should see at least one of each type
        Assert.True(hasStone || hasBone || hasPlantFiber,
            "CraftingMaterials should generate at least one type of crafting material");
    }

    [Fact]
    public void Generate_ScrapTool_ReturnsDamagedTool()
    {
        // Act
        var result = RewardGenerator.Generate(RewardPool.ScrapTool);

        // Assert
        Assert.Single(result.Tools);
        var tool = result.Tools[0];
        Assert.True(tool.Durability >= 4 && tool.Durability <= 10,
            "Scrap tool should have durability between 4 and 10");
    }

    [Fact]
    public void Generate_WaterSource_ReturnsWater()
    {
        // Act
        var result = RewardGenerator.Generate(RewardPool.WaterSource);

        // Assert
        Assert.True(result.WaterLiters > 0);
        Assert.True(result.WaterLiters >= 1.0 && result.WaterLiters <= 2.5,
            "Water amount should be between 1.0 and 2.5 liters");
    }

    [Fact]
    public void Generate_TinderBundle_ReturnsTinder()
    {
        // Act
        var result = RewardGenerator.Generate(RewardPool.TinderBundle);

        // Assert
        Assert.Equal(1, result.Count(Resource.Tinder));
        Assert.True(result.Weight(Resource.Tinder) >= 0.4 && result.Weight(Resource.Tinder) <= 0.8,
            "Tinder weight should be between 0.4 and 0.8 kg");
    }

    [Fact]
    public void Generate_BoneHarvest_ReturnsBones()
    {
        // Act
        var result = RewardGenerator.Generate(RewardPool.BoneHarvest);

        // Assert
        Assert.True(result.Count(Resource.Bone) >= 1 && result.Count(Resource.Bone) <= 3,
            "Bone harvest should return 1-3 bones");
        Assert.True(result.Weight(Resource.Bone) >= 0.2 && result.Weight(Resource.Bone) <= 1.5,
            "Bone weight should be between 0.2 and 1.5 kg (1-3 bones at 0.2-0.5 kg each)");
    }

    [Fact]
    public void Generate_SmallGame_ReturnsMeatAndMaybeBone()
    {
        // Act
        var result = RewardGenerator.Generate(RewardPool.SmallGame);

        // Assert
        Assert.Equal(1, result.Count(Resource.RawMeat));
        Assert.True(result.Weight(Resource.RawMeat) >= 0.4 && result.Weight(Resource.RawMeat) <= 0.7,
            "Small game meat should weigh between 0.4 and 0.7 kg");
        // Bone is optional (70% chance), hide optional (40% chance)
        Assert.True(result.Count(Resource.Bone) <= 1);
        Assert.True(result.Count(Resource.Hide) <= 1);
    }

    [Fact]
    public void Generate_HideScrap_ReturnsHide()
    {
        // Act
        var result = RewardGenerator.Generate(RewardPool.HideScrap);

        // Assert
        Assert.Equal(1, result.Count(Resource.Hide));
        Assert.True(result.Weight(Resource.Hide) >= 0.5 && result.Weight(Resource.Hide) <= 0.9,
            "Hide scrap should weigh between 0.5 and 0.9 kg");
    }

    [Theory]
    [InlineData(RewardPool.BasicSupplies)]
    [InlineData(RewardPool.AbandonedCamp)]
    [InlineData(RewardPool.HiddenCache)]
    [InlineData(RewardPool.BasicMeat)]
    [InlineData(RewardPool.LargeMeat)]
    [InlineData(RewardPool.GameTrailDiscovery)]
    [InlineData(RewardPool.SquirrelCache)]
    [InlineData(RewardPool.HoneyHarvest)]
    [InlineData(RewardPool.MedicinalForage)]
    [InlineData(RewardPool.CraftingMaterials)]
    [InlineData(RewardPool.ScrapTool)]
    [InlineData(RewardPool.WaterSource)]
    [InlineData(RewardPool.TinderBundle)]
    [InlineData(RewardPool.BoneHarvest)]
    [InlineData(RewardPool.SmallGame)]
    [InlineData(RewardPool.HideScrap)]
    public void Generate_AllPools_ReturnNonEmpty(RewardPool pool)
    {
        // Act
        var result = RewardGenerator.Generate(pool);

        // Assert
        Assert.False(result.IsEmpty, $"RewardPool.{pool} should not return empty resources");
        Assert.False(string.IsNullOrEmpty(result.GetDescription()),
            $"RewardPool.{pool} should have a description");
    }

    [Fact]
    public void Generate_AllPoolsHaveDescriptions()
    {
        // Test that all pools generate proper descriptions for expedition logs
        var pools = Enum.GetValues<RewardPool>().Where(p => p != RewardPool.None);

        foreach (var pool in pools)
        {
            var result = RewardGenerator.Generate(pool);
            var description = result.GetDescription();
            Assert.False(string.IsNullOrEmpty(description),
                $"RewardPool.{pool} should generate a description");
            Assert.NotEqual("nothing", description);
        }
    }
}
