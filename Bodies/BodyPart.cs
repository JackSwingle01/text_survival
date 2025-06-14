﻿using text_survival.IO;

namespace text_survival.Bodies;

public class BodyPart
{
    // Core properties
    public string Name { get; }
    public double Health { get; private set; }
    public double MaxHealth { get; }
    public bool IsVital { get; }
    public bool IsInternal { get; }
    public bool IsDamaged => Health < MaxHealth;
    public bool IsDestroyed => Health <= 0;

    public double Coverage { get; set; } // Percentage of parent this part covers
    public double EffectiveCoverage { get; private set; }

    // Hierarchy
    public BodyPart? Parent { get; private set; }
    private List<BodyPart> _parts = new();
    public IReadOnlyList<BodyPart> Parts => _parts.AsReadOnly();

    // Physical state
    private Dictionary<string, double> _baseCapacities = new();

    public BodyPart(string name, double maxHealth, bool isVital, bool isInternal, double coverage)
    {
        Name = name;
        MaxHealth = maxHealth;
        Health = maxHealth;
        IsVital = isVital;
        IsInternal = isInternal;
        Coverage = coverage;
        _baseCapacities = new Dictionary<string, double>();
    }

    // Capacity management
    public void SetBaseCapacity(string capacity, double value)
    {
        _baseCapacities[capacity] = value;
    }

    public double GetCapacity(string capacity)
    {
        if (!_baseCapacities.TryGetValue(capacity, out double baseValue))
        {
            return 0;
        }

        // Apply health scaling
        return baseValue * (Health / MaxHealth);
    }

    public IReadOnlyDictionary<string, double> GetCapacities()
    {
        var result = new Dictionary<string, double>();
        foreach (var pair in _baseCapacities)
        {
            result[pair.Key] = GetCapacity(pair.Key);
        }
        return result;
    }


    // Hierarchical structure
    public void AddPart(BodyPart part)
    {
        part.Parent = this;
        _parts.Add(part);
    }

    public void Heal(HealingInfo healingInfo)
    {
        if (IsDestroyed) return;

        // Handle targeted healing
        if (healingInfo.TargetPart != null && healingInfo.TargetPart != Name)
        {
            // Try to find the targeted part
            var targetPart = FindPartByName(healingInfo.TargetPart);
            if (targetPart != null)
            {
                targetPart.Heal(healingInfo);
                return;
            }
        }

        // Distribute healing
        if (_parts.Count > 0 && healingInfo.TargetPart == null)
        {
            // Prioritize damaged parts for healing
            var damagedParts = _parts.Where(p => p.IsDamaged).ToList();
            if (damagedParts.Count > 0)
            {
                damagedParts[Utils.RandInt(0, damagedParts.Count - 1)].Heal(healingInfo);
                return;
            }

            // Random distribution if no parts are damaged
            if (Utils.FlipCoin())
            {
                BodyPart p = _parts[Utils.RandInt(0, _parts.Count - 1)];
                p.Heal(healingInfo);
                return;
            }
        }

        // Apply healing to this part
        double adjustedAmount = healingInfo.Amount * healingInfo.Quality;
        Health += adjustedAmount;
        if (Health > MaxHealth)
        {
            Health = MaxHealth;
        }
    }

    public void CalculateEffectiveCoverage()
    {
        if (Parent == null)
        {
            EffectiveCoverage = 1.0; // Root part has 100% chance
            return;
        }

        // My coverage of parent × parent's effective coverage
        EffectiveCoverage = (Coverage / 100.0) * Parent.EffectiveCoverage;

        // Calculate for all children
        foreach (var part in _parts)
        {
            part.CalculateEffectiveCoverage();
        }
    }

