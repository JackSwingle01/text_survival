using System.Text.Json;

namespace Survival.Tests;

/// The save format: every save says which format it is in (derived from the saved types), and a fixed sample world, committed under
/// Fixtures/, must load and save again byte for byte, so a change to the format is always deliberate
/// (documentation/save-format.md).
public class SaveTests
{
    [Fact]
    public void ASave_CarriesTheFormat()
    {
        using var doc = JsonDocument.Parse(Save.ToJson(World.New(17)));
        Assert.Equal(Save.Format, doc.RootElement.GetProperty("Format").GetString());
    }

    [Fact]
    public void ASaveInAnotherFormat_IsRefused_NamingBoth()
    {
        string json = Save.ToJson(World.New(17));
        string other = json.Replace($"{{\"Format\":\"{Save.Format}\",", "{\"Format\":\"0123456789ab\",");
        var e = Assert.Throws<JsonException>(() => Save.FromJson(other));
        Assert.Contains("format 0123456789ab", e.Message);
        Assert.Contains($"format {Save.Format}", e.Message);
    }

    [Fact]
    public void ASaveWithNoFormat_IsRefused()
    {
        var e = Assert.Throws<JsonException>(() => Save.FromJson("""{"Seed":17,"Minute":0}"""));
        Assert.Contains("no format", e.Message);
    }

    [Fact]
    public void ASaveInAnOldNumberedFormat_IsRefused()
    {
        var e = Assert.Throws<JsonException>(() => Save.FromJson("""{"Version":27,"World":{}}"""));
        Assert.Contains("numbered format 27", e.Message);
        Assert.Contains($"format {Save.Format}", e.Message);
    }

    record ToyV1(int Minute, string Name);
    record ToyV2(int Minute, string Name, double SnowM);
    record ToyRenamed(int Minute, string Label);
    enum ToyKind { Hare, Wolf }
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ToyNamed>))]
    enum ToyNamed { Calm, Storm }
    record ToyHolder(ToyKind Kind, ToyNamed Named, Dictionary<AgentId, List<ToyV1>> ByAgent, Activity? Doing);

    static string Describe(Type t) => Save.Describe(t, new JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() });

    /// The format is the saved types' shape: what the serializer would read differently changes it.
    [Fact]
    public void TheFormat_IsTheShapeOfTheSavedTypes()
    {
        string v1 = Describe(typeof(ToyV1)).Replace("ToyV1", "Toy");
        Assert.NotEqual(v1, Describe(typeof(ToyV2)).Replace("ToyV2", "Toy"));      // a field added
        Assert.NotEqual(v1, Describe(typeof(ToyRenamed)).Replace("ToyRenamed", "Toy")); // a field renamed
        Assert.Equal(v1, Describe(typeof(ToyV1)).Replace("ToyV1", "Toy"));          // and nothing else moves it
    }

    /// The description reaches through collections, nullables, enums, polymorphic bases and custom converters.
    [Fact]
    public void TheFormat_ReachesEverySavedType()
    {
        string d = Describe(typeof(ToyHolder));
        Assert.Contains("enum Hare=0,Wolf=1\n", d);                // enum names and numbers
        Assert.Contains("enum Calm=0,Storm=1 via System.Text.Json.Serialization.JsonStringEnumConverter`1[", d); // and its own converter
        Assert.Contains("converter Survival.AgentIdConverter", d); // a type written by its own converter
        Assert.Contains("raid=Survival.Raid", d);                 // a derived activity by its discriminator
        Assert.Contains("Minute:System.Int32", d);                // the element type inside a map of lists
    }

    [Fact]
    public void TheFormat_ChangesWithAChangeOfMeaning()
    {
        string d = Describe(typeof(ToyV1));
        Assert.NotEqual(Save.Fingerprint(d, "#1 a"), Save.Fingerprint(d, "#2 b"));
        Assert.Equal(Save.Fingerprint(d, "#1 a"), Save.Fingerprint(d, "#1 a"));
    }

    /// A raid lasts a minute, so nothing saves one today; this keeps one saveable if it ever lasts longer.
    [Fact]
    public void AHerdMidRaid_SavesAndLoads()
    {
        var w = World.New(17);
        var tile = Map.Step(Map.Of(w).Camp, Direction.West);
        TestKit.Give(Items.PileAt(w, tile, new Cause(AgentId.Player, "put down by the player")), Resource.RawMeat, 5);
        var raven = Agents.SpawnGroup(w, Species.Raven, tile, 4)[0];
        w.Agent(raven).Doing = new Doing { Plain = new Raid(raven), MinutesLeft = 1 };

        var loaded = Save.FromJson(Save.ToJson(w));

        Assert.Equal(new Raid(raven), loaded.Agent(raven).Doing?.Activity);
        Assert.Equal(Save.ToJson(w), Save.ToJson(loaded));
    }
}

/// The committed sample save (Fixtures/sample-save.json), in the current format. Loading it and saving again must give the
/// same bytes: a field added, renamed, dropped or written differently fails here, and the fix is a
/// deliberate format change (documentation/save-format.md), never a quiet one.
public class SaveFixtureTests
{
    static readonly AgentId P = AgentId.Player;

    /// Set to regenerate the fixture from SampleWorld (after a format change).
    const string UpdateVariable = "SURVIVAL_UPDATE_SAVE_FIXTURE";

