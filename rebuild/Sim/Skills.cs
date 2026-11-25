namespace Survival;

public enum Skill { Firecraft, Foraging, Cooking, Butchering, Crafting }

public sealed class SkillProgress
{
    public int Level { get; set; }
    public double Xp { get; set; }
}

/// Skills live with the person (PersonMind), so an NPC learns by the same rules as the player.
/// Experience is per minute of the work or per attempt, never per step, so step size cannot change it.
public static class Skills
{
    public const int MaxLevel = 20;

    /// 5 at level 0, 5 more each level: level 1 is a few fires, level 20 a whole run's practice.
    /// Never 0, or the first attempt at anything would grant a free level.
    public static double XpToNext(int level) => 5.0 * (level + 1);

    public const double FireSuccessXp = 3, FireFailureXp = 1;
    public const double ForageXpPerHour = 1, ButcherXpPerHour = 2, CraftXpPerHour = 2, CookXpPerKg = 1;

    public const double FirecraftPctPerLevel = 0.02, ForageYieldPctPerLevel = 0.015, ButcherYieldPctPerLevel = 0.01;

    public static int Level(Agent agent, Skill skill) =>
        agent.Mind is PersonMind p && p.Skills.TryGetValue(skill, out var s) ? s.Level : 0;

    public static void Award(World world, Agent agent, Skill skill, double xp, List<WorldEvent> events)
    {
        if (xp < 0) throw new ArgumentOutOfRangeException(nameof(xp), xp, "Experience is never taken away");
        if (agent.Mind is not PersonMind p || xp == 0) return;
        if (!p.Skills.TryGetValue(skill, out var s)) p.Skills[skill] = s = new SkillProgress();
        if (s.Level >= MaxLevel) return;
        s.Xp += xp;
        // A hair of slack: sixty minutes of 1/60 is not exactly 1 in floating point.
        while (s.Level < MaxLevel && s.Xp + 1e-9 >= XpToNext(s.Level))
        {
            s.Xp = Math.Max(0, s.Xp - XpToNext(s.Level));
            s.Level++;
            events.Add(new SkillLeveledUp(world.Minute, agent.Id, agent.Tile, skill, s.Level));
        }
        if (s.Level >= MaxLevel) s.Xp = 0;
    }

    /// Added to the fire-start odds, not multiplied.
    public static double FirecraftPct(int level) => level * FirecraftPctPerLevel;
    public static double ForageYieldFactor(int level) => 1 + level * ForageYieldPctPerLevel;
    public static double ButcherYieldFactor(int level) => 1 + level * ButcherYieldPctPerLevel;
}