    public BodyPart? Damage(DamageInfo damageInfo)
    {
        if (IsDestroyed) return this;

        if (damageInfo.TargetPart == null)
        {
            // Standard untargeted damage
            return DamageUntargeted(damageInfo);
        }

        // Handle targeted damage - but still allow for sub-part hits
        if (damageInfo.TargetPart == Name)
        {
            if (_parts.Count == 0)
            {
                ApplyDamage(damageInfo);
                return this;
            }


            if (Utils.DetermineSuccess(damageInfo.Accuracy))
            {
                // Direct hit on the targeted part
                ApplyDamage(damageInfo);
                return this;
            }

            // Even when targeting, there's a chance to hit sub-parts
            // Higher accuracy for targeted hits - use 75% of normal child coverage
            var partChances = new Dictionary<BodyPart, double>();
            double totalChildCoverage = 0;

            foreach (var part in _parts)
            {
                // Reduce child coverage to make it more likely to hit the targeted part
                double adjustedCoverage = part.Coverage * 0.75;
                partChances[part] = adjustedCoverage;
                totalChildCoverage += adjustedCoverage;
            }

            // Add self with remaining coverage - more likely than with random hits
            double selfCoverage = 100 - totalChildCoverage;
            partChances[this] = selfCoverage;

            BodyPart hit = Utils.GetRandomWeighted(partChances);
            if (hit == this)
            {
                ApplyDamage(damageInfo);
                return this;
            }
            else
            {
                // When hitting a child on a targeted attack, we should
                // propagate that this was intentional targeting
                damageInfo.TargetPart = hit.Name; // Update target to child part
                damageInfo.Accuracy *= 0.8; // Reduce accuracy for child hit
                return hit.Damage(damageInfo);

            }
        }
        else // Handle targeted damage for a different part (searching)
        {
            // Look for the targeted part among children
            var targetedPart = FindPartByName(damageInfo.TargetPart);

            if (targetedPart != null && Utils.DetermineSuccess(damageInfo.Accuracy)) // chance to miss based on accuracy
            {
                // Found the part - propagate damage to it
                return targetedPart.Damage(damageInfo);
            }

            // Target not found as a descendant - try to hit this part instead
            // But with reduced damage since the intended target was missed
            damageInfo.Amount = damageInfo.Amount * 0.7; // Reduced damage for missing intended target
            damageInfo.TargetPart = null; // Clear targeting since we're defaulting

            // Process as untargeted hit
            return DamageUntargeted(damageInfo);
        }
    }

    // Separate method for untargeted damage distribution
    private BodyPart? DamageUntargeted(DamageInfo damageInfo)
    {
        // Distribute damage based on coverage
        if (_parts.Count > 0)
        {
            // Get all parts with their coverage values
            var partChances = new Dictionary<BodyPart, double>();
            double totalChildCoverage = 0;

            foreach (var part in _parts)
            {
                partChances[part] = part.Coverage;
                totalChildCoverage += part.Coverage;
            }

            // Add self with remaining coverage
            double selfCoverage = 100 - totalChildCoverage;
            partChances[this] = selfCoverage;

            BodyPart hit = Utils.GetRandomWeighted(partChances);
            if (hit == this)
            {
                ApplyDamage(damageInfo);
                return this;
            }
            else
            {
                return hit.Damage(damageInfo);
            }

        }

        // Default if no children or calculation issue
        ApplyDamage(damageInfo);
        return this;
    }

    // Helper method to find a part by name in the hierarchy
    private BodyPart? FindPartByName(string partName)
    {
        if (Name == partName) return this;

        foreach (var part in _parts)
        {
            var foundPart = part.FindPartByName(partName);
            if (foundPart != null) return foundPart;
        }

        return null;
    }

    // Method to actually apply damage
    private void ApplyDamage(DamageInfo damageInfo)
    {
        // Apply damage reduction for internal parts if damage is not penetrating
        double damageAmount = damageInfo.Amount;
        if (IsInternal && !damageInfo.IsPenetrating)
        {
            damageAmount *= 0.5; // 50% damage reduction for internal parts
        }

        Health -= damageAmount;
        // Output.WriteLine(this, " took ", damageAmount, " damage");
        // Handle destruction
        if (IsDestroyed)
        {
            Health = 0;
            if (IsVital && Parent != null)
            {
                var criticalDamage = new DamageInfo
                {
                    Amount = Parent.MaxHealth * 0.5,
                    Type = "critical",
                    Source = damageInfo.Source,
                    IsPenetrating = true // Critical damage always penetrates
                };
                Parent.Damage(criticalDamage);
            }
        }
    }

    public void Describe()
    {
        // Calculate health percentage
        int healthPercent = (int)((Health / MaxHealth) * 100);

        // Determine damage severity description
        string damageDescription;
        if (healthPercent <= 0)
        {
            damageDescription = "destroyed";
        }
        else if (healthPercent < 20)
        {
            damageDescription = "critically damaged";
        }
        else if (healthPercent < 40)
        {
            damageDescription = "severely damaged";
        }
        else if (healthPercent < 60)
        {
            damageDescription = "moderately damaged";
        }
        else if (healthPercent < 80)
        {
            damageDescription = "lightly damaged";
        }
        else if (healthPercent < 100)
        {
            damageDescription = "slightly damaged";
        }
        else
        {
            damageDescription = "in perfect condition";
        }

        // Output description line
        Output.WriteLine($"- {Name} is {damageDescription} ({healthPercent}%)");
    }
    public override string ToString() => Name;
}