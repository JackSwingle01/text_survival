using System.Text.Json.Serialization;

namespace Survival;

/// The world as an agent knows it, read by every source listing and the planner: the slow world plus sensing for animals.
public abstract class WorldView
{
    public abstract bool Threat(TileId tile);

    public abstract bool OpenWater(TileId tile);

    public abstract GroundSeen Ground(TileId tile);
}

public readonly record struct GroundSeen(double SnowDepthM, double PondLiquidM);

/// A forecast of what a source pays: the stat's units a minute (ml of water, energy minutes), and the kcal each unit costs.
public readonly record struct Source(double PerMinute, double KcalPerUnit = 0);

public sealed record Asking(Stat Stat, Priority Priority, double Amount, double EarnKcalPerMinute, double Cohesion, Func<TileId, bool> KinAt,
    double BestPerMinute = double.PositiveInfinity);

/// The source's tile, the tiles walked onto to it (none: here), and what it pays; none: nothing pays, so it stays put until it plans again.
public sealed record Plan(
    [property: JsonConverter(typeof(JsonStringEnumConverter<Stat>))] Stat Stat,
    [property: JsonConverter(typeof(JsonStringEnumConverter<Priority>))] Priority Priority,
    TileId? At, List<TileId> Route, double PerMinute = 0);

/// Priorities.Top picks the stat, its owner lists sources, Trip.Routes finds ways; here they are weighed in minutes.
public static class Planner
{
    public const double StayFactor = 1.25;

    public const int MaxRouteMinutes = 1440;

    /// Each total x 1 ± up to this, so agents with the same view don't all choose alike in one minute.
    public const double Noise = 0.05;

    /// travel + amount ÷ rate + kcal ÷ the earning rate, x noise, x (1 + cohesion) with no kin there; threats refused below Critical; only there, if given.
    public static Plan Choose(World world, AgentId who, WorldView view, Asking ask, Func<TileId, Source?> sourceAt, bool routeAway = true,
        TileId? only = null)
    {
        var agent = world.Agent(who);
        var here = agent.Tile;
        bool careless = ask.Priority == Priority.Critical;
        double kcalPerMinute = Traversal.KcalPerMinute(world, agent);
        var rng = world.Rng("planner", who);
        double Total(TileId t, int minutes)
        {
            if (!careless && view.Threat(t) || sourceAt(t) is not { } s || s.PerMinute <= 0) return double.PositiveInfinity;
            double total = minutes + ask.Amount / s.PerMinute + (minutes * kcalPerMinute + ask.Amount * s.KcalPerUnit) / ask.EarnKcalPerMinute;
            total *= 1 + Noise * (2 * rng.NextDouble() - 1);
            return ask.KinAt(t) ? total : total * (1 + ask.Cohesion);
        }
        double stay = Total(here, 0);
        Plan Stay() => double.IsInfinity(stay) ? new Plan(ask.Stat, ask.Priority, null, []) : new Plan(ask.Stat, ask.Priority, here, [], sourceAt(here)!.Value.PerMinute);
        if (!routeAway) return Stay();
        double best = double.PositiveInfinity;
        TileId? bestTile = null;
        var routes = Trip.Routes(world, who, MaxRouteMinutes, refused: careless ? null : view.Threat, until: (t, minutes) =>
        {
            if ((minutes + ask.Amount / ask.BestPerMinute) * (1 - Noise) >= Math.Min(best, stay / StayFactor)) return true;
            if (t == here || only is TileId o && t != o) return false;
            double total = Total(t, minutes);
            if (total < best || total == best && bestTile is { } b && t.CompareTo(b) < 0) (best, bestTile) = (total, t);
            return only != null;
        });
        if (bestTile is TileId source && best * StayFactor < stay)
            return new Plan(ask.Stat, ask.Priority, source, [.. routes.RouteTo(source)], sourceAt(source)!.Value.PerMinute);
        return Stay();
    }
}
