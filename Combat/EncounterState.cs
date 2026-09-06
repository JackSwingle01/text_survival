using text_survival.Actors;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Combat;

/// <summary>A background encounter checkpoint at a world-minute boundary.</summary>
public sealed class EncounterState
{
    public Location Location { get; set; } = null!;
    public EncounterPurpose Purpose { get; set; }
    public int Rounds { get; set; }
    public bool IsOver { get; set; }
    public List<CombatantState> Combatants { get; set; } = [];
    public List<Actor> ConsideredHelpers { get; set; } = [];
    public List<Gear> ThrownWeapons { get; set; } = [];

    public static EncounterState Capture(CombatScenario scenario) => new()
    {
        Location = scenario.Location!, Purpose = scenario.Purpose, Rounds = scenario.ElapsedRounds,
        IsOver = scenario.IsOver, ConsideredHelpers = scenario.ConsideredHelpers.ToList(),
        ThrownWeapons = scenario.ThrownWeapons.ToList(),
        Combatants = scenario.Team1.Concat(scenario.Team2).Select(u => new CombatantState
        {
            Actor = u.actor, TeamOne = scenario.Team1.Contains(u), Active = scenario.Units.Contains(u),
            Position = u.Position, Awareness = u.Awareness, Boldness = u.BoldnessModifier,
            Signal = u.CompanionSignalModifier, SignalExpires = u.CompanionSignalExpiresAt,
            LastSignal = scenario.LastSignalMinute.GetValueOrDefault(u.actor, -100),
            JustDamaged = u.JustDamaged, Optimistic = u.InitialOptimism,
            Dodge = u.DodgeSet, Block = u.BlockSet, Brace = u.BraceSet
        }).ToList()
    };

    public CombatScenario Restore()
    {
        var units = Combatants.Select(c => new Unit(c.Actor, c.Position)
        {
            Awareness = c.Awareness, BoldnessModifier = c.Boldness,
            CompanionSignalModifier = c.Signal, CompanionSignalExpiresAt = c.SignalExpires,
            JustDamaged = c.JustDamaged, InitialOptimism = c.Optimistic,
            DodgeSet = c.Dodge, BlockSet = c.Block, BraceSet = c.Brace
        }).ToList();
        var scenario = new CombatScenario(units.Where((u, i) => Combatants[i].TeamOne).ToList(),
            units.Where((u, i) => !Combatants[i].TeamOne).ToList(), null, Location)
        { Purpose = Purpose, ElapsedRounds = Rounds, IsOver = IsOver, ThrownWeapons = ThrownWeapons };
        scenario.Units.RemoveAll(u => !Combatants[units.IndexOf(u)].Active);
        foreach (var unit in units)
        {
            unit.allies.RemoveAll(u => !scenario.Units.Contains(u));
            unit.enemies.RemoveAll(u => !scenario.Units.Contains(u));
            scenario.LastSignalMinute[unit.actor] = Combatants[units.IndexOf(unit)].LastSignal;
        }
        scenario.ConsideredHelpers.UnionWith(ConsideredHelpers);
        return scenario;
    }
}

public sealed class CombatantState
{
    public Actor Actor { get; set; } = null!;
    public bool TeamOne { get; set; }
    public bool Active { get; set; }
    public GridPosition Position { get; set; }
    public AwarenessState Awareness { get; set; }
    public double Boldness { get; set; }
    public double Signal { get; set; }
    public double SignalExpires { get; set; }
    public double LastSignal { get; set; }
    public bool JustDamaged { get; set; }
    public bool Optimistic { get; set; }
    public bool Dodge { get; set; }
    public bool Block { get; set; }
    public bool Brace { get; set; }
}