    static string FixturePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Sim.Tests.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "Fixtures", "sample-save.json");
    }

    /// A small world with a bit of most of what a save holds: a fire burning, a pile, a shelter and
    /// bedding, a crossing made, an NPC, a wolf pack, the player's memory, explored and remembered ground, rng
    /// streams, a journal, a made thing's parts and an unfinished one in the pack, meat half cooked at the fire, a well-fed, dirty player and an NPC asleep. Not World.New: its two dozen herds would make the fixture half a megabyte.
    internal static World SampleWorld()
    {
        var map = Map.Generate(17);
        var camp = map.Camp;
        var w = new World { Seed = 17, GeneratedMap = map };
        w.Agents[P] = new Agent { Id = P, Tile = camp };
        var shelter = Shelters.Place(w, camp, Shelters.CampOverhang);
        (shelter.WorkingPiece, shelter.WorkingPieceStat, shelter.WorkingPieceLeftPct) = (Resource.Hide, ShelterStat.Insulation, 0.5); // a hide half worked in
        Shelters.PlaceBedding(w, camp, BeddingKind.Camp);
        TestKit.Supply(w);
        var npc = Npc.Spawn(w, camp);
        Agents.SpawnGroup(w, Species.Wolf, new TileId(camp.X + 12, camp.Y), 2); // a pack: two wolves that know each other
        Sim.Step(w, new Wait(P), 5);
        for (int i = 0; i < 10 && !(Fire.PitAt(w, camp) is FireId f && Fire.IsActive(w.Fires[f])); i++)
            Sim.Step(w, new StartFire(P, ToolType.FireStriker, Resource.BirchBark, 0.05, 1.0), 1);
        Sim.Step(w, new AddFuel(P, Resource.BirchBranch, 2), 1);
        Sim.Step(w, new PutDown(P, Resource.PineBranch, 2), 1);
        Sim.Step(w, new Travel(P, Direction.East), 60);
        Sim.Step(w, new Wait(P), 30);
        Sim.Step(w, new Travel(P, Direction.West), 60);
        Sim.Step(w, new Rest(P), 60);
        // A spear made, with what it is made of, and a second one half done in the pack.
        Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
        Sim.Step(w, new Make(P, "crude_spear"), Verbs.UntilDoneMinutes);
        Sim.Step(w, new Make(P, "crude_spear"), 8);
        // A piece of meat half cooked at the fire (Stations).
        Items.AddPiece(Items.Pack(w, P), Resource.RawMeat, 0.4);
        Sim.Step(w, new PutOn(P, "cook_meat"), 1);
        Sim.Step(w, new Wait(P), 6);
        // A good meal and a day without hunger behind it (Well fed), and the NPC asleep where it dropped.
        Body.Eat(w.Agent(P).Body, 2500);
        w.Agent(P).Body.MinutesWithoutHunger = 24 * 60;
        w.Agent(npc).Body.EnergyMinutes = 0;
        Effects.Set(w.Agent(npc).Body, EffectKind.Asleep, 40 / 90.0);
        Effects.Set(w.Agent(P).Body, EffectKind.Dirty, 0.3);
        ((NpcMind)w.Agent(npc).Mind).Plan = new NpcPlan(Stat.Warmth, Priority.Need, w.Minute, 90); // a committed plan, saved by name
        // A path trodden beside camp, and seen: a route in memory.
        w.Trails[new EdgeId(camp, new TileId(camp.X, camp.Y + 1))] = new EdgeTrodden(Tracks.PathAt + 0.05, w.Minute);
        Memories.Glance(w, w.Agent(P), []);
        Sim.Step(w, new Wait(P), 2);
        // A caribou on its way to water, the wolf it sensed kept in mind (Fauna.Needs): a stored plan and a sighting.
        var den = new TileId(camp.X + 12, camp.Y);
        var caribou = (HerdMind)w.Agent(Agents.Spawn(w, Species.Caribou, new TileId(camp.X + 15, camp.Y))).Mind;
        caribou.Plan = new Plan(Stat.Water, Priority.Need, new TileId(camp.X + 17, camp.Y), [new TileId(camp.X + 16, camp.Y), new TileId(camp.X + 17, camp.Y)]);
        caribou.Sightings = [new Sensed(Fauna.AnimalsAt(w, den).First().Id, den, w.Minute)];
        // The pack's wolf has the caribou in sight as quarry (Fauna.Sense): a sighting of a herd, by species and count.
        Fauna.MindOf(Fauna.AnimalsAt(w, den).First()).Sightings = [new Sensed(null, new TileId(camp.X + 15, camp.Y), w.Minute, Quarry: Species.Caribou, Count: 1)];
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
            File.WriteAllText(path, Indented(Save.WorldToJson(SampleWorld())));
        }
        Assert.True(File.Exists(path), $"No sample save at {path}: see documentation/save-format.md");
        string fixture = File.ReadAllText(path);
        Assert.True(fixture == Indented(Save.WorldToJson(Save.WorldFromJson(fixture))),
            $"The save format changed: loading {Path.GetFileName(path)} and saving again gives different bytes. " +
            "Make the change deliberate: documentation/save-format.md.");
    }

    /// A loaded sample runs on like any world: the format holds everything the world needs.
    [Fact]
    public void TheSampleSave_RunsOn_AsTheWorldItWasSavedFrom()
    {
        var loaded = Save.WorldFromJson(File.ReadAllText(FixturePath()));
        var again = Save.WorldFromJson(File.ReadAllText(FixturePath()));
        Sim.Step(loaded, new Wait(P), 120);
        Sim.Step(again, new Wait(P), 60);
        Sim.Step(again, new Wait(P), 60);
        Assert.Equal(Save.ToJson(loaded), Save.ToJson(again));
    }
}
