namespace Survival.Web;

/// Development only: drives a game into a state that is slow or chancy to reach by play, so the
/// page can be screenshotted there (tools/shots.mjs --shot) and tests can check the view. Not part
/// of the boundary a player's client uses; the page calls it only for ?scenario=.
public static class Scenarios
{
    public static readonly IReadOnlyList<string> Names = ["supplied", "fight", "dead", "won"];

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
            default:
                throw new ArgumentException($"No scenario '{name}'; there are {string.Join(", ", Names)}");
        }
        return host.View();
    }
}
