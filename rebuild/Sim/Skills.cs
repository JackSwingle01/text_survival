namespace Survival;

/// What a person gets better at by doing it. Fighting, Hunting and the rest come when the actions
/// they reward exist.
public enum Skill { Firecraft, Foraging, Cooking, Butchering, Crafting }

/// One skill's progress: the level reached and the experience toward the next.
public sealed class SkillProgress
{
    public int Level { get; set; }
    public double Xp { get; set; }
}

/// Skills live with the person (PersonMind), so an NPC learns by the same rules as the player.
/// Experience is awarded per minute of the work or per attempt, never per step, so how a caller
/// chunks its steps cannot change what anyone learns.
public static class Skills
{
    public const int MaxLevel = 20;

    /// Experience from one level to the next: 5 at level 0, 5 more each level, so level 1 is a few
    /// fires or an afternoon's foraging and level 20 is a whole run's practice. Never 0, or the first
    /// attempt at anything would grant a free level.
    public static double XpToNext(int level) => 5.0 * (level + 1);

    // Experience: per attempt or per hour of the work.
    public const double FireSuccessXp = 3, FireFailureXp = 1;
    public const double ForageXpPerHour = 1, ButcherXpPerHour = 2, CraftXpPerHour = 2, CookXpPerKg = 1;

    // Bonuses per level. Small steps over a long curve: +10% a level runs the odds to their cap
    // within a few levels.
    public const double FirecraftPctPerLevel = 0.02, ForageYieldPctPerLevel = 0.015, CookSpeedPctPerLevel = 0.02, ButcherYieldPctPerLevel = 0.01;

    public static int Level(Agent agent, Skill skill) =>
        agent.Mind is PersonMind p && p.Skills.TryGetValue(skill, out var s) ? s.Level : 0;

    /// Adds experience; levels up as many times as it pays for, each with a SkillLeveledUp event.
    /// Anything without a person's mind (a herd) learns nothing.
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

    // ---- what a level is worth ----

    /// Added to the fire-start odds (an odds term).
    public static double FirecraftPct(int level) => level * FirecraftPctPerLevel;
    /// Multiplies what a forage minute finds.
    public static double ForageYieldFactor(int level) => 1 + level * ForageYieldPctPerLevel;
    /// Multiplies how much meat or fish a minute at the fire cooks: less time over the fire.
    public static double CookSpeedFactor(int level) => 1 + level * CookSpeedPctPerLevel;
    /// Multiplies what butchering yields: less left on the bone.
    public static double ButcherYieldFactor(int level) => 1 + level * ButcherYieldPctPerLevel;
}
