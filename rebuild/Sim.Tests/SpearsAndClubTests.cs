namespace Survival.Tests;

/// Spears, the staff and the stone club (#147; spec 2026-11-24 recipes-that-make-sense, the recipe table and
/// "Combat numbers"): what each recipe takes and asks for, the one-step wooden spear at a fire, and the club's blows.
public class SpearsAndClubTests
{
    static readonly AgentId P = AgentId.Player;

    static World Noon(out Inventory pack) => MakeTests.Noon(out pack);

    static void Burning(World w) => MakeTests.Burning(w);

    /// A binding is a sinew or 50 g of plant fibre; some rows bind with one, some with the other.
    static void Kit(Inventory pack, string recipe)
    {
        void Branch() => Items.AddPiece(pack, Resource.PineBranch, 1.5);
        void Sinew() => Items.AddPiece(pack, Resource.Sinew, 0.1);
        void Fibre() => Items.Add(pack, Resource.PlantFiber, 0.05);
        switch (recipe)
        {
            case "staff" or "wooden_spear": Branch(); break;
            case "flint_tipped_spear": Branch(); Items.AddPiece(pack, Resource.Flint, 0.2); Fibre(); break;
            case "obsidian_tipped_spear": Branch(); Items.AddPiece(pack, Resource.Obsidian, 0.2); Sinew(); break;
            case "ivory_tipped_spear": Branch(); Items.AddPiece(pack, Resource.Ivory, 0.8); Sinew(); Sinew(); break;
            case "stone_club": Branch(); Items.AddPiece(pack, Resource.Stone, 0.6); Fibre(); break;
            default: throw new ArgumentException(recipe);
        }
    }

    /// The gear that gives each capability and nothing else the recipes ask for.
    static string ToolGiving(Capability cap) => cap switch
    {
        Capability.Striking => "stone",
        Capability.Cutting => "crude_edge",
        _ => throw new ArgumentException(cap.ToString()),
    };

    [Theory]
    [InlineData("staff", new Capability[0], 2)]
    [InlineData("wooden_spear", new[] { Capability.Cutting }, 50)]
    [InlineData("flint_tipped_spear", new[] { Capability.Cutting, Capability.Striking }, 40)]
    [InlineData("obsidian_tipped_spear", new[] { Capability.Cutting, Capability.Striking }, 40)]
    [InlineData("ivory_tipped_spear", new[] { Capability.Cutting, Capability.Striking }, 60)]
    [InlineData("stone_club", new[] { Capability.Cutting }, 20)]
    public void EachRecipe_IsMadeFromWhatItLists_AndRefusedWithoutItsTools(string recipe, Capability[] tools, int minutes)
    {
        var w = Noon(out var pack);
        Burning(w);
        Kit(pack, recipe);
        foreach (var cap in tools)
        {
            Assert.Equal($"you need something for {Names.Of(cap)}", Sim.Refusal(w, new Make(P, recipe))?.Reason);
            pack.Gear.Add(Items.Make(ToolGiving(cap)));
        }
        var step = Sim.Step(w, new Make(P, recipe), Verbs.UntilDoneMinutes);
        Assert.Equal(Making.MadeIt, step.InterruptedReason);
        Assert.Equal(minutes, step.MinutesElapsed);
        Assert.Single(pack.Gear, g => g.Kind == recipe && g.MadeOf != null);
        Assert.Empty(Items.KgByResource(pack)); // every input went in, a piece or 50 g of each
    }

    /// The wooden spear is sharpened and fire-hardened in one recipe: no fire, no spear.
    [Fact]
    public void TheWoodenSpear_IsRefusedWithoutAFire()
    {
        var w = Noon(out var pack);
        Kit(pack, "wooden_spear");
        pack.Gear.Add(Items.Make("crude_edge"));
        Assert.Equal("you need a fire burning here", Sim.Refusal(w, new Make(P, "wooden_spear"))?.Reason);
        Burning(w);
        Assert.Null(Sim.Refusal(w, new Make(P, "wooden_spear")));
    }

    /// The staff is a branch picked up and trimmed by feel: no tool and no light.
    [Fact]
    public void TheStaff_NeedsNoToolAndNoLight()
    {
        var w = Noon(out var pack);
        w.Minute = 14 * 60; // eleven at night, and no fire
        Kit(pack, "staff");
        Assert.Null(Sim.Refusal(w, new Make(P, "staff")));
    }

