using text_survival.Actors.Animals;

namespace text_survival.Combat;

/// <summary>
/// Animal behavior states during combat. Each state has readable tells
/// that let players learn patterns and predict actions.
/// </summary>
public enum CombatBehavior
{
    /// <summary>Moving side to side, looking for an opening. Neutral state.</summary>
    Circling,

    /// <summary>Snarling, raised hackles. Testing player's nerve. May charge soon.</summary>
    Threatening,

    /// <summary>Committed attack run. Player must react.</summary>
    Charging,

    /// <summary>Off-balance after failed charge or dodged attack. Vulnerable moment.</summary>
    Recovering,

    /// <summary>Lost nerve, backing away. May flee if given opportunity.</summary>
    Retreating
}

/// <summary>
/// Manages animal behavior state machine during combat.
/// Provides readable tells and predictable-but-learnable transitions.
/// Boldness drives state transitions - bold animals progress faster.
/// Recovery timing varies by species and condition.
/// </summary>
public class AnimalCombatBehaviorManager
{
    private static readonly Random _rng = new();

    // Boldness-driven timing (base turns, modified by boldness)
    private const int BaseCirclingMaxTurns = 3;
    private const int BaseThreateningMaxTurns = 2;

    // Standoff boldness decay per turn
    private const double StandoffBoldnessDecay = 0.03;
    private const double HoldGroundBoldnessDecay = 0.02;
    private const double WeaknessShownBoldnessGain = 0.05;

    // Species recovery base values (turns = base * (2 - vitality) * random)
    private static readonly Dictionary<string, double> SpeciesRecoveryBase = new()
    {
        { "bear", 0.8 },      // Relentless
        { "cave bear", 0.7 }, // Even more relentless
        { "wolf", 1.5 },      // Quick but readable
        { "sabertooth", 1.2 }, // Fast and deadly
        { "hyena", 1.4 },     // Similar to wolf
        { "default", 1.5 }
    };

    public CombatBehavior CurrentBehavior { get; private set; } = CombatBehavior.Circling;
    public int TurnsInCurrentBehavior { get; private set; } = 0;
    public int RecoveryTurnsRemaining { get; private set; } = 0;

    private readonly Animal _animal;
    private double _currentBoldness;
    private double _speciesRecoveryBase;

    public AnimalCombatBehaviorManager(Animal animal, double initialBoldness)
    {
        _animal = animal;
        _currentBoldness = Math.Clamp(initialBoldness, 0.0, 1.0);

        // Determine species recovery base
        string animalKey = animal.Name.ToLower();
        _speciesRecoveryBase = SpeciesRecoveryBase.GetValueOrDefault(animalKey, SpeciesRecoveryBase["default"]);
    }

    /// <summary>
    /// Current boldness level (0-1). Affected by damage taken, player actions.
    /// </summary>
    public double Boldness => _currentBoldness;

    /// <summary>
    /// Max turns in Circling before transitioning. Bold animals commit faster.
    /// </summary>
    private int CirclingMaxTurns => Math.Max(1, (int)(BaseCirclingMaxTurns * (1.5 - _currentBoldness)));

    /// <summary>
    /// Max turns in Threatening before committing. Bold animals don't hesitate.
    /// </summary>
    private int ThreateningMaxTurns => Math.Max(1, (int)(BaseThreateningMaxTurns * (1.5 - _currentBoldness)));

    /// <summary>
    /// Recovery turns based on species and condition.
    /// Formula: floor(speciesBase * (2 - vitality) * random(0.8-1.2))
    /// </summary>
    private int RecoveringTurns => CalculateRecoveryTurns();

    private int CalculateRecoveryTurns()
    {
        double vitality = _animal.Vitality;
        double randomVariance = 0.8 + _rng.NextDouble() * 0.4; // 0.8 to 1.2
        double turns = _speciesRecoveryBase * (2.0 - vitality) * randomVariance;
        return Math.Max(1, (int)Math.Floor(turns));
    }

    /// <summary>
    /// Gets a text description of the current behavior (the "tell").
    /// </summary>
    public string GetBehaviorDescription()
    {
        return CurrentBehavior switch
        {
            CombatBehavior.Circling => GetCirclingDescription(),
            CombatBehavior.Threatening => GetThreateningDescription(),
            CombatBehavior.Charging => GetChargingDescription(),
            CombatBehavior.Recovering => GetRecoveringDescription(),
            CombatBehavior.Retreating => GetRetreatingDescription(),
            _ => $"The {_animal.Name} watches you."
        };
    }

    /// <summary>
    /// Gets a short status text for UI display.
    /// </summary>
    public string GetBehaviorStatus()
    {
        return CurrentBehavior switch
        {
            CombatBehavior.Circling => "Circling",
            CombatBehavior.Threatening => "Threatening",
            CombatBehavior.Charging => "CHARGING!",
            CombatBehavior.Recovering => "Recovering",
            CombatBehavior.Retreating => "Retreating",
            _ => "Unknown"
        };
    }

