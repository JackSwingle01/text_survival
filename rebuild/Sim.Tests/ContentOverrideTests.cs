namespace Survival.Tests;

/// The tuner's way in: numbers changed in memory before the tables are read (Content.Override,
/// ContentOverrides). The game loads with none.
public class ContentOverrideTests
{
    static ContentTables With(params (string Path, double Value)[] overrides) =>
        Content.Parse(Content.Override(Content.EmbeddedFiles(), overrides.ToDictionary(o => o.Path, o => o.Value)));

    [Fact]
    public void NoOverrides_LeaveEveryFileAsEmbedded()
    {
        var files = Content.EmbeddedFiles();
        var same = Content.Override(files, new Dictionary<string, double>());
        Assert.Equal(files, same);
        Assert.Empty(ContentOverrides.Applied);
    }

    [Fact]
    public void Overrides_ChangeOnlyTheNamedNumbers()
    {
        var t = With(("species.json:species.Wolf.boldness", 0.25), ("species.json:species.Wolf.moveAllowanceM", 9),
            ("items.json:gear.wooden_spear.weapon.damage", 0.8), ("species.json:sightings.alert.1", 3.5));
        Assert.Equal(0.25, t.Species[Species.Wolf].Boldness);
        Assert.Equal(9, t.Species[Species.Wolf].MoveAllowanceM);
        Assert.Equal(0.8, t.Gear["wooden_spear"].Weapon!.Damage);
        Assert.Equal(3.5, t.Sightings.Alert[1]);
        Assert.Equal(Content.Tables.Species[Species.Bear], t.Species[Species.Bear]);
        Assert.Equal(Content.Tables.Species[Species.Wolf].Threat, t.Species[Species.Wolf].Threat);
        Assert.Equal(Content.Tables.Gear["stone_axe"].Weapon, t.Gear["stone_axe"].Weapon);
    }

    [Fact]
    public void ADoubleWrittenWhole_TakesAFraction() =>
        Assert.Equal(1.5, With(("species.json:species.Wolf.cohesion", 1.5)).Species[Species.Wolf].Cohesion);

    [Theory]
    [InlineData("species.json:species.Wolf.bravery")] // no such field
    [InlineData("species.json:species.Wolff.boldness")] // no such row
    [InlineData("species.json:species.Wolf.temperament")] // not a number
    [InlineData("species.json:sightings.alert.9")] // past the end
    [InlineData("nothing.json:a.b")] // no such file
    [InlineData("species.Wolf.boldness")] // no file
    public void APathThatNamesNoNumber_Throws(string path) =>
        Assert.Throws<InvalidDataException>(() => Content.Override(Content.EmbeddedFiles(), new Dictionary<string, double> { [path] = 1 }));

    [Fact]
    public void AFractionForAWholeNumberField_FailsTheTablesChecks() =>
        Assert.Throws<InvalidDataException>(() => With(("species.json:species.Wolf.moveAllowanceM", 4.5)));

    [Fact]
    public void AnOverrideStillMeetsTheTablesChecks() =>
        Assert.Throws<InvalidDataException>(() => With(("species.json:species.Wolf.fatPct", 0.9), ("species.json:species.Wolf.musclePct", 0.9)));

    [Fact]
    public void SettingOverridesAfterContentHasLoaded_Throws()
    {
        _ = Content.Tables;
        Assert.Throws<InvalidOperationException>(() => ContentOverrides.Set(new Dictionary<string, double> { ["species.json:species.Wolf.boldness"] = 1 }));
    }
}
