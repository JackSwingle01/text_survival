using System.Text.RegularExpressions;

namespace Survival.Tests;

/// The player reads the game's names in words (Names, Content/names.json), never as code keys.
public class NamesTests
{
    static readonly AgentId P = AgentId.Player;

    static readonly Regex CodeKey = new("[a-z][A-Z]|_");

    [Fact]
    public void ANameReads_AsItsWords()
    {
        Assert.Equal("birch bark", Names.Of(Resource.BirchBark));
        Assert.Equal("Hand drill", Names.Title(ToolType.HandDrill));
        Assert.Equal("light snow", Names.Of(WeatherCondition.LightSnow));
    }

    /// names.json must name every value of every table it keeps: a value added in code with no words fails at load.
    [Fact]
    public void EveryValueOfEveryTable_HasWords()
    {
        foreach (var type in Names.Tables)
            foreach (Enum value in Enum.GetValues(type))
                Assert.False(string.IsNullOrWhiteSpace(Names.Of(value)), $"{type.Name}.{value}");
    }

    [Fact]
    public void ALoaderRefuses_ATableMissingAValue()
    {
        var files = Content.EmbeddedFiles();
        files["names.json"] = files["names.json"].Replace("\"BirchBark\": \"birch bark\",", "", StringComparison.Ordinal);
        Assert.Contains("names.json Resource: no words for BirchBark", Assert.Throws<InvalidDataException>(() => Content.Parse(files)).Message);
    }

    /// Every odds sheet Verbs.At offers at a stocked camp - the strike, each recipe's outcomes - names
    /// its base and every factor in words.
    [Fact]
    public void EveryOddsFactorOffered_IsWords()
    {
        var w = World.New(17);
        TestKit.Supply(w);
        Items.Pack(w, P).Gear.Add(Items.Make("hand_drill"));
        Sim.Step(w, new Wait(P), 1);
        var here = Verbs.At(w, P);
        var sheets = here.All.Select(v => v.Odds).Concat(here.All.SelectMany(v => v.Outcomes?.Outcomes.Select(o => o.Odds) ?? [])).OfType<Odds>().ToList();
        Assert.NotEmpty(sheets);
        foreach (var o in sheets)
        {
            Assert.DoesNotMatch(CodeKey, o.Base);
            Assert.All(o.Terms, t => Assert.DoesNotMatch(CodeKey, t.Factor));
        }
    }

    [Fact]
    public void AnOutcome_ReadsInWords()
    {
        var waste = Outcomes.Parse("fail:waste_material:Stone:1=.2").Single();
        Assert.Equal("Fails: the stone is wasted", waste.Words);
        var cut = Outcomes.Parse("succeed:injury:LeftArm|RightArm:0.03=.05");
        Assert.Equal(["Done, but you cut your left arm", "Done, but you cut your right arm"], cut.Select(o => o.Words));
        Assert.Equal("Fails: the striking tool loses 2 uses", Outcomes.Parse("fail:break_tool:Striking:2=.02").Single().Words);
        Assert.Equal("Done, but an extra sharp rock", Outcomes.Parse("succeed:extra_product:sharp_rock:1=.05").Single().Words);
    }

    /// A title-case name read inside a sentence is lower case throughout, not just its first letter.
    [Fact]
    public void AFind_ReadsInLowerCase()
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        Assert.Equal("You find stone pile.", Narration.ForPlayer(w, new PoolDiscovered(0, P, new PoolId(camp, 0), "Stone Pile"))!.Text);
    }
}
