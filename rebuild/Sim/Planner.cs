using System.Text.Json.Serialization;

namespace Survival;

/// What an agent knows, read by every source listing and the planner: Memory for people, the slow world plus sensing for animals.
public abstract class Knowledge
{
    public abstract bool Threat(TileId tile);

    public abstract bool OpenWater(TileId tile);

    public abstract GroundSeen Ground(TileId tile);
}

public readonly record struct GroundSeen(double SnowDepthM, double PondLiquidM);

/// A forecast of what a source pays: units of the stat a minute, and the kcal each unit costs.
public readonly record struct Source(double PerMinute, double KcalPerUnit = 0);

public sealed record Asking(Stat Stat, Priority Priority, double Amount, double EarnKcalPerMinute, double Cohesion, Func<TileId, bool> KinAt);

/// The source's tile and the tiles walked onto to it (none when it is here); none: nothing pays, so it stays put until it plans again.
public sealed record Plan(
    [property: JsonConverter(typeof(JsonStringEnumConverter<Stat>))] Stat Stat,
    [property: JsonConverter(typeof(JsonStringEnumConverter<Priority>))] Priority Priority,
    TileId? At, List<TileId> Route);

/// Priorities.Top picks the stat, its owner lists sources, Trip.Routes finds ways; here they are weighed in minutes.
public static class Planner
{
    public const double StayFactor = 1.25;

    public const int MaxRouteMinutes = 1440;

    /// travel + amount ÷ rate + kcal ÷ the earning rate, x (1 + cohesion) with no kin there; threats refused below Critical.
    public static Plan Choose(World world, AgentId who, Knowledge view, Asking ask, Func<TileId, Source?> sourceAt, bool routeAway = true)
    {
        var agent = world.Agent(who);
        var here = agent.Tile;
        bool careless = ask.Priority == Priority.Critical;
        double kcalPerMinute = Traversal.KcalPerMinute(world, agent);
        double Total(TileId t, int minutes)
        {
            if (!careless && view.Threat(t) || sourceAt(t) is not { } s || s.PerMinute <= 0) return double.PositiveInfinity;
            double total = minutes + ask.Amount / s.PerMinute + (minutes * kcalPerMinute + ask.Amount * s.KcalPerUnit) / ask.EarnKcalPerMinute;
            return ask.KinAt(t) ? total : total * (1 + ask.Cohesion);
        }
        double stay = Total(here, 0);
        if (!routeAway) return new Plan(ask.Stat, ask.Priority, double.IsInfinity(stay) ? null : here, []);
        double best = double.PositiveInfinity;
        TileId? bestTile = null;
        var routes = Trip.Routes(world, who, MaxRouteMinutes, refused: careless ? null : view.Threat, until: (t, minutes) =>
        {
            if (minutes >= Math.Min(best, stay / StayFactor)) return true;
            if (t == here) return false;
            double total = Total(t, minutes);
            if (total < best || total == best && bestTile is { } b && t.CompareTo(b) < 0) (best, bestTile) = (total, t);
            return false;
        });
        if (bestTile is TileId source && best * StayFactor < stay) return new Plan(ask.Stat, ask.Priority, source, [.. routes.RouteTo(source)]);
        return new Plan(ask.Stat, ask.Priority, double.IsInfinity(stay) ? null : here, []);
    }
}