    /// The club: blunt, short, never thrown, and it outlasts the staff and every point.
    [Fact]
    public void TheStoneClub_IsBlunt_Short_NotThrown_AndDurable()
    {
        var club = Items.GearKinds["stone_club"];
        var weapon = club.Weapon!;
        Assert.Equal(WeaponClass.Blunt, weapon.Class);
        Assert.Equal(3.5, weapon.Damage);
        Assert.Equal(1.0, weapon.ReachM);
        Assert.Null(weapon.Thrown);
        foreach (var other in new[] { "staff", "flint_tipped_spear", "obsidian_tipped_spear", "ivory_tipped_spear" })
            Assert.True(club.MaxDurability > Items.GearKinds[other].MaxDurability, other);
    }

    /// The obsidian point is sharper and more fragile than the flint one; ivory is the best point.
    [Fact]
    public void ThePoints_RankAsTheirMaterials()
    {
        static double Damage(string kind) => Items.GearKinds[kind].Weapon!.Damage;
        static int Uses(string kind) => Items.GearKinds[kind].MaxDurability;
        Assert.True(Damage("obsidian_tipped_spear") > Damage("flint_tipped_spear"));
        Assert.True(Uses("obsidian_tipped_spear") < Uses("flint_tipped_spear"));
        Assert.True(Damage("ivory_tipped_spear") > Damage("obsidian_tipped_spear"));
        Assert.True(Damage("flint_tipped_spear") > Damage("wooden_spear"));
    }

    // ---- the club's blows ----

    /// Blows, each the weapon's damage with the fight's luck on it, until a fresh average member of the species is
    /// down (dead, or too far gone to know where it is), as the fight's own Body.Damage lands them. The mean over seeds.
    static double MeanBlowsToDown(string kind, Species species, int bodies)
    {
        var weapon = Items.GearKinds[kind].Weapon!;
        var blow = (weapon.Damage, Combat.TypeOf(weapon));
        double total = 0;
        for (int seed = 0; seed < bodies; seed++)
        {
            var body = SpeciesTable.Average(species);
            var rng = Rng.ForSystem(seed, "blows");
            int blows = 0;
            while (!Combat.IsDown(body) && blows < 100)
            {
                var (amount, type) = Combat.Luck(blow, rng);
                Body.Damage(body, new DamageInfo(amount, type), rng);
                blows++;
            }
            total += blows;
        }
        return total / bodies;
    }

    /// The spec's measurement: a club of 3.5 downs a person, a wolf or a caribou in no more blows than the wooden
    /// spear does, and falls short on a bear.
    [Theory]
    [InlineData(Species.Human)]
    [InlineData(Species.Wolf)]
    [InlineData(Species.Caribou)]
    public void AClubOf35_Downs_InNoMoreBlowsThanTheWoodenSpear(Species species)
    {
        double club = MeanBlowsToDown("stone_club", species, 400), spear = MeanBlowsToDown("wooden_spear", species, 400);
        Assert.True(club <= spear, $"{species}: club {club:F2} blows, spear {spear:F2}");
    }

    [Fact]
    public void AClubOf35_FallsShortOfTheWoodenSpear_OnABear()
    {
        double club = MeanBlowsToDown("stone_club", Species.Bear, 400), spear = MeanBlowsToDown("wooden_spear", Species.Bear, 400);
        Assert.True(club > spear, $"club {club:F2} blows, spear {spear:F2}");
    }

    // ---- content ----

    static readonly string[] Removed = ["crude_spear", "heavy_spear", "stone_tipped_spear"];

    [Fact]
    public void NoRemovedSpear_Remains_AndNoLootTableNamesOne()
    {
        Assert.All(Removed, id => Assert.False(Recipes.Exists(id), $"recipe {id}"));
        Assert.All(Removed, id => Assert.False(Items.GearKinds.ContainsKey(id), $"gear {id}"));
        static IEnumerable<string> Gear(IEnumerable<LootRoll> rolls) => rolls.SelectMany(l => (l.Gear ?? []).Concat(Gear(l.Of ?? [])));
        Assert.Empty(Content.Loot.Values.SelectMany(Gear).Intersect(Removed));
        Assert.Empty(Content.Tables.Discoveries.Finds.Values.Concat(Content.Tables.Discoveries.Details.Values)
            .SelectMany(f => Flat(f.Items ?? [])).Select(i => i.Gear).OfType<string>().Intersect(Removed));
    }

    static IEnumerable<FindItem> Flat(IEnumerable<FindItem> items) => items.SelectMany(i => new[] { i }.Concat(Flat(i.Shuffle ?? [])));
}
