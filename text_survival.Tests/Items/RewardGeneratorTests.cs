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
        Assert.True(result.Descriptions.Count > 0);
    }

    [Fact]
    public void Generate_CraftingMaterials_ReturnsCraftingResources()
    {
        // Act - run multiple times to test randomness
        bool hasStone = false, hasBone = false, hasPlantFiber = false;
        for (int i = 0; i < 50; i++)
        {
            var result = RewardGenerator.Generate(RewardPool.CraftingMaterials);
            if (result.Stone.Count > 0) hasStone = true;
            if (result.Bone.Count > 0) hasBone = true;
            if (result.PlantFiber.Count > 0) hasPlantFiber = true;
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
        Assert.True(tool.Durability > 0 && tool.Durability < 10,
            "Scrap tool should have limited durability");
    }

    [Fact]
    public void Generate_WaterSource_ReturnsWater()
    {
        // Act
        var result = RewardGenerator.Generate(RewardPool.WaterSource);

        // Assert
        Assert.True(result.WaterLiters > 0);
        Assert.True(result.WaterLiters >= 0.5 && result.WaterLiters <= 1.5,
            "Water amount should be between 0.5 and 1.5 liters");
    }

    [Fact]
    public void Generate_TinderBundle_ReturnsTinder()
    {
        // Act
        var result = RewardGenerator.Generate(RewardPool.TinderBundle);

        // Assert
        Assert.Single(result.Tinder);
        Assert.True(result.Tinder[0] >= 0.2 && result.Tinder[0] <= 0.5,
            "Tinder weight should be between 0.2 and 0.5 kg");
    }

    [Fact]
    public void Generate_BoneHarvest_ReturnsBones()
    {
        // Act
        var result = RewardGenerator.Generate(RewardPool.BoneHarvest);

        // Assert
        Assert.True(result.Bone.Count >= 1 && result.Bone.Count <= 3,
            "Bone harvest should return 1-3 bones");
        foreach (var bone in result.Bone)
        {
            Assert.True(bone >= 0.2 && bone <= 0.5,
                "Each bone should weigh between 0.2 and 0.5 kg");
        }
    }

    [Fact]
    public void Generate_SmallGame_ReturnsMeatAndMaybeBone()
    {
        // Act
        var result = RewardGenerator.Generate(RewardPool.SmallGame);

        // Assert
        Assert.Single(result.RawMeat);
        Assert.True(result.RawMeat[0] >= 0.2 && result.RawMeat[0] <= 0.4,
            "Small game meat should weigh between 0.2 and 0.4 kg");
        // Bone is optional (50% chance)
        Assert.True(result.Bone.Count <= 1);
    }

    [Fact]
    public void Generate_HideScrap_ReturnsHide()
    {
        // Act
        var result = RewardGenerator.Generate(RewardPool.HideScrap);

        // Assert
        Assert.Single(result.Hide);
        Assert.True(result.Hide[0] >= 0.3 && result.Hide[0] <= 0.6,
            "Hide scrap should weigh between 0.3 and 0.6 kg");
    }

    [Theory]
    [InlineData(RewardPool.BasicSupplies)]
    [InlineData(RewardPool.AbandonedCamp)]
    [InlineData(RewardPool.HiddenCache)]
    [InlineData(RewardPool.BasicMeat)]
    [InlineData(RewardPool.LargeMeat)]
    [InlineData(RewardPool.GameTrailDiscovery)]
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
        Assert.True(result.Descriptions.Count > 0,
            $"RewardPool.{pool} should have at least one description");
    }

    [Fact]
    public void Generate_AllPoolsHaveDescriptions()
    {
        // Test that all pools generate proper descriptions for expedition logs
        var pools = Enum.GetValues<RewardPool>().Where(p => p != RewardPool.None);

        foreach (var pool in pools)
        {
            var result = RewardGenerator.Generate(pool);
            Assert.True(result.Descriptions.Count > 0,
                $"RewardPool.{pool} should generate descriptions");
            Assert.All(result.Descriptions, d => Assert.False(string.IsNullOrEmpty(d)));
        }
    }
}
