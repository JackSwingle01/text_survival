using text_survival.Actions;
using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Actors.Animals.Behaviors;
using text_survival.Actors.Player;
using text_survival.Bodies;
using text_survival.Items;

namespace text_survival.Combat;

/// <summary>
/// Polymorphic combat interface enabling any Actor to participate in combat.
/// Implementations handle decision-making (UI vs AI) and equipment differences.
/// </summary>
public interface ICombatActor
{
    // Identity
    Actor ActorReference { get; }
    string Name { get; }
    bool IsAlive { get; }

    // Combat stats (from Actor base class)
    double AttackDamage { get; }
    DamageType AttackType { get; }
    double Speed { get; }
    double Vitality { get; }

    // Equipment (nullable for animals)
    Gear? Weapon { get; }
    double ArmorCushioning { get; }
    double ArmorToughness { get; }
}

#region PlayerCombatActor

/// <summary>
/// Player wrapper for combat - uses UI prompts for all decisions.
/// </summary>
public class PlayerCombatActor : ICombatActor
{
    private readonly Player _player;
    private readonly Inventory _inventory;

    public PlayerCombatActor(Player player, Inventory inventory)
    {
        _player = player;
        _inventory = inventory;
    }

    public Actor ActorReference => _player;
    public string Name => "You";
    public bool IsAlive => _player.IsAlive;

    public double AttackDamage => _player.AttackDamage;
    public DamageType AttackType => _player.AttackType;
    public double Speed => _player.Speed;
    public double Vitality => _player.Vitality;

    public Gear? Weapon => _inventory.Weapon;
    public double ArmorCushioning => _inventory.TotalCushioning;
    public double ArmorToughness => _inventory.TotalToughness;
}

#endregion

#region NPCCombatActor

/// <summary>
/// NPC wrapper for combat - uses AI for decisions.
/// Reuses animal behavior state machine for decision-making.
/// </summary>
public class NPCCombatActor : ICombatActor
{
    private readonly NPC _npc;

    public NPCCombatActor(NPC npc)
    {
        _npc = npc;
    }

    public Actor ActorReference => _npc;
    public string Name => _npc.Name;
    public bool IsAlive => _npc.IsAlive;

    public double AttackDamage => _npc.AttackDamage;
    public DamageType AttackType => _npc.AttackType;
    public double Speed => _npc.Speed;
    public double Vitality => _npc.Vitality;

    public Gear? Weapon => _npc.Inventory.Weapon;
    public double ArmorCushioning => _npc.Inventory.TotalCushioning;
    public double ArmorToughness => _npc.Inventory.TotalToughness;
}

#endregion

#region AnimalCombatActor

/// <summary>
/// Animal wrapper for combat - uses existing behavior state machine.
/// </summary>
public class AnimalCombatActor : ICombatActor
{
    private readonly Animal _animal;

    public AnimalCombatActor(Animal animal)
    {
        _animal = animal;
    }

    public Actor ActorReference => _animal;
    public string Name => _animal.Name;
    public bool IsAlive => _animal.IsAlive;

    public double AttackDamage => _animal.AttackDamage;
    public DamageType AttackType => _animal.AttackType;
    public double Speed => _animal.Speed;
    public double Vitality => _animal.Vitality;

    // Animals don't have equipment
    public Gear? Weapon => null;
    public double ArmorCushioning => 0;
    public double ArmorToughness => 0;
}

#endregion
