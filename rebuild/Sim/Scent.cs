namespace Survival;

/// What a source gives off to an animal's nose (GLOSSARY "Scent"), 0-1, and how far the wind carries it.
public static class Scent
{
    /// A full scent to a nose of 1 in calm air, in tiles: a wolf (2.5) smells a fresh carcass 24 tiles (2.4 km)
    /// straight downwind, as Isle Royale wolves scented moose (Mech 1966, R).
    public const double FullReachTiles = 6;
    public const double BleedingLevel = 0.9, BloodyLevel = 0.8, ScentSourceLevel = 0.5, DirtyLevel = 0.2;

    /// x2 straight downwind, x0.5 upwind, x1 across and in calm air; a wind is named for where it comes from.
    public static double WindFactor(WindDirection wind, TileId source, TileId nose)
    {
        if (wind == WindDirection.Calm || source == nose) return 1;
        var (fromX, fromY) = Offset(wind);
        double dx = nose.X - source.X, dy = nose.Y - source.Y;
        double cos = (-fromX * dx - fromY * dy) / (Math.Sqrt(fromX * fromX + fromY * fromY) * Math.Sqrt(dx * dx + dy * dy));
        return Math.Pow(2, cos);
    }

    public static double ReachTiles(double scentLevel, double smellFactor, double windFactor) => FullReachTiles * scentLevel * smellFactor * windFactor;

    public static double FarthestAnimalReachTiles(double smellFactor) => ReachTiles(MaxSpeciesLevel, smellFactor, 2);

    static readonly double MaxSpeciesLevel = Enum.GetValues<Species>().Max(s => SpeciesTable.Of(s).ScentLevel);

    public static double OfFood(Resource r) => Items.Resources[r].ScentLevel ?? 0;

    /// An animal: its species' base. A person: that, blood, grime (Dirty), and the strongest food carried (x its kilos up to one).
    public static double Of(World world, Agent agent)
    {
        double level = SpeciesTable.Of(agent.Species).ScentLevel;
        if (agent.Mind is not PersonMind person) return level;
        if (Effects.Has(agent.Body, EffectKind.Bleeding)) level += BleedingLevel;
        level += BloodyLevel * Effects.Severity(agent.Body, EffectKind.Bloody);
        level += DirtyLevel * Effects.Severity(agent.Body, EffectKind.Dirty);
        return Math.Min(1, level + OfFoodIn(person.Pack));
    }

    public static double OfFoodIn(Inventory inv)
    {
        double food = 0;
        foreach (var (r, kg) in Items.Totals(inv))
            if (kg > 0) food = Math.Max(food, OfFood(r) * Math.Min(1, kg));
        return food;
    }

    static (int X, int Y) Offset(WindDirection d) => d switch
    {
        WindDirection.North => (0, -1),
        WindDirection.Northeast => (1, -1),
        WindDirection.East => (1, 0),
        WindDirection.Southeast => (1, 1),
        WindDirection.South => (0, 1),
        WindDirection.Southwest => (-1, 1),
        WindDirection.West => (-1, 0),
        WindDirection.Northwest => (-1, -1),
        _ => (0, 0),
    };
}

/// The one sensing function (spec "Scent"): what an animal makes out from where it stands, by sight or by scent.
public readonly record struct Senses(TileId From, double SightBudgetTiles, double SmellFactor, WindDirection Wind)
{
    public static Senses Now(World world, Agent animal)
    {
        var row = SpeciesTable.Of(animal.Species);
        double sight = Sight.BudgetTiles(world, animal.Tile, Body.Vision(animal.Body)) * row.PerceptionFactor;
        return new Senses(animal.Tile, sight, row.SmellFactor, Weather.At(world, world.Minute).WindDirection);
    }

    public bool Sees(World world, TileId tile) =>
        tile == From || MapGen.Manhattan(tile, From) <= SightBudgetTiles && Sight.Sees(world, From, tile, SightBudgetTiles);

    public bool Smells(TileId source, double scentLevel) =>
        scentLevel > 0 && MapGen.Manhattan(source, From) <= Scent.ReachTiles(scentLevel, SmellFactor, Scent.WindFactor(Wind, source, From));

    public bool Finds(World world, TileId tile, double scentLevel) => Smells(tile, scentLevel) || Sees(world, tile);

    public int AnimalScanTiles => (int)Math.Ceiling(Math.Max(SightBudgetTiles, Scent.FarthestAnimalReachTiles(SmellFactor)));
}
