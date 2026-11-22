namespace Survival.Web;

/// Development only: drives a game into a state that is slow or chancy to reach by play, so the
/// page can be screenshotted there (tools/shots.mjs --shot) and tests can check the view. Not part
/// of the boundary a player's client uses; the page calls it only for ?scenario=.
public static class Scenarios
{
    public static readonly IReadOnlyList<string> Names = ["supplied", "fight", "dead", "won", "winter", "trails"];

    /// 15 January, 09:00: the clock's start (1 July) plus 198 days.
    const long WinterMinute = 198 * 1440;

    /// A new game's clock moved on to mid-January before anything has happened: the ground has lived
    /// the autumn and winter (Ground catches a tile up from the sky alone).
    static void ToWinter(World w)
    {
        if (w.Minute != 0) throw new InvalidOperationException($"Winter moves a new game's clock, not one at minute {w.Minute}");
        w.Minute = WinterMinute;
    }

    public static string Apply(GameHost host, string name)
    {
        var w = host.World;
        var p = w.Agent(AgentId.Player);
        switch (name)
        {
            // Wood, stone and fibre in the pack: enough to make a staff and a knife.
            case "supplied":
                {
                    var pack = Items.Pack(w, AgentId.Player);
                    Items.Add(pack, Resource.Birch, 3);
                    Items.Add(pack, Resource.Stone, 0.9);
                    Items.Add(pack, Resource.PlantFiber, 0.3);
                    Items.Add(pack, Resource.BirchBark, 0.1);
                    break;
                }
            // A lone wolf on the player's tile, stalked: the player's hunt opens on the grid.
            case "fight":
                {
                    var wolf = Agents.Spawn(w, Species.Wolf, p.Tile, 1);
                    Fauna.BeginHunt(w, AgentId.Player, wolf, 0);
                    break;
                }
            case "dead":
                {
                    var rng = w.Rng("scenario", AgentId.Player);
                    for (int i = 0; i < 50 && Body.IsAlive(p.Body); i++)
                        Body.Damage(p.Body, new DamageInfo(1.0, DamageType.Blunt, BodyTarget.Chest, By: new Cause(null, "the cold and the dark")), rng);
                    break;
                }
            case "won":
                p.Tile = Map.Of(w).Exit;
                break;
            // Mid-January, and the player takes a look at the ground.
            case "winter":
                {
                    ToWinter(w);
                    Sim.Step(w, new Wait(AgentId.Player), 1);
                    break;
                }
            // Winter, with routes worn near camp and prints fresh around the player. The wear is written
            // as it stands, a little over each tier so it holds through the looks, not walked in: the
            // scenario does not hang on how fast feet wear a route.
            case "trails":
                {
                    ToWinter(w);
                    var m = Map.Of(w);
                    var c = p.Tile;
                    int side = c.X < m.Width / 2 ? 1 : -1; // toward the open side of the map
                    TileId At(int dx, int dy) => new(c.X + side * dx, c.Y + dy);
                    bool Open(TileId t) => m.InBounds(t) && m.TerrainAt(t).IsPassable();
                    void Wear(TileId a, TileId b, double wear)
                    {
                        if (Open(a) && Open(b)) w.Trails[new EdgeId(a, b)] = new TrailWear(wear, w.Minute);
                    }
                    // A route out of camp: a trail beside it, a path further on, a trace where it turns north.
                    for (int i = 0; i < 8; i++) Wear(At(i, 1), At(i + 1, 1), i < 3 ? Tracks.TrailAt + 5 : i < 5 ? Tracks.PathAt + 3 : Tracks.TraceAt + 2);
                    for (int j = 1; j > -4; j--) Wear(At(8, j), At(8, j - 1), Tracks.TraceAt + 2);
                    // Grazing ground beyond it, worn on every side.
                    for (int y = -8; y <= -5; y++)
                        for (int x = 5; x <= 9; x++)
                        {
                            Wear(At(x, y), At(x + 1, y), Tracks.TrailAt + 5);
                            Wear(At(x, y), At(x, y + 1), Tracks.TrailAt + 5);
                        }
                    // The player has walked out along it and looked, and is back in camp.
                    foreach (var look in new[] { At(4, 1), At(8, 0), At(8, -4), At(7, -7) })
                    {
                        if (!Open(look)) continue;
                        (p.Tile, p.Crossing) = (look, null);
                        Sim.Step(w, new Wait(AgentId.Player), 1);
                    }
                    (p.Tile, p.Crossing) = (c, null);
                    // Prints on the nearest ground in sight, open ground before forest (where they show only
                    // between the trees), never water: a person heading east, a wolf pack heading north,
                    // caribou crossing every way.
                    Sim.Step(w, new Wait(AgentId.Player), 1);
                    var open = Sight.InView(w, p)
                        .Where(t => t != c && Math.Max(Math.Abs(t.X - c.X), Math.Abs(t.Y - c.Y)) <= ViewBuilder.TrackReadTiles
                            && m.TerrainAt(t).IsPassable() && m.TerrainAt(t) is not (Terrain.Water or Terrain.DeepWater))
                        .OrderBy(t => m.TerrainAt(t) == Terrain.Forest).ThenBy(t => Math.Abs(t.X - c.X) + Math.Abs(t.Y - c.Y)).ThenBy(t => t)
                        .Take(3).ToList();
                    if (open.Count < 3) throw new InvalidOperationException($"The trails scenario needs three tiles of ground in sight near camp; seed {w.Seed} has {open.Count}");
                    Tracks.RecordMove(w, Map.Step(open[0], Direction.West), open[0], TrackMaker.Human, 1);
                    Tracks.RecordMove(w, Map.Step(open[1], Direction.South), open[1], TrackMaker.Paw, 4);
                    foreach (var dir in new[] { Direction.West, Direction.East, Direction.North, Direction.South })
                        Tracks.RecordMove(w, Map.Step(open[2], dir), open[2], TrackMaker.Hoof, 3);
                    Sim.Step(w, new Wait(AgentId.Player), 1);
                    break;
                }
            default:
                throw new ArgumentException($"No scenario '{name}'; there are {string.Join(", ", Names)}");
        }
        return host.View();
    }
}
