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

    /// A raid lasts a minute, so nothing saves one today; this keeps one saveable if it ever lasts longer.
    [Fact]
    public void AHerdMidRaid_SavesAndLoads()
    {
        var w = World.New(17);
        var tile = Map.Step(Map.Of(w).Camp, Direction.West);
        Items.Add(Items.StashAt(w, tile, new Cause(AgentId.Player, "stashed by the player")), Resource.RawMeat, 5);
        var flock = Agents.SpawnGroup(w, Species.Raven, tile, 4, [tile])[0];
        w.Agent(flock).Doing = new Doing { Plain = new Raid(flock), MinutesLeft = 1 };

        var loaded = Save.FromJson(Save.ToJson(w));

        Assert.Equal(new Raid(flock), loaded.Agent(flock).Doing?.Activity);
        Assert.Equal(Save.ToJson(w), Save.ToJson(loaded));
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
    /// bedding, a crossing made, an NPC, a wolf pack, the player's memory, explored and remembered ground, rng
    /// streams, a journal, a made thing's parts and an unfinished one in the pack, a well-fed player and an NPC asleep. Not World.New: its two dozen herds would make the fixture half a megabyte.
    internal static World SampleWorld()
    {
        var map = Map.Generate(17);
        var camp = map.Camp;
        var w = new World { Seed = 17, GeneratedMap = map };
        w.Agents[P] = new Agent { Id = P, Tile = camp };
        Shelters.Place(w, camp, Shelters.CampOverhang);
        Shelters.PlaceBedding(w, camp, BeddingKind.Camp);
        TestKit.Supply(w);
        var npc = Npc.Spawn(w, camp);
        Agents.SpawnGroup(w, Species.Wolf, new TileId(camp.X + 12, camp.Y), 2); // a pack: two wolves that know each other
        Sim.Step(w, new Wait(P), 5);
        for (int i = 0; i < 10 && !(Fire.PitAt(w, camp) is FireId f && Fire.IsActive(w.Fires[f])); i++)
            Sim.Step(w, new StartFire(P, ToolType.FireStriker, Resource.BirchBark, 0.05, 1.0), 1);
        Sim.Step(w, new AddFuel(P, Resource.Birch, 2), 1);
        Sim.Step(w, new Stash(P, Resource.Pine, 2), 1);
        Sim.Step(w, new Travel(P, Direction.East), 60);
        Sim.Step(w, new Wait(P), 30);
        Sim.Step(w, new Travel(P, Direction.West), 60);
        Sim.Step(w, new Rest(P), 60);
        // A spear made, with what it is made of, and a second one half done in the pack.
        Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
        Sim.Step(w, new Make(P, "crude_spear"), Verbs.UntilDoneMinutes);
        Sim.Step(w, new Make(P, "crude_spear"), 8);
        // A good meal and a day without hunger behind it (Well fed), and the NPC asleep where it dropped.
        Body.Eat(w.Agent(P).Body, 2500);
        w.Agent(P).Body.MinutesWithoutHunger = 24 * 60;
        w.Agent(npc).Body.EnergyMinutes = 0;
        Effects.Set(w.Agent(npc).Body, EffectKind.Asleep, 40 / 90.0);
        ((NpcMind)w.Agent(npc).Mind).Goal = new NpcGoal(Stat.Warmth, Priority.Need, w.Minute, 90); // a committed goal, saved by name
        // A path trodden beside camp, and seen: a route in memory.
        w.Trails[new EdgeId(camp, new TileId(camp.X, camp.Y + 1))] = new EdgeTrodden(Tracks.PathAt + 0.05, w.Minute);
        Memories.Glance(w, w.Agent(P), []);
        Sim.Step(w, new Wait(P), 2);
        // A caribou on its way to water, the wolf it sensed kept in mind (Fauna.Needs): a stored plan and a sighting.
        var den = new TileId(camp.X + 12, camp.Y);
        var caribou = (HerdMind)w.Agent(Agents.Spawn(w, Species.Caribou, new TileId(camp.X + 15, camp.Y))).Mind;
        caribou.Plan = new Plan(Stat.Water, Priority.Need, new TileId(camp.X + 17, camp.Y), [new TileId(camp.X + 16, camp.Y), new TileId(camp.X + 17, camp.Y)]);
        caribou.Sightings = [new Sensed(Fauna.AnimalsAt(w, den).First().Id, den, w.Minute)];
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
