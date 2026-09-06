using text_survival.Actions;
using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Tests.Support;

namespace text_survival.Tests.Companions;

/// <summary>
/// Small, fully specified worlds. Advance uses the real world tick. MoveActor is an
/// arrangement operation for the independently controlled leader: it commits real
/// crossings (including footprints), without simulating a second following algorithm.
/// </summary>
internal sealed class CompanionWorld
{
    public GameContext Game { get; }
    public GameMap Map => Game.Map!;
    public Location Camp => Game.Camp;

    public CompanionWorld(int width = 8, int height = 3, int seed = 41)
    {
        text_survival.Utils.Seed(seed);
        var weather = new Weather(25, GameContext.StartTime);
        var map = new GameMap(width, height) { Weather = weather };
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                map.SetLocation(x, y, new Location($"Tile {x},{y}", "Test ground", weather, 1)
                {
                    Terrain = TerrainType.Plain,
                    VisibilityFactor = 1
                });

        map.CurrentPosition = new GridPosition(0, 0);
        var camp = map.CurrentLocation;
        camp.AddFeature(new CacheFeature("Shared cache", CacheType.Built));
        var player = new Player { CurrentLocation = camp, Map = map };
        SetComfortable(player);
        Game = new GameContext(player, camp, weather)
        {
            Map = map,
            Ui = new ScriptedUi()
        };
        map.UpdateVisibility();
    }

    public NPC AddNpc(string name, int x = 1, int y = 1)
    {
        var npc = new NPC(name, new Personality
        {
            Boldness = 0.6,
            Sociability = 0.6,
            Selfishness = 0.3
        }, Tile(x, y), Map)
        { Camp = Camp };
        SetComfortable(npc);
        Game.NPCs.Add(npc);
        return npc;
    }

    public Actor AddLeader(LeaderKind kind)
    {
        switch (kind)
        {
            case LeaderKind.Player:
                MoveActor(Game.player, 1, 1);
                return Game.player;
            case LeaderKind.Animal:
                return AnimalFactory.FromType(AnimalType.Caribou, Tile(1, 1), Map)!;
            default:
                var npc = AddNpc("Leader");
                npc.CurrentAction = new NPCRest(1_000);
                return npc;
        }
    }

    public Location Tile(int x, int y) => Map.GetLocationAt(x, y)!;
    public GridPosition Position(Actor actor) => Map.GetPosition(actor.CurrentLocation);

    public static void SetComfortable(Actor actor)
    {
        actor.Body.BodyTemperature = Body.BASE_BODY_TEMP;
        actor.Body.Energy = SurvivalProcessor.MAX_ENERGY_MINUTES * 0.9;
        actor.Body.Hydration = SurvivalProcessor.MAX_HYDRATION * 0.95;
        actor.Body.CalorieStore = SurvivalProcessor.MAX_CALORIES * 0.9;
    }

    public static void Follow(NPC follower, Actor target)
    {
        // An already accepted agreement. Recruitment and persuasion are separate specs.
        follower.Relationships.AddMemory(MemoryType.SavedMe, target);
        follower.Following = new FollowIntent(target);
    }

    public ForageFeature AddForage(Location at)
    {
        // Even a one-minute lean session must find something: the production feature
        // only records depletion when it yields resources. Yield quantity stays random.
        var forage = new ForageFeature(2).AddSticks(abundance: 120, minKg: 0.01, maxKg: 0.01);
        at.AddFeature(forage);
        return forage;
    }

    public void Advance(int minutes)
    {
        for (int i = 0; i < minutes; i++)
            Game.UpdateWithoutEvents(1, ActivityType.Resting);
    }

    public void AdvanceUntil(Func<bool> condition, int maxMinutes)
    {
        for (int i = 0; i < maxMinutes && !condition(); i++)
            Advance(1);
        Assert.True(condition(), $"Condition was not reached within {maxMinutes} game minutes.");
    }

    public void MoveActor(Actor actor, int x, int y)
    {
        var destination = new GridPosition(x, y);
        while (Position(actor) != destination)
        {
            var from = Position(actor);
            var next = from.X != x
                ? new GridPosition(from.X + Math.Sign(x - from.X), from.Y)
                : new GridPosition(from.X, from.Y + Math.Sign(y - from.Y));
            var location = Map.GetLocationAt(next)!;
            Assert.Contains(location, Map.GetTravelOptionsFrom(actor.CurrentLocation));
            if (actor is Player)
                Map.MoveTo(location, actor);
            else if (actor is NPC npc)
                new NPCMove(location, npc).Complete(npc);
            else
            {
                Map.RecordMove(from, next, ((Animal)actor).AnimalType.Tracks());
                actor.CurrentLocation = location;
            }
        }
    }

    /// <summary>
    /// Migration adapter only: repeatedly asks the current production route query for
    /// steps. Replace this adapter with Navigation when IPathfinder is introduced.
    /// No search, barrier filtering, or fallback route is implemented in the fixture.
    /// </summary>
    public IReadOnlyList<GridPosition>? FindRoute(GridPosition from, GridPosition to)
    {
        var steps = new List<GridPosition>();
        var visited = new HashSet<GridPosition> { from };
        while (from != to)
        {
            var next = Map.GetNextInPath(Map.GetLocationAt(from)!, Map.GetLocationAt(to)!);
            if (next == null) return null;
            from = Map.GetPosition(next);
            Assert.True(visited.Add(from), "Route planning repeated a tile without reaching the destination.");
            steps.Add(from);
        }
        return steps;
    }
}

public enum LeaderKind { Npc, Player, Animal }
