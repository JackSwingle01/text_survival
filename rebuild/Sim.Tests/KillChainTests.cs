using Survival;
using Xunit;

/// Emergent-world build step 3, part A (the kill chain): stable ids, who killed what, the body and
/// carcass pipeline, herd renewal, the carrion flock, the herds' access gate, and the caches that
/// make whole-map passes cheap without changing a result.
public class KillChainTests
{
    static readonly AgentId P = AgentId.Player;

    /// Seed 17 with nothing else alive in it but the player, in a fixed sky (41 °F: rot at its base rate).
    static World Bare(double tempC = 5, int seed = 17)
    {
        var w = World.New(seed);
        w.FixedWeather = new(0, WeatherCondition.Cloudy, tempC, 0.1, 0, 0.8, WindDirection.North);
        foreach (var id in w.Agents.Keys.Where(id => id != P).ToList()) Agents.Remove(w, id);
        return w;
    }

    static Carcass Caribou(World w, TileId tile, Cause? cause = null) =>
        Carcasses.Create(w, tile, "Caribou", SpeciesTable.NewBody(Species.Caribou, w.Rng("test")), w.Minute, cause: cause);

    static long Days(double d) => (long)(d * 1440);

    // ---- stable ids ----

    [Fact]
    public void A_Stash_BegunAgain_IsANewThing()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        Items.Add(Items.StashAt(w, camp, new Cause(P, "stashed by the player")), Resource.Pine, 1);
        var first = Things.StashAt(w, camp)!.Value;
        Assert.Equal(1, Items.TakeFromStash(w, camp, Resource.Pine, 1));
        Assert.False(Things.Exists(w, first));
        Items.Add(Items.StashAt(w, camp, new Cause(P, "stashed by the player")), Resource.Oak, 1);
        var second = Things.StashAt(w, camp)!.Value;
        Assert.NotEqual(first, second);
        Assert.False(Things.Exists(w, first), "a held ref to the old stash never names the new one");
        var loaded = Save.FromJson(Save.ToJson(w));
        Assert.Equal(second, Things.StashAt(loaded, camp));
        Assert.Equal(w.NextStashId, loaded.NextStashId);
    }

    [Fact]
    public void A_Shelter_PlacedAgain_IsANewThing()
    {
        var w = Bare();
        var tile = Map.Step(Map.Of(w).Camp, Direction.East);
        Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
        var first = Things.ShelterAt(w, tile)!.Value;
        Shelters.Remove(w, tile);
        Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
        var second = Things.ShelterAt(w, tile)!.Value;
        Assert.NotEqual(first, second);
        Assert.False(Things.Exists(w, first));
        Assert.True(Things.Exists(w, second));
        Assert.Equal(second, Things.ShelterAt(Save.FromJson(Save.ToJson(w)), tile));
    }

    // ---- who killed what ----

    /// Strikes until a blow lands (each seed a fresh world), then hands the fight back.
    static (World W, Encounter Enc, AgentId Prey) Struck(Func<World, TileId, (Encounter Enc, AgentId Prey, CombatUnit Striker, CombatAction Act)> setup)
    {
        for (int seed = 1; seed < 200; seed++)
        {
            var w = Bare(seed: seed);
            var tile = Map.Of(w).Camp;
            var (enc, prey, striker, act) = setup(w, tile);
            var hit = Combat.Apply(w, enc, striker, act, []);
            if (hit.Hit && hit.Damage != null) return (w, enc, prey);
            w.Encounters.Clear();
        }
        throw new InvalidOperationException("no blow landed in 200 tries");
    }

    /// Review: a thrown spear was taken out of the thrower's hands before the aftermath read it, so the
    /// kill was put down to "the player", not the spear.
    [Fact]
    public void A_ThrownSpear_IsNamedOnTheKill()
    {
        var (w, enc, prey) = Struck((w, tile) =>
        {
            var caribou = Agents.Spawn(w, Species.Caribou, tile, 1);
            var spear = Items.Make("wooden_spear");
            Items.Pack(w, P).Gear.Add(spear);
            var e = Combat.Create(w, [new Fighter(P) { Weapon = spear }], [new Fighter(caribou)], tile, 10, Awareness.Engaged, Awareness.Unaware);
            return (e, caribou, Combat.UnitOf(e, P), CombatAction.Throw);
        });
        Assert.Null(Combat.UnitOf(enc, P).Weapon); // thrown: it is on the ground
        w.Agent(prey).Body.BloodPct = 0;
        Combat.Finish(w, enc, []);
        var c = w.Tiles[enc.Tile].Carcasses!.Single();
        Assert.Equal(new Cause(P, "the player's wooden spear"), c.Cause);
    }

    /// Review: a side's dead were put down to its first fighter, so an ally's kill was the player's.
    [Fact]
    public void An_AllysKill_IsTheAllys()
    {
        var (w, enc, prey) = Struck((w, tile) =>
        {
            var caribou = Agents.Spawn(w, Species.Caribou, tile, 1);
            var npc = Npc.Spawn(w, tile);
            var e = Combat.Create(w, [new Fighter(P), new Fighter(npc)], [new Fighter(caribou)], tile, 1, Awareness.Engaged, Awareness.Unaware);
            return (e, caribou, Combat.UnitOf(e, npc), CombatAction.Attack);
        });
        w.Agent(prey).Body.BloodPct = 0;
        Combat.Finish(w, enc, []);
        var c = w.Tiles[enc.Tile].Carcasses!.Single();
        Assert.NotEqual(P, c.Cause!.Value.Who);
        Assert.Equal("a person", c.Cause!.Value.What);
    }

    /// Review: an animal that bled out after a fight was "its wounds", naming nobody.
    [Fact]
    public void An_AnimalThatBleedsOut_NamesItsHunter()
    {
        var (w, enc, prey) = Struck((w, tile) =>
        {
            var caribou = Agents.Spawn(w, Species.Caribou, tile, 1);
            var spear = Items.Make("wooden_spear");
            Items.Pack(w, P).Gear.Add(spear);
            var e = Combat.Create(w, [new Fighter(P) { Weapon = spear }], [new Fighter(caribou)], tile, 1, Awareness.Engaged, Awareness.Unaware);
            return (e, caribou, Combat.UnitOf(e, P), CombatAction.Attack);
        });
        w.Encounters.Remove(enc); // the fight is over and the caribou walked away from it
        var body = w.Agent(prey).Body;
        Assert.Equal(new Cause(P, "the player's wooden spear"), body.WoundedBy);
        body.BloodPct = 0;
        Sim.Advance(w, 1);
        var c = w.Tiles.Values.SelectMany(t => t.Carcasses ?? []).Single();
        Assert.Equal(new Cause(P, "wounds from the player's wooden spear"), c.Cause);
    }

    [Fact]
    public void A_DeadPerson_KeepsWhatTheyDiedOf()
    {
        var w = Bare();
        var npc = Npc.Spawn(w, Map.Of(w).Camp);
        var body = w.Agent(npc).Body;
        body.WoundedBy = new Cause(new AgentId(999), "a wolf pack");
        body.BloodPct = 0.52;
        Effects.Add(body, EffectKind.Bleeding, 1.0);
        for (int i = 0; i < 24 * 60 && Body.IsAlive(body); i++) Sim.Advance(w, 1);
        Assert.False(Body.IsAlive(body));
        Assert.Equal(new Cause(new AgentId(999), "a wolf pack"), body.DeathCause);
        Assert.NotNull(body.DiedMinute);
        var sign = Signs.At(w, w.Agent(npc).Tile).Single(s => s.Kind == SignKind.Body);
        Assert.Equal(body.DeathCause, sign.Cause);
        Assert.Equal(body.DiedMinute, sign.MadeMinute);

        var cold = BodyState.BaselineHuman();
        LiveUntilDead(cold, b => b.CoreTempF = 80);
        Assert.Equal("the cold", Body.CauseOfDeath(cold).What);
        var starved = BodyState.BaselineHuman();
        double spentKg = starved.StructuralKg / 0.82; // fat and muscle burned down to their floors (3% and 15%)
        (starved.FatKg, starved.MuscleKg) = (0.03 * spentKg, 0.15 * spentKg);
        LiveUntilDead(starved, b => b.CaloriesKcal = 0);
        Assert.Equal("starvation", Body.CauseOfDeath(starved).What);
    }

    /// Lives a body in a mild room, minute by minute, until it dies: each runs before every minute.
    static void LiveUntilDead(BodyState body, Action<BodyState> each)
    {
        var rng = Rng.ForSystem(1, "body");
        for (int m = 0; m < 10 * 1440 && Body.IsAlive(body); m++)
        {
            each(body);
            Body.Update(body, new SurvivalContext { AirTempF = 70 }, rng);
        }
        Assert.False(Body.IsAlive(body), "lived ten days");
    }

    /// Review: seed 99's NPC froze carrying six kilograms of fat at a core of 92.5 °F and was said to
    /// have starved, because its day's calories were gone. The cause is the damage that crossed the line
    /// (body review B5): exhaustion is not one, since effects never kill.
    [Fact]
    public void A_Death_IsNamedForWhatKilled_NotForAnEmptyStomach()
    {
        BodyState Seed99()
        {
            var b = BodyState.BaselineHuman();
            (b.CoreTempF, b.FatKg, b.MuscleKg, b.HydrationMl, b.CaloriesKcal, b.EnergyMinutes) = (92.5, 6.38, 22.5, 1923, 0, 20);
            Effects.Add(b, EffectKind.Hypothermia, 0.24);
            Effects.Add(b, EffectKind.Hungry, 0.98);
            Effects.Add(b, EffectKind.Tired, 0.98);
            return b;
        }
        var cold = Seed99();
        LiveUntilDead(cold, b => { b.CoreTempF = 86; b.CaloriesKcal = 0; });
        Assert.Equal("the cold", Body.CauseOfDeath(cold).What); // fat left to burn: hunger is not killing it

        var spent = Seed99();
        spent.CoreTempF = Body.BaseCoreTempF;
        spent.EnergyMinutes = 0;
        var rng = Rng.ForSystem(1, "body");
        for (int m = 0; m < 6 * 60; m++)
        {
            spent.EnergyMinutes = 0;
            Body.Update(spent, new SurvivalContext { AirTempF = 70 }, rng);
        }
        Assert.True(Body.IsAlive(spent)); // worn out on its feet, not dead of it

        var dry = Seed99();
        LiveUntilDead(dry, b => { b.CoreTempF = 92.5; b.HydrationMl = 0; }); // thirst does more harm an hour than mild cold
        Assert.Equal("thirst", Body.CauseOfDeath(dry).What);

        var bled = Seed99();
        bled.CoreTempF = Body.BaseCoreTempF;
        bled.BloodPct = 0.55;
        Effects.Add(bled, EffectKind.Bleeding, 0.5);
        LiveUntilDead(bled, _ => { });
        Assert.Equal("blood loss", Body.CauseOfDeath(bled).What);
    }

    // ---- the body and carcass pipeline ----

    static RemainsStage StageAt(World w, TileId tile, int id) =>
        Carcasses.Find(w, tile, id) is { } c ? Remains.StageOf(w, tile, c) : RemainsStage.Gone;

    [Fact]
    public void A_Carcass_RotsToBonesThenIsGone_KeepingItsCause()
    {
        var w = Bare(tempC: 5); // 41 °F: one effective hour an hour
        var camp = Map.Of(w).Camp;
        var cause = new Cause(new AgentId(999), "a wolf pack");
        var c = Caribou(w, camp, cause);
        double bonesAt = Remains.SkeletalHours(c.BodyWeightKg);
        Sim.Advance(w, (int)(bonesAt * 60) - 120);
        Assert.Equal(RemainsStage.Flesh, StageAt(w, camp, c.Id));
        Assert.True(Carcasses.MeatKg(c) > 0);
        Sim.Advance(w, 240);
        Assert.Equal(RemainsStage.Bones, StageAt(w, camp, c.Id));
        Assert.Equal(0, Carcasses.MeatKg(c)); // the hour's settling rotted the flesh off the ledger
        Assert.True(c.Remaining[Resource.Bone] > 0);
        Assert.Equal(cause, c.Cause);
        var sign = Signs.At(w, camp).Single(s => s.Thing.Kind == ThingKind.Carcass);
        Assert.Equal(SignKind.Bones, sign.Kind);
        Assert.Equal(cause, sign.Cause);
        Assert.Contains(Report.Finds(w), f => f.Kind == "bones" && f.Tile == camp && f.Category == FindCategory.DeadMatter);
        Assert.False(Tags.Of(w, new ThingRef(ThingKind.Carcass, camp, c.Id)).HasFlag(Tag.Edible));
        Sim.Advance(w, (int)((Remains.GoneHours - bonesAt) * 60) + 60);
        Assert.Equal(RemainsStage.Gone, StageAt(w, camp, c.Id));
        Assert.Null(w.Tiles.GetValueOrDefault(camp)?.Carcasses);
    }

    /// The frozen traveller: the same carcass kept by a hard frost is still flesh long after one at a thaw is bones.
    [Fact]
    public void Frost_KeepsACarcass_FarLonger()
    {
        var thaw = Bare(tempC: 5);
        var frost = Bare(tempC: -25); // -13 °F: a tenth of the rot
        var a = Caribou(thaw, Map.Of(thaw).Camp);
        var b = Caribou(frost, Map.Of(frost).Camp);
        int days = (int)Math.Ceiling(Remains.SkeletalHours(a.BodyWeightKg) / 24) + 1;
        Sim.Advance(thaw, days * 1440);
        Sim.Advance(frost, days * 1440);
        Assert.Equal(RemainsStage.Bones, StageAt(thaw, Map.Of(thaw).Camp, a.Id));
        Assert.Equal(RemainsStage.Flesh, StageAt(frost, Map.Of(frost).Camp, b.Id));
        Assert.True(Carcasses.EffectiveHoursNow(frost, Map.Of(frost).Camp, b) < Carcasses.EffectiveHoursNow(thaw, Map.Of(thaw).Camp, a) / 5);
    }

    /// Scavengers speed it: a carcass stripped of its meat goes to bones far sooner than a whole one.
    [Fact]
    public void A_StrippedCarcass_GoesToBonesSooner()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var whole = Caribou(w, camp);
        var stripped = Caribou(w, camp);
        Carcasses.EatMeat(stripped, Carcasses.MeatKg(stripped) * 0.95);
        Sim.Advance(w, 3 * 1440);
        Assert.Equal(RemainsStage.Flesh, StageAt(w, camp, whole.Id));
        Assert.Equal(RemainsStage.Bones, StageAt(w, camp, stripped.Id));
    }

    /// Review: cutting lowered the flesh left, which aged a two-day-old carcass to bones half-way
    /// through the butchering; the rot then took the uncut hide and sinew and the cut pieces waiting on
    /// it, about twenty kilograms, while YieldKg still counted them. Cutting is not stripping: every
    /// kilogram the carcass yields is in the pack or still waiting on it, and nothing rots uncut.
    [Fact]
    public void Butchering_AnOlderCarcass_KeepsEverythingItCut()
    {
        var w = Bare(); // 41 °F: an effective hour an hour
        w.Minute = Days(2);
        var camp = Map.Of(w).Camp;
        var player = w.Agent(P);
        var pack = Items.Pack(w, P);
        pack.Gear.Add(Items.Make("flint_knife"));
        var c = Carcasses.Create(w, camp, "Caribou", SpeciesTable.NewBody(Species.Caribou, w.Rng("test")), w.Minute - Days(2));
        var onIt = new Dictionary<Resource, double>(c.Remaining);
        var before = Enum.GetValues<Resource>().ToDictionary(r => r, r => Items.Have(pack, r));
        string? stopped = null;
        for (int i = 0; i < 60 && Carcasses.Find(w, camp, c.Id) != null && stopped != Work.PackFull; i++)
        {
            (player.Body.HydrationMl, player.Body.CaloriesKcal, player.Body.EnergyMinutes) = (3000, 2000, 900);
            stopped = Sim.Step(w, new Butcher(P, c.Id, ButcherMode.Careful), 15).InterruptedReason;
        }
        Assert.True(Carcasses.AllCut(c), $"stopped: {stopped}");
        foreach (var (r, kg) in onIt)
            Assert.True(Math.Abs(c.CutKg.GetValueOrDefault(r) - kg) < 1e-6, $"{r}: {kg:0.00} kg on it, {c.CutKg.GetValueOrDefault(r):0.00} cut; the rest rotted uncut");
        foreach (var (r, kg) in c.YieldKg)
            Assert.Equal(kg, Items.Have(pack, r) - before[r] + c.Pending.GetValueOrDefault(r), 6);
        Assert.True(c.YieldKg.GetValueOrDefault(Resource.Hide) > 0);
        // What waits on the carcass is the butcher's, however long it waits and however far it rots.
        var waiting = new Dictionary<Resource, double>(c.Pending);
        Sim.Advance(w, (int)(Remains.SkeletalHours(c.BodyWeightKg) * 60));
        Assert.Equal(RemainsStage.Bones, StageAt(w, camp, c.Id));
        Assert.Equal(waiting, c.Pending);
    }

    [Fact]
    public void A_DeadPerson_GoesToASkeletonThenBones_AndLeavesTheWorld()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        var dead = w.Agent(npc);
        var cause = new Cause(null, "the cold");
        dead.Body.BloodPct = 0;
        Body.MarkDead(dead.Body, w.Minute, cause);
        double skeletal = Remains.SkeletalHours(dead.Body.WeightKg);
        Assert.Equal(RemainsStage.Flesh, Remains.StageOf(w, dead));
        Assert.True(Tags.Of(w, Things.OfAgent(w, npc)).HasFlag(Tag.ScentSource));
        Sim.Advance(w, (int)(skeletal * 60) + 60);
        Assert.Equal(RemainsStage.Skeletal, Remains.StageOf(w, dead));
        Assert.True(w.Agents.ContainsKey(npc));
        Sim.Advance(w, (int)(Remains.ScatterHours * 60) + 60);
        Assert.False(w.Agents.ContainsKey(npc), "scattered: the person has left the world's agents");
        var bones = w.Tiles[camp].Carcasses!.Single(c => c.Animal == "Human");
        Assert.Equal(cause, bones.Cause);
        Assert.Equal(RemainsStage.Bones, Remains.StageOf(w, camp, bones));
        Assert.Contains(w.Tiles[camp].Stash!.Gear, g => g.Kind == "stone_axe"); // what they carried lies beside them
        Assert.Equal(npc, w.Tiles[camp].StashCause!.Value.Who);
    }

    /// Old bones can weather away while someone works on them: the work stops, nothing breaks.
    [Fact]
    public void Bones_WeatheringAwayUnderTheKnife_EndTheWork()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
        var bones = Carcasses.Bones(w, camp, "Bison", 800, 60, w.Minute, new Cause(null, "a wolf pack"), Remains.GoneHours - 0.5);
        var r = Sim.Step(w, new Butcher(P, bones.Id), 180);
        Assert.Equal("there's nothing left to butcher", r.InterruptedReason);
        Assert.Null(Carcasses.Find(w, camp, bones.Id));
    }

    [Fact]
    public void The_PlayersBody_StaysInTheWorld()
    {
        var w = Bare();
        var player = w.Agent(P);
        player.Body.BloodPct = 0;
        Body.MarkDead(player.Body, w.Minute, new Cause(null, "the cold"));
        Sim.Advance(w, 40 * 1440);
        Assert.True(w.Agents.ContainsKey(P));
    }

    /// Rot, scattering and feeding are settled on the clock: the same world however it is stepped,
    /// and through a save and load.
    [Fact]
    public void Remains_AreTheSame_HoweverTheClockIsStepped()
    {
        World Make()
        {
            var w = Bare();
            var camp = Map.Of(w).Camp;
            Caribou(w, camp);
            var npc = Npc.Spawn(w, Map.Step(camp, Direction.East));
            w.Agent(npc).Body.BloodPct = 0;
            Body.MarkDead(w.Agent(npc).Body, w.Minute, new Cause(null, "the cold"));
            Agents.Spawn(w, Species.Raven, Map.Step(Map.Step(camp, Direction.North), Direction.North), 4, [Map.Step(Map.Step(camp, Direction.North), Direction.North), camp]);
            return w;
        }
        var whole = Make();
        Sim.Advance(whole, 25 * 1440);
        var stepped = Make();
        Sim.Advance(stepped, 3 * 1440 + 17);
        stepped = Save.FromJson(Save.ToJson(stepped));
        for (int i = 0; i < 7; i++) Sim.Advance(stepped, 1000);
        Sim.Advance(stepped, 25 * 1440 - (3 * 1440 + 17) - 7000);
        Assert.Equal(Save.ToJson(whole), Save.ToJson(stepped));
    }

    // ---- renewal ----

    /// Day-of-year to world minute, at the hour the clock starts (09:00).
    static long MinuteOfDayOfYear(int dayOfYear) => (dayOfYear - 182 + 365) % 365 * 1440L;

    static AgentId CaribouHerd(World w, int members)
    {
        var land = Fauna.MindOf(World.New(17).Agents.Values.First(a => a.Species == Species.Caribou)).Territory;
        return Agents.Spawn(w, Species.Caribou, land[0], members, [.. land]);
    }

    [Fact]
    public void Prey_CalveInTheirSeason_TowardWhatTheLandCarries()
    {
        var w = Bare();
        w.Minute = MinuteOfDayOfYear(140);
        var herd = CaribouHerd(w, 3);
        double capacity = Fauna.CapacityOf(w, w.Agent(herd));
        Assert.True(capacity > 3, $"capacity {capacity:0.0}");
        for (int d = 0; d < 40; d++)
        {
            Sim.Advance(w, 1440);
            Assert.InRange(w.Agent(herd).Members.Count, 3, Math.Max(3, (int)Math.Floor(capacity)));
        }
        Assert.True(w.Agent(herd).Members.Count > 3, "nothing born in forty days of the calving season");

        var off = Bare();
        off.Minute = MinuteOfDayOfYear(250);
        var idle = CaribouHerd(off, 3);
        Sim.Advance(off, 40 * 1440);
        Assert.Equal(3, off.Agent(idle).Members.Count);
    }

    [Fact]
    public void A_HerdAtCapacity_HasNoYoung()
    {
        var w = Bare();
        w.Minute = MinuteOfDayOfYear(140);
        var land = Fauna.MindOf(World.New(17).Agents.Values.First(a => a.Species == Species.Caribou)).Territory;
        var probe = Agents.Spawn(w, Species.Caribou, land[0], 1, [.. land]);
        int full = (int)Math.Ceiling(Fauna.CapacityOf(w, w.Agent(probe)));
        Agents.Remove(w, probe);
        var herd = Agents.Spawn(w, Species.Caribou, land[0], full, [.. land]);
        Sim.Advance(w, 30 * 1440);
        Assert.True(w.Agent(herd).Members.Count <= full);
    }

    [Fact]
    public void A_PackLongUnfed_Starves()
    {
        var w = Bare();
        var land = Fauna.MindOf(World.New(17).Agents.Values.First(a => a.Species == Species.Wolf)).Territory;
        var wolves = Agents.Spawn(w, Species.Wolf, land[0], 4, [.. land]);
        Fauna.MindOf(w.Agent(wolves)).LastFedMinute = w.Minute - Days(30);
        Sim.Advance(w, 20 * 1440);
        Assert.True(!w.Agents.ContainsKey(wolves) || w.Agent(wolves).Members.Count < 4, "a pack thirty days unfed lost no one");
        Assert.Contains(w.Tiles.Values.SelectMany(t => t.Carcasses ?? []), c => c.Animal == "Wolf" && c.Cause?.What == "starvation");
    }

    /// Arrivals come where the valley is empty, never megafauna, outside camp's safe zone and out of
    /// everyone's sight, no bigger than the land they come to carries, and no pack on another's land.
    /// Review: an arrival was sized from its row alone (caribou twelve strong on land carrying four, a
    /// wolf pack of ten on land carrying two), and a pack could come in on top of another's land.
    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void Herds_ArriveWhereTheValleyIsEmpty_SizedToTheirLand_OutOfSight()
    {
        var w = Bare(seed: 123); // a valley with mammoths, all taken out
        var camp = Map.Of(w).Camp;
        Assert.True(Fauna.ValleyCapacity(w, Species.Caribou) > 0);
        Assert.Equal(0, Fauna.ValleyCapacity(w, Species.Mammoth));
        // The player out on the open land, where arrivals could otherwise land in plain view.
        var open = Fauna.MindOf(World.New(123).Agents.Values.First(a => a.Species == Species.Bison)).Territory[0];
        w.Agent(P).Tile = open;
        var seen = new HashSet<AgentId>(w.Agents.Keys);
        int arrivals = 0;
        // Each day's look is the minute after midnight, when the day's arrivals came in (Fauna.Renew).
        Sim.Advance(w, 1440 - Weather.MinuteOfDay(w.Minute));
        for (int day = 0; day < 200; day++)
        {
            var lands = w.Agents.Values.Where(a => a.Mind is HerdMind).Select(a => Fauna.MindOf(a).Territory.ToList()).ToList();
            var body = w.Agent(P).Body;
            (body.HydrationMl, body.CaloriesKcal, body.EnergyMinutes) = (3000, 2000, 900); // alive, and so someone to be seen by
            Sim.Advance(w, 1440);
            Assert.True(Body.IsAlive(body));
            var newcomers = w.Agents.Values.Where(a => a.Mind is HerdMind && !seen.Contains(a.Id)).ToList();
            var packLand = w.Agents.Values.Where(a => a.Species == Species.Wolf && seen.Contains(a.Id)).SelectMany(a => Fauna.MindOf(a).Territory).ToHashSet();
            foreach (var herd in newcomers)
            {
                seen.Add(herd.Id);
                var land = Fauna.MindOf(herd).Territory;
                // A cub on its mother's land, or a lame member left behind on its herd's: born here, not arrived.
                if (lands.Concat(w.Agents.Values.Where(o => o.Id != herd.Id && o.Mind is HerdMind).Select(o => Fauna.MindOf(o).Territory)).Any(l => l.SequenceEqual(land)))
                    continue;
                arrivals++;
                Assert.False(Fauna.IsMegafauna(herd.Species));
                Assert.True(MapGen.Manhattan(land[0], camp) > Fauna.SafeZoneTiles, $"{herd.Species} came in at {land[0]}, inside camp's safe zone");
                Assert.True(MapGen.Manhattan(land[0], open) > Fauna.ArrivalClearanceTiles, $"{herd.Species} came in at {land[0]}, in sight of the player at {open}");
                Assert.True(herd.Members.Count <= Fauna.CapacityOf(w, herd) + 1e-9,
                    $"{herd.Species} came in {herd.Members.Count} strong on land carrying {Fauna.CapacityOf(w, herd):0.0}");
                if (herd.Species == Species.Wolf) Assert.DoesNotContain(land, packLand.Contains);
            }
        }
        Assert.True(arrivals > 0, "nothing came into an empty valley in two hundred days");
        Assert.Contains(w.Agents.Values, h => h.Species == Species.Caribou);
        // Capacity is derived, never saved: a loaded world reads the same capacities off the same land.
        var loaded = Save.FromJson(Save.ToJson(w));
        foreach (var herd in w.Agents.Values.Where(a => a.Mind is HerdMind))
            Assert.Equal(Fauna.CapacityOf(w, herd), Fauna.CapacityOf(loaded, loaded.Agent(herd.Id)));
    }

    /// Herds of a kind whose land overlaps share what it carries: two caribou herds put on one land
    /// have the young of one between them, not one each.
    [Fact]
    public void Herds_SharingLand_ShareWhatItCarries()
    {
        var w = Bare();
        w.Minute = MinuteOfDayOfYear(140);
        var a = CaribouHerd(w, 1);
        int full = (int)Math.Floor(Fauna.CapacityOf(w, w.Agent(a)));
        Assert.True(full >= 4, $"capacity {full}");
        w.Agent(a).Members.AddRange(Enumerable.Range(0, full / 2 - 1).Select(_ => SpeciesTable.NewBody(Species.Caribou, w.Rng("test"))));
        var b = CaribouHerd(w, full - full / 2);
        Sim.Advance(w, 50 * 1440);
        int together = w.Agent(a).Members.Count + w.Agent(b).Members.Count;
        Assert.True(together <= full, $"{together} caribou on land carrying {full}");
    }

    /// Review: no pack would hunt a mammoth to keep it (IsQuarry), but every pack hunted one anyway
    /// (PreyHere, PreySensed asked IsPrey), and seed 123's packs ate its mammoths out by day 540. One
    /// rule now: a pack neither goes for nor follows a mammoth.
    [Fact]
    public void A_Pack_LeavesMammothsAlone()
    {
        var w = Bare(seed: 123);
        var land = Fauna.MindOf(World.New(123).Agents.Values.First(a => a.Species == Species.Mammoth)).Territory;
        var mammoths = Agents.Spawn(w, Species.Mammoth, land[0], 6, [.. land]);
        var wolves = Agents.Spawn(w, Species.Wolf, land[0], 6, [land[0]]);
        TestKit.SetHunger(w.Agent(wolves), 0.95);
        Assert.False(Fauna.IsQuarry(Species.Mammoth));
        Assert.True(Fauna.IsQuarry(Species.Caribou) && Fauna.IsQuarry(Species.Bison));
        var events = new List<WorldEvent>();
        for (int i = 0; i < 3 * 1440 && w.Agents.ContainsKey(wolves); i++)
        {
            TestKit.SetHunger(w.Agent(wolves), 0.95); // starving the whole time, on the mammoths' land
            events.AddRange(Sim.Advance(w, 1));
        }
        Assert.DoesNotContain(events.OfType<FightEnded>(), f => f.Agent == mammoths);
        Assert.True(w.Agents.ContainsKey(wolves), "the pack went for the mammoths and died of it");
        Assert.True(w.Agent(mammoths).Members.Count >= 6);
    }

    /// Review: hyenas and the carrion flock never died but in a fight, and a pack only after fourteen
    /// days unfed, so they followed nothing down. What lives on kills thins toward what its land
    /// carries: a fed pack, hyenas and a flock on land with no prey near and little growing all lose
    /// members, and the herd that is left is no bigger than before.
    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void What_LivesOnKills_ThinsWhenTheLandCannotFeedIt()
    {
        var w = Bare();
        var land = Fauna.MindOf(World.New(17).Agents.Values.First(a => a.Species == Species.Wolf)).Territory;
        var wolves = Agents.Spawn(w, Species.Wolf, land[0], 8, [.. land]);
        var hyenas = Agents.Spawn(w, Species.Hyena, land[0], 6, [.. land]);
        var flock = Agents.Spawn(w, Species.Raven, land[0], 6, [land[0]]);
        Assert.Equal(0, Fauna.CapacityOf(w, w.Agent(wolves))); // no prey left in the valley
        double flockRoom = Fauna.CapacityOf(w, w.Agent(flock));
        Assert.True(flockRoom < 6, $"the flock's land carries {flockRoom:0.0}");
        for (int d = 0; d < 60; d++)
        {
            Fauna.MindOf(w.Agent(wolves)).LastFedMinute = w.Minute; // fed (scavenging), never past StarveDays
            Sim.Advance(w, 1440);
        }
        Assert.True(!w.Agents.ContainsKey(wolves) || w.Agent(wolves).Members.Count < 8, "a pack on land with no prey lost no one");
        Assert.True(!w.Agents.ContainsKey(hyenas) || w.Agent(hyenas).Members.Count < 6, "hyenas on land with no prey lost no one");
        Assert.True(!w.Agents.ContainsKey(flock) || w.Agent(flock).Members.Count < 6, "a flock over what its land carries lost no one");
        Assert.Contains(w.Tiles.Values.SelectMany(t => t.Carcasses ?? []), c => c.Animal == "Hyena" && c.Cause?.What == "starvation");
    }

    /// The wolves took my kill: a pack on the player's own kill holds it against the player (the gate
    /// is unwired; the rule is what it will be). What the player made is still the player's.
    [Fact]
    public void A_Pack_OnYourKill_GuardsIt()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
        var kill = Caribou(w, camp, new Cause(P, "the player's wooden spear"));
        Items.Add(Items.StashAt(w, camp, new Cause(P, "stashed by the player")), Resource.Pine, 2);
        Agents.Spawn(w, Species.Wolf, camp, 3);
        w.AccessGates = Access.Gate.All;
        Assert.Equal("a wolf pack guards it", Work.Refusal(w, new Butcher(P, kill.Id)));
        Assert.Null(Work.Refusal(w, new Unstash(P, Resource.Pine, 1)));
    }

    /// Review: an NPC that died holding a lit torch left it burning in the world's fires for good.
    /// A dropped torch goes out within the hour.
    [Fact]
    public void A_TorchInADeadHand_GoesOut()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        var torch = new FireId(w.NextFireId++);
        w.Fires[torch] = new FireState { Tile = camp, LastUpdatedMinute = w.Minute, CarriedBy = npc, TorchMinutesLeft = 600 };
        w.Agent(npc).Body.BloodPct = 0;
        Body.MarkDead(w.Agent(npc).Body, w.Minute, new Cause(null, "the cold"));
        Sim.Advance(w, 61);
        Assert.False(w.Fires.ContainsKey(torch), "the dead carry no torch");

        var gone = Npc.Spawn(w, camp); // carried off the map: an agent that has left the world
        var second = new FireId(w.NextFireId++);
        w.Fires[second] = new FireState { Tile = camp, LastUpdatedMinute = w.Minute, CarriedBy = gone, TorchMinutesLeft = 600 };
        Agents.Remove(w, gone);
        Sim.Advance(w, 61);
        Assert.False(w.Fires.ContainsKey(second));
    }

    // ---- the carrion flock ----

    [Fact]
    public void The_Flock_FindsFlesh_CirclesIt_AndEats()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var tile = Map.Step(Map.Step(camp, Direction.East), Direction.East);
        var c = Caribou(w, tile);
        double meat = Carcasses.MeatKg(c);
        var start = new TileId(tile.X, tile.Y + 6);
        var flock = Agents.Spawn(w, Species.Raven, start, 5, [start, tile]);
        Assert.False(Tags.Of(w, new ThingRef(ThingKind.Carcass, tile, c.Id)).HasFlag(Tag.Beacon));
        for (int i = 0; i < 6 * 60 && w.Agent(flock).Tile != tile; i++) Sim.Advance(w, 1);
        Assert.Equal(tile, w.Agent(flock).Tile);
        Assert.True(Tags.Of(w, new ThingRef(ThingKind.Carcass, tile, c.Id)).HasFlag(Tag.Beacon), "circling flesh: a beacon");
        Assert.Contains(Report.Finds(w), f => f.Tile == tile && f.Kind == "carcass" && f.Text.Contains("ravens circling"));
        TestKit.SetHunger(w.Agent(flock), 1);
        Sim.Advance(w, 60);
        Assert.True(Carcasses.MeatKg(c) < meat, "the flock ate nothing");
    }

    [Fact]
    public void The_Flock_FollowsAPredator_AndReadsAsRavensOverIt()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var tile = Map.Step(Map.Step(camp, Direction.East), Direction.East);
        var wolves = Agents.Spawn(w, Species.Wolf, tile, 3);
        Fauna.MindOf(w.Agent(wolves)).State = HerdState.Resting;
        var start = new TileId(tile.X - 5, tile.Y);
        var flock = Agents.Spawn(w, Species.Raven, start, 4, [start, tile]);
        for (int i = 0; i < 6 * 60 && w.Agent(flock).Tile != w.Agent(wolves).Tile; i++) Sim.Advance(w, 1);
        Assert.Equal(w.Agent(wolves).Tile, w.Agent(flock).Tile);
        var sign = Signs.At(w, w.Agent(flock).Tile).Single(s => s.Kind == SignKind.RavensOver);
        Assert.Equal(wolves, sign.Cause!.Value.Who);
        Assert.Equal(Things.OfAgent(w, flock), sign.Thing);
        Assert.Contains(Report.Finds(w), f => f.Kind == "ravens" && f.Category == FindCategory.Sign);
    }

    [Fact]
    public void The_Flock_RaidsAnUnwatchedStash_ThroughAccess()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var tile = Map.Step(camp, Direction.West);
        Items.Add(Items.StashAt(w, tile, new Cause(P, "stashed by the player")), Resource.RawMeat, 5);
        var flock = Agents.Spawn(w, Species.Raven, tile, 4, [tile]);
        TestKit.SetHunger(w.Agent(flock), 0.2); // not hungry enough to forage: only the stash tempts it
        Assert.Null(Fauna.Refusal(w, new Raid(flock)));
        w.Agent(P).Tile = tile;
        Assert.Equal("someone is keeping it", Fauna.Refusal(w, new Raid(flock)));
        w.Agent(P).Tile = camp;
        Agents.Spawn(w, Species.Wolf, tile, 2);
        Assert.Null(Fauna.Refusal(w, new Raid(flock))); // the guard gate is not wired
        w.AccessGates = Access.Gate.All;
        Assert.Equal("a wolf pack guards it", Fauna.Refusal(w, new Raid(flock)));
        w.AccessGates = Access.Wired;
        foreach (var id in w.Agents.Values.Where(a => a.Species == Species.Wolf).Select(a => a.Id).ToList()) Agents.Remove(w, id);
        Sim.Advance(w, 5 * 1440);
        Assert.True(Items.Have(w.Tiles[tile].Stash!, Resource.RawMeat) < 5, "five days unwatched and nothing taken");
    }

    /// The flock is its own kind, not the hyena: it never guards, hunts or threatens, and no pack hunts it.
    [Fact]
    public void The_Flock_IsNeitherPredatorNorPrey()
    {
        Assert.False(Fauna.IsPredator(Species.Raven));
        Assert.False(Fauna.IsPrey(Species.Raven));
        Assert.True(Fauna.IsPredator(Species.Hyena));
        Assert.Equal(HerdBehavior.Scavenger, Fauna.BehaviorOf(Species.Hyena));
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var c = Caribou(w, camp);
        Agents.Spawn(w, Species.Raven, camp, 4);
        Assert.False(Tags.Of(w, new ThingRef(ThingKind.Carcass, camp, c.Id)).HasFlag(Tag.Guarded));
    }

    // ---- the herds' access gate ----

    [Fact]
    public void Feeding_And_Grazing_AskAccess()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var tile = Map.Step(Map.Step(camp, Direction.East), Direction.East);
        Caribou(w, tile);
        var flock = Agents.Spawn(w, Species.Raven, tile, 3); // guards nothing, so the pack alone holds the tile
        var caribou = Agents.Spawn(w, Species.Caribou, tile, 2);
        Assert.Null(Fauna.Refusal(w, new Feed(flock)));
        Assert.Null(Fauna.Refusal(w, new Graze(caribou)));
        var wolves = Agents.Spawn(w, Species.Wolf, tile, 3);
        Assert.Null(Fauna.Refusal(w, new Feed(flock))); // unwired: the pack changes nothing yet
        Assert.Null(Fauna.Refusal(w, new Graze(caribou)));
        w.AccessGates = Access.Gate.All;
        Assert.Equal("nothing here it can reach to eat", Fauna.Refusal(w, new Feed(flock)));
        Assert.Equal("a wolf pack guards it", Fauna.Refusal(w, new Graze(caribou)));
        Assert.Null(Fauna.Refusal(w, new Feed(wolves))); // the guard's own meal
        Fauna.MindOf(w.Agent(wolves)).Fear = 0.9; // an afraid pack holds nothing
        Assert.Null(Fauna.Refusal(w, new Feed(flock)));
        Assert.Null(Fauna.Refusal(w, new Graze(caribou)));
    }

    /// Review: adding to a stash went through every gate and starting one through none. Putting down
    /// is one gate now: a guard bars it on someone else's pile, never on your own, begun or not.
    [Fact]
    public void Putting_Down_IsOneGate()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        TestKit.Supply(w);
        var npc = Npc.Spawn(w, camp);
        TestKit.Supply(w, npc);
        Agents.Spawn(w, Species.Wolf, camp, 3);
        w.AccessGates = Access.Gate.All;
        Assert.Null(Work.Refusal(w, new Stash(P, Resource.Pine, 1))); // a new pile: the player's own
        Sim.Step(w, new Stash(P, Resource.Pine, 1), 1);
        Assert.Null(Work.Refusal(w, new Stash(P, Resource.Pine, 1))); // still the player's own
        Assert.Equal("a wolf pack guards it", Work.Refusal(w, new Stash(npc, Resource.Pine, 1))); // someone else's
        Assert.Equal("a wolf pack guards it", Work.Refusal(w, new Unstash(npc, Resource.Pine, 1)));
    }

    // ---- the caches ----

    /// A never-observed tile's ground carried on from its class's cache is the ground the long way
    /// gives, sub-step for sub-step, through Peek and through CatchUp.
    [Fact]
    public void Ground_CarriedOnFromItsClass_IsTheLongWay()
    {
        var w = World.New(17);
        var m = Map.Of(w);
        var plain = Enumerable.Range(5, m.Width - 10).SelectMany(x => Enumerable.Range(5, m.Height - 10).Select(y => new TileId(x, y)))
            .Where(t => m.TerrainAt(t) == Terrain.Plain && m.SiteAt(t) == null && m.CaveAt(t) == Cave.None && t != m.Camp
                && w.Tiles.GetValueOrDefault(t)?.Ground == null).Take(3).ToList();
        w.Minute = 4 * 1440 + 7;
        Ground.Peek(w, plain[0]); // warms the class
        w.Minute = 9 * 1440 + 3;
        var carried = Save.ToJson(new World { Seed = 17, Tiles = new() { [plain[1]] = new TileState { Ground = Ground.Peek(w, plain[1]) } } });
        var fresh = World.New(17);
        fresh.Minute = w.Minute;
        var longWay = Save.ToJson(new World { Seed = 17, Tiles = new() { [plain[1]] = new TileState { Ground = Ground.Peek(fresh, plain[1]) } } });
        Assert.Equal(longWay, carried);
        Ground.CatchUp(w, plain[2]);
        Ground.CatchUp(fresh, plain[2]);
        static string Json(GroundState g) => System.Text.Json.JsonSerializer.Serialize(g);
        Assert.Equal(Json(fresh.Tiles[plain[2]].Ground!), Json(w.Tiles[plain[2]].Ground!));
    }

    /// The ground cache shares one state among every unobserved tile of a class (Ground.Peek): read
    /// warm, tile after tile, a sample from across the map is the ground each tile gives on its own
    /// with the cache thrown away. A per-tile input the class leaves out (a tile's own weather) would
    /// hand a tile another's ground here, and this fails.
    [Fact]
    public void Ground_FromTheCache_IsEachTilesOwn_AcrossTheMap()
    {
        var w = World.New(17);
        var m = Map.Of(w);
        static string Json(GroundState g) => System.Text.Json.JsonSerializer.Serialize(g);
        foreach (int day in new[] { 3, 40, 150 })
        {
            w.Minute = day * 1440L + 7 * day;
            var sample = Enumerable.Range(0, m.Width * m.Height).Where(i => i % 101 == day % 101)
                .Select(i => new TileId(i / m.Height, i % m.Height))
                .Where(t => m.TerrainAt(t).IsPassable() && w.Tiles.GetValueOrDefault(t)?.Ground == null).ToList();
            Assert.True(sample.Count > 40, $"{sample.Count} tiles sampled");
            w.GroundCache = null;
            var warm = sample.Select(t => Json(Ground.Peek(w, t))).ToList();
            for (int i = 0; i < sample.Count; i++)
            {
                w.GroundCache = null;
                Assert.True(warm[i] == Json(Ground.Peek(w, sample[i])), $"day {day}: {sample[i]} read another tile's ground from the cache");
            }
        }
    }

    /// A bench's NPC has the same id whatever generation populated (NpcBench), and the herds born
    /// after it come after it.
    [Fact]
    public void A_BenchNpc_KeepsItsId_WhateverTheValleyHolds()
    {
        var a = World.New(17);
        var b = World.New(5);
        Assert.NotEqual(a.Agents.Count, b.Agents.Count);
        Assert.Equal(new AgentId(NpcBench.FirstId), NpcBench.Spawn(a, Map.Of(a).Camp));
        Assert.Equal(new AgentId(NpcBench.FirstId), NpcBench.Spawn(b, Map.Of(b).Camp));
        Assert.Equal(new AgentId(NpcBench.FirstId + 1), NpcBench.Spawn(b, Map.Of(b).Camp));
        Assert.True(Agents.Spawn(b, Species.Caribou, Map.Of(b).Camp, 1).CompareTo(new AgentId(NpcBench.FirstId + 1)) > 0);
    }

    /// The Nearby index answers as a walk over every agent would, as herds walk, are born and die.
    [Fact]
    public void Nearby_IsExact_AsAgentsMove()
    {
        var w = World.New(17);
        var m = Map.Of(w);
        void Check()
        {
            foreach (var a in w.Agents.Values)
            {
                var truth = w.Agents.Values.Where(b => b.Tile == a.Tile).Select(b => b.Id).ToList();
                Assert.Equal(truth, Nearby.At(w, a.Tile));
                var within = w.Agents.Values.Where(b => MapGen.Manhattan(b.Tile, a.Tile) <= 6).Select(b => b.Id).ToList();
                Assert.Equal(within, Nearby.Within(w, a.Tile, 6).Select(b => b.Id).ToList());
            }
        }
        for (int i = 0; i < 40; i++)
        {
            Sim.Advance(w, 37);
            Check();
        }
        var spawned = Agents.Spawn(w, Species.Caribou, m.Camp, 2);
        Check();
        w.Agent(spawned).Tile = Map.Step(m.Camp, Direction.South); // set by hand, as tests do
        Check();
        Agents.Remove(w, spawned);
        Check();
    }
}