    #region Behavior Descriptions (Readable Tells)

    private string GetCirclingDescription()
    {
        var descriptions = new[]
        {
            $"The {_animal.Name} moves to your left, eyes fixed on you.",
            $"The {_animal.Name} paces, looking for an angle.",
            $"The {_animal.Name} circles slowly, watching for weakness.",
            $"The {_animal.Name} shifts position, testing your reactions."
        };
        return descriptions[_rng.Next(descriptions.Length)];
    }

    private string GetThreateningDescription()
    {
        var descriptions = new[]
        {
            $"The {_animal.Name}'s hackles rise. A low growl builds in its throat.",
            $"The {_animal.Name} bares its teeth, testing you.",
            $"The {_animal.Name} coils, muscles tensing. It's about to spring.",
            $"The {_animal.Name} snarls, lowering its head. This is a warning."
        };
        return descriptions[_rng.Next(descriptions.Length)];
    }

    private string GetChargingDescription()
    {
        var descriptions = new[]
        {
            $"The {_animal.Name} explodes forward!",
            $"The {_animal.Name} launches at your throat!",
            $"The {_animal.Name} charges!",
            $"The {_animal.Name} springs at you with terrifying speed!"
        };
        return descriptions[_rng.Next(descriptions.Length)];
    }

    private string GetRecoveringDescription()
    {
        var descriptions = new[]
        {
            $"The {_animal.Name} skids past, off-balance.",
            $"The {_animal.Name} missed. It's turning back.",
            $"The {_animal.Name} stumbles, recovering its footing.",
            $"The {_animal.Name} overextended. It's exposed."
        };
        return descriptions[_rng.Next(descriptions.Length)];
    }

    private string GetRetreatingDescription()
    {
        var descriptions = new[]
        {
            $"The {_animal.Name} hesitates, then backs away.",
            $"The {_animal.Name}'s ears flatten. It's had enough.",
            $"The {_animal.Name} whimpers, giving ground.",
            $"The {_animal.Name} backs off, eyes still fixed on you."
        };
        return descriptions[_rng.Next(descriptions.Length)];
    }

    #endregion

    #region State Transitions

    /// <summary>
    /// Updates behavior based on player action and combat state.
    /// Call at the end of each turn.
    /// </summary>
    public void UpdateBehavior(CombatPlayerAction lastPlayerAction, DistanceZone currentZone, double animalVitality)
    {
        TurnsInCurrentBehavior++;

        // Update boldness based on damage taken
        if (animalVitality < 0.7) _currentBoldness -= 0.05;
        if (animalVitality < 0.4) _currentBoldness -= 0.10;

        // Standoff boldness decay - the animal is deciding whether to commit
        // This creates natural standoff resolution even without player action
        bool isShowingWeakness = lastPlayerAction == CombatPlayerAction.BackAway ||
                                  lastPlayerAction == CombatPlayerAction.GiveGround;

        if (isShowingWeakness)
        {
            // Player shows weakness - animal gets bolder
            _currentBoldness += WeaknessShownBoldnessGain;
        }
        else
        {
            // Base decay - the wolf is deciding, your fire is burning
            _currentBoldness -= StandoffBoldnessDecay;

            // Additional decay if player holds ground
            if (lastPlayerAction == CombatPlayerAction.HoldGround)
            {
                _currentBoldness -= HoldGroundBoldnessDecay;
            }
        }

        // Clamp boldness
        _currentBoldness = Math.Clamp(_currentBoldness, 0.0, 1.0);

        // State-specific transitions
        CombatBehavior newBehavior = CurrentBehavior switch
        {
            CombatBehavior.Circling => TransitionFromCircling(lastPlayerAction, currentZone),
            CombatBehavior.Threatening => TransitionFromThreatening(lastPlayerAction, currentZone),
            CombatBehavior.Charging => TransitionFromCharging(),
            CombatBehavior.Recovering => TransitionFromRecovering(),
            CombatBehavior.Retreating => TransitionFromRetreating(lastPlayerAction),
            _ => CombatBehavior.Circling
        };

        if (newBehavior != CurrentBehavior)
        {
            CurrentBehavior = newBehavior;
            TurnsInCurrentBehavior = 0;
        }
    }

    private CombatBehavior TransitionFromCircling(CombatPlayerAction action, DistanceZone zone)
    {
        // Player showing weakness triggers aggression
        if (action == CombatPlayerAction.BackAway || action == CombatPlayerAction.GiveGround)
        {
            _currentBoldness += 0.1;
            if (_currentBoldness > 0.6) return CombatBehavior.Threatening;
        }

        // Holding ground decreases boldness slightly
        if (action == CombatPlayerAction.HoldGround)
        {
            _currentBoldness -= 0.05;
        }

        // Check for natural transition after circling
        if (TurnsInCurrentBehavior >= CirclingMaxTurns)
        {
            if (_currentBoldness < 0.3) return CombatBehavior.Retreating;
            if (_currentBoldness > 0.5) return CombatBehavior.Threatening;
        }

        // Low boldness may trigger retreat
        if (_currentBoldness < 0.25) return CombatBehavior.Retreating;

        return CombatBehavior.Circling;
    }

