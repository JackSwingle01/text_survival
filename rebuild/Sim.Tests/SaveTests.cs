using System.Text.Json;

namespace Survival.Tests;

/// The save format: every save says which format it is in, and a fixed sample save, committed under
/// Fixtures/, must load and save again byte for byte, so a change to the format is always deliberate
/// (documentation/save-format.md).
public class SaveTests
{
    [Fact]
    public void ASave_CarriesTheFormatVersion()
    {
        using var doc = JsonDocument.Parse(Save.ToJson(World.New(17)));
        Assert.Equal(Save.FormatVersion, doc.RootElement.GetProperty("Version").GetInt32());
    }

    [Fact]
    public void ASaveInAnotherFormat_IsRefused_NamingBoth()
    {
        string json = Save.ToJson(World.New(17));
        string other = json.Replace($"{{\"Version\":{Save.FormatVersion},", $"{{\"Version\":{Save.FormatVersion + 1},");
        var e = Assert.Throws<JsonException>(() => Save.FromJson(other));
        Assert.Contains($"format {Save.FormatVersion + 1}", e.Message);
        Assert.Contains($"format {Save.FormatVersion}", e.Message.Replace($"format {Save.FormatVersion + 1}", ""));
    }

    [Fact]
    public void ASaveWithNoVersion_IsRefused()
    {
        var e = Assert.Throws<JsonException>(() => Save.FromJson("""{"Seed":17,"Minute":0}"""));
        Assert.Contains("no format version", e.Message);
    }
}

/// The committed sample save (Fixtures/sample-save.json), in the current format. Loading it and saving again must give the
/// same bytes: a field added, renamed, dropped or written differently fails here, and the fix is a
/// deliberate format change (documentation/save-format.md), never a quiet one.
public class SaveFixtureTests
{
    static readonly AgentId P = AgentId.Player;

    /// Set to regenerate the fixture from SampleWorld (after raising Save.FormatVersion).
    const string UpdateVariable = "SURVIVAL_UPDATE_SAVE_FIXTURE";

    static string FixturePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Sim.Tests.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "Fixtures", "sample-save.json");
    }

    /// A small world with a bit of most of what a save holds: a fire burning, a stash, a shelter and
    /// bedding, a crossing made, an NPC, a wolf pack, the player's memory and explored ground, rng
    /// streams, a journal. Not World.New: its two dozen herds would make the fixture half a megabyte.
    internal static World SampleWorld()
    {
        var map = Map.Generate(17);
        var camp = map.Camp;
        var w = new World { Seed = 17, GeneratedMap = map };
        w.Agents[P] = new Agent { Id = P, Tile = camp };
        Shelters.Place(w, camp, Shelters.CampOverhang);
        Shelters.PlaceBedding(w, camp, BeddingKind.Camp);
        TestKit.Supply(w);
        Npc.Spawn(w, camp);
        Agents.Spawn(w, Species.Wolf, new TileId(camp.X + 12, camp.Y), 2);
        Sim.Step(w, new Wait(P), 5);
        for (int i = 0; i < 10 && !(Fire.PitAt(w, camp) is FireId f && Fire.IsActive(w.Fires[f])); i++)
            Sim.Step(w, new StartFire(P, ToolType.FireStriker, Resource.BirchBark, 0.05, 1.0), 1);
        Sim.Step(w, new AddFuel(P, Resource.Birch, 2), 1);
        Sim.Step(w, new Stash(P, Resource.Pine, 2), 1);
        Sim.Step(w, new Travel(P, Direction.East), 60);
        Sim.Step(w, new Wait(P), 30);
        Sim.Step(w, new Travel(P, Direction.West), 60);
        Sim.Step(w, new Rest(P), 60);
        return w;
    }

    /// The fixture is kept indented so a format change reads as a diff; the tokens are the save's own.
    static string Indented(string json)
    {
        using var doc = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true })) doc.WriteTo(writer);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    [Fact]
    public void TheSampleSave_LoadsAndSavesAgain_ByteForByte()
    {
        string path = FixturePath();
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Indented(Save.ToJson(SampleWorld())));
        }
        Assert.True(File.Exists(path), $"No sample save at {path}: see documentation/save-format.md");
        string fixture = File.ReadAllText(path);
        Assert.True(fixture == Indented(Save.ToJson(Save.FromJson(fixture))),
            $"The save format changed: loading {Path.GetFileName(path)} and saving again gives different bytes. " +
            "Make the change deliberate: documentation/save-format.md.");
    }

    /// A loaded sample runs on like any world: the format holds everything the world needs.
    [Fact]
    public void TheSampleSave_RunsOn_AsTheWorldItWasSavedFrom()
    {
        var loaded = Save.FromJson(File.ReadAllText(FixturePath()));
        var again = Save.FromJson(File.ReadAllText(FixturePath()));
        Sim.Step(loaded, new Wait(P), 120);
        Sim.Step(again, new Wait(P), 60);
        Sim.Step(again, new Wait(P), 60);
        Assert.Equal(Save.ToJson(loaded), Save.ToJson(again));
    }
}
