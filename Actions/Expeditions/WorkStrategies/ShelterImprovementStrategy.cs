using text_survival.Actions;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Desktop;
using DesktopIO = text_survival.Desktop.DesktopIO;

namespace text_survival.Actions.Expeditions.WorkStrategies;

public class ShelterImprovementStrategy : IWorkStrategy
{
    private ShelterImprovementType _selectedType;
    private Resource _selectedMaterial;
    private int _quantity = 1;
    private bool _cancelled;

    public string? ValidateLocation(GameContext ctx, Location location)
    {
        var shelter = location.GetFeature<ShelterFeature>();
        if (shelter == null)
            return "There's no shelter here to improve.";
        if (shelter.IsDestroyed)
            return "The shelter is destroyed and cannot be improved.";

        // Check if player has any shelter materials
        bool hasMaterials = MaterialProperties.ShelterMaterials
            .Any(m => ctx.Inventory.Count(m) > 0);

        if (!hasMaterials)
            return "You don't have any materials to improve the shelter.";

        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        var shelter = location.GetFeature<ShelterFeature>()!;

        // Step 1: Choose what to improve
        var typeChoice = new Choice<ShelterImprovementType?>("What do you want to improve?");
        typeChoice.AddOption($"Insulation ({shelter.TemperatureInsulation:P0}/{shelter.InsulationCap:P0})", ShelterImprovementType.Insulation);
        typeChoice.AddOption($"Overhead ({shelter.OverheadCoverage:P0}/{shelter.OverheadCap:P0})", ShelterImprovementType.Overhead);
        typeChoice.AddOption($"Wind ({shelter.WindCoverage:P0}/{shelter.WindCap:P0})", ShelterImprovementType.Wind);
        typeChoice.AddOption("Cancel", null);

        var selectedType = typeChoice.GetPlayerChoice(ctx);
        if (selectedType == null)
        {
            _cancelled = true;
            return null;
        }
        _selectedType = selectedType.Value;

        // Step 2: Choose material
        var availableMaterials = MaterialProperties.ShelterMaterials
            .Where(m => ctx.Inventory.Count(m) > 0)
            .OrderByDescending(m => MaterialProperties.GetEffectiveness(m, _selectedType))
            .ToList();

        var materialChoice = new Choice<Resource?>("What material do you want to use?");
        foreach (var material in availableMaterials)
        {
            int count = ctx.Inventory.Count(material);
            double effectiveness = MaterialProperties.GetEffectiveness(material, _selectedType);
            string quality = effectiveness switch
            {
                >= 0.8 => "excellent",
                >= 0.6 => "good",
                >= 0.4 => "moderate",
                _ => "poor"
            };
            materialChoice.AddOption($"{material.ToDisplayName()} x{count} ({quality})", material);
        }
        materialChoice.AddOption("Cancel", null);

        var selectedMaterial = materialChoice.GetPlayerChoice(ctx);
        if (selectedMaterial == null)
        {
            _cancelled = true;
            return null;
        }
        _selectedMaterial = selectedMaterial.Value;

        // Step 3: Choose quantity
        int available = ctx.Inventory.Count(_selectedMaterial);
        if (available > 1)
        {
            var quantityChoice = new Choice<int>("How many do you want to use?");
            for (int i = 1; i <= Math.Min(available, 5); i++)
            {
                quantityChoice.AddOption($"{i}", i);
            }
            _quantity = quantityChoice.GetPlayerChoice(ctx);
        }
        else
        {
            _quantity = 1;
        }

        // Work takes 10 minutes per material used
        return null; // We handle timing internally
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        if (_cancelled)
            return (0, new List<string>());

        int workTime = _quantity * 10; // 10 minutes per material

        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkMoving: true,
            checkManipulation: true,
            effectRegistry: ctx.player.EffectRegistry
        );

        return ((int)(workTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Crafting;

    public string GetActivityName() => "improving shelter";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        if (_cancelled)
            return new WorkResult([], null, 0, false);

        var shelter = location.GetFeature<ShelterFeature>()!;
        var collected = new List<string>();

        // Consume materials
        for (int i = 0; i < _quantity; i++)
        {
            ctx.Inventory.Pop(_selectedMaterial);
        }

        // Apply improvement
        double improvement = shelter.Improve(_selectedType, _selectedMaterial, _quantity);

        string typeName = _selectedType switch
        {
            ShelterImprovementType.Insulation => "insulation",
            ShelterImprovementType.Overhead => "overhead coverage",
            ShelterImprovementType.Wind => "wind protection",
            _ => "protection"
        };

        string resultMessage;
        if (improvement < 0.001)
        {
            if (shelter.IsNatural)
                resultMessage = $"This {shelter.Name.ToLower()}'s {typeName} can't be improved further.";
            else
                resultMessage = $"The shelter's {typeName} is at its limit for this frame type.";
        }
        else
        {
            resultMessage = $"You improved the shelter's {typeName} by {improvement:P1}.";
        }

        collected.Add(shelter.GetStatusText());

        // Add narrative description
        GameDisplay.AddNarrative(ctx, shelter.GetNarrativeDescription());

        DesktopIO.ShowWorkResult(ctx, "Shelter Improvement", resultMessage, collected);

        return new WorkResult(collected, null, actualTime, false);
    }
}