    private CombatBehavior TransitionFromThreatening(CombatPlayerAction action, DistanceZone zone)
    {
        // Player backing away from threat triggers charge
        if (action == CombatPlayerAction.BackAway || action == CombatPlayerAction.GiveGround)
        {
            return CombatBehavior.Charging;
        }

        // Holding ground or advancing may cause hesitation
        if (action == CombatPlayerAction.HoldGround || action == CombatPlayerAction.CloseDistance)
        {
            _currentBoldness -= 0.1;
            if (_currentBoldness < 0.4)
            {
                return _rng.NextDouble() < 0.6 ? CombatBehavior.Circling : CombatBehavior.Retreating;
            }
        }

        // After threatening too long, commit to charge or back off
        if (TurnsInCurrentBehavior >= ThreateningMaxTurns)
        {
            if (_currentBoldness > 0.4) return CombatBehavior.Charging;
            return CombatBehavior.Circling;
        }

        // High boldness with player in range triggers charge
        if (_currentBoldness > 0.7 && zone <= DistanceZone.Close)
        {
            return CombatBehavior.Charging;
        }

        return CombatBehavior.Threatening;
    }

    private CombatBehavior TransitionFromCharging()
    {
        // Charging always leads to Recovering (whether hit lands or misses)
        // The actual damage/dodge happens in CombatRunner, this is just state
        return CombatBehavior.Recovering;
    }

    private CombatBehavior TransitionFromRecovering()
    {
        // Recovery is a brief window, then reassess
        if (TurnsInCurrentBehavior >= RecoveringTurns)
        {
            if (_currentBoldness < 0.3) return CombatBehavior.Retreating;
            return CombatBehavior.Circling;
        }
        return CombatBehavior.Recovering;
    }

    private CombatBehavior TransitionFromRetreating(CombatPlayerAction action)
    {
        // Player pressing advantage may trigger fight response
        if (action == CombatPlayerAction.CloseDistance || action == CombatPlayerAction.Strike)
        {
            if (_rng.NextDouble() < 0.3 + _currentBoldness * 0.4)
            {
                _currentBoldness += 0.2; // Cornered animal fights back
                return CombatBehavior.Threatening;
            }
        }

        // Continue retreating
        return CombatBehavior.Retreating;
    }

    #endregion

    #region Combat Modifiers

    /// <summary>
    /// Hit chance modifier based on current behavior (vulnerability windows).
    /// </summary>
    public double GetHitChanceModifier()
    {
        return CurrentBehavior switch
        {
            CombatBehavior.Circling => 1.0,
            CombatBehavior.Threatening => 0.8,   // Ready to dodge
            CombatBehavior.Charging => 1.2,     // Committed, predictable
            CombatBehavior.Recovering => 1.5,   // Off-balance, exposed
            CombatBehavior.Retreating => 1.3,   // Not defending
            _ => 1.0
        };
    }

    /// <summary>
    /// Critical hit chance based on current behavior (vulnerability windows).
    /// </summary>
    public double GetCriticalChance()
    {
        return CurrentBehavior switch
        {
            CombatBehavior.Circling => 0.05,
            CombatBehavior.Threatening => 0.03,
            CombatBehavior.Charging => 0.15,    // Committed to path
            CombatBehavior.Recovering => 0.25,  // The opening
            CombatBehavior.Retreating => 0.10,
            _ => 0.05
        };
    }

    /// <summary>
    /// Whether the animal will attack this turn (used by combat loop).
    /// </summary>
    public bool WillAttackThisTurn()
    {
        return CurrentBehavior == CombatBehavior.Charging;
    }

    /// <summary>
    /// Whether the animal is trying to flee (combat may end if at Far zone).
    /// </summary>
    public bool IsTryingToFlee()
    {
        return CurrentBehavior == CombatBehavior.Retreating && _currentBoldness < 0.2;
    }

    /// <summary>
    /// Modifies boldness directly (used for intimidation, taking damage, etc.).
    /// </summary>
    public void ModifyBoldness(double delta)
    {
        _currentBoldness = Math.Clamp(_currentBoldness + delta, 0.0, 1.0);
    }

    /// <summary>
    /// Forces transition to a specific behavior (used for brace counter-attacks, etc.).
    /// </summary>
    public void ForceBehavior(CombatBehavior behavior)
    {
        CurrentBehavior = behavior;
        TurnsInCurrentBehavior = 0;
    }

    #endregion
}

/// <summary>
/// Player actions that affect animal behavior transitions.
/// </summary>
public enum CombatPlayerAction
{
    None,
    HoldGround,
    BackAway,
    GiveGround,
    CloseDistance,
    Strike,
    Thrust,
    Throw,
    Dodge,
    Block,
    Brace,
    Intimidate,
    Disengage,
    DropMeat,
    Shove,
    Grapple,
    GoDown
}
