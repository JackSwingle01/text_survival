using text_survival.Bodies;
using text_survival.Crafting;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Desktop;
using DesktopIO = text_survival.Desktop.DesktopIO;

namespace text_survival.Actions;

/// <summary>
/// Handles need-based crafting. Player picks a need category, sees options, and crafts.
/// </summary>
public class CraftingRunner(GameContext ctx)
{
    private readonly GameContext _ctx = ctx;
    private readonly NeedCraftingSystem _crafting = new();

    /// <summary>
    /// Run the crafting menu. Shows all categories and recipes on one screen.
    /// The CraftingOverlay handles all crafting logic internally.
    /// </summary>
    public void Run()
    {
        // Use the overlay-based crafting UI
        Desktop.DesktopIO.RunCraftingAndWait(_ctx);
    }

    /// <summary>
    /// Contextual prompt when player needs something specific.
    /// Returns true if player crafted something.
    /// </summary>
    public bool PromptForNeed(NeedCategory need, string context)
    {
        GameDisplay.AddNarrative(_ctx, $"You {context}.");

        var options = _crafting.GetOptionsForNeed(need, _ctx.Inventory);
        var craftable = options.Where(o => o.CanCraft(_ctx.Inventory)).ToList();

        if (craftable.Count == 0)
        {
            GameDisplay.AddNarrative(_ctx, $"You need {GetNeedDescription(need)}, but don't have materials to make one.");
            GameDisplay.Render(_ctx, statusText: "Thinking.");
            return false;
        }

        GameDisplay.AddNarrative(_ctx, $"You could make {GetNeedDescription(need)}. Craft one now?");
        GameDisplay.Render(_ctx, statusText: "Thinking.");

        if (!DesktopIO.Confirm(_ctx, "Craft now?"))
            return false;

        // Show crafting screen for this category
        GameDisplay.RenderCraftingScreen(_ctx, _crafting, $"CRAFT {GetNeedLabel(need).ToUpper()}");

        var result = ShowOptionsForNeed(need);

        Desktop.DesktopIO.ClearCrafting(_ctx);
        return result;
    }

    private bool ShowOptionsForNeed(NeedCategory need)
    {
        var options = _crafting.GetOptionsForNeed(need, _ctx.Inventory);

        // Filter out features that already exist at camp (can only build one)
        options = options.Where(o => !IsFeatureAlreadyBuilt(o)).ToList();

        var craftable = options.Where(o => o.CanCraft(_ctx.Inventory)).ToList();

        if (craftable.Count == 0)
        {
            GameDisplay.AddNarrative(_ctx, "You can't make anything right now.");
            GameDisplay.Render(_ctx, statusText: "Thinking.");
            return false;
        }

        var choice = new Choice<CraftOption?>("What do you want to make?");

        foreach (var option in craftable)
        {
            string label = $"{option.Name} - {option.GetRequirementsShort()} - {option.CraftingTimeMinutes} min";
            choice.AddOption(label, option);
        }

        choice.AddOption("Cancel", null);

        var selected = choice.GetPlayerChoice(_ctx);

        if (selected == null)
            return false;

        return DoCraft(selected);
    }

    private bool DoCraft(CraftOption option)
    {
        GameDisplay.AddNarrative(_ctx, $"You begin working on a {option.Name}...");

        var capacities = _ctx.player.GetCapacities();
        int totalTime = option.CraftingTimeMinutes;

        // Consciousness impairment slows crafting (+25%)
        if (AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness))
        {
            totalTime = (int)(totalTime * 1.25);
            GameDisplay.AddWarning(_ctx, "Your foggy mind slows the work.");
        }

        // Dexterity impairment slows crafting (combines manipulation, wetness, darkness, vitality)
        double dexterity = AbilityCalculator.GetDexterity(_ctx.player, _ctx);
        if (dexterity < 0.7)
        {
            // Scale penalty based on dexterity: 0.7 = no penalty, 0.0 = +50% time
            double penaltyFactor = 1.0 + ((0.7 - dexterity) / 0.7 * 0.5);
            totalTime = (int)(totalTime * penaltyFactor);

            // Get context for warnings
            var abilityContext = AbilityContext.FromFullContext(
                _ctx.player, _ctx.Inventory, _ctx.CurrentLocation, _ctx.GameTime.Hour);

            // Contextual warning
            if (abilityContext.DarknessLevel > 0.5 && !abilityContext.HasLightSource)
                GameDisplay.AddWarning(_ctx, "The darkness makes the work harder.");
            else if (abilityContext.WetnessPct > 0.3)
                GameDisplay.AddWarning(_ctx, "Your wet hands slow the work.");
            else
                GameDisplay.AddWarning(_ctx, "Your unsteady hands slow the work.");
        }

        // Use centralized progress method - handles web animation and processes all time at once
        var (elapsed, interrupted) = GameDisplay.UpdateAndRenderProgress(_ctx, "Crafting.", totalTime, ActivityType.Crafting);

        if (!_ctx.player.IsAlive)
            return false;

        if (interrupted)
        {
            GameDisplay.AddWarning(_ctx, "Your work was interrupted.");
            return false;
        }

        // Handle shelter rebuild
        if (option.RebuildShelter)
        {
            var oldShelter = _ctx.Camp.GetFeature<ShelterFeature>();
            if (oldShelter == null)
            {
                GameDisplay.AddWarning(_ctx, "No shelter to rebuild.");
                return false;
            }

            // Consume materials for new frame
            foreach (var req in option.Requirements)
            {
                ConsumeMaterial(_ctx.Inventory, req.Material, req.Count);
            }

            // Salvage materials from old shelter
            var salvage = oldShelter.GetSalvageMaterials();
            foreach (var (resource, count) in salvage)
            {
                _ctx.Inventory.Add(resource, count);
            }

            // Remove old shelter and add new log frame
            _ctx.Camp.RemoveFeature(oldShelter);
            var newShelter = ShelterFeature.CreateLogFrame();
            _ctx.Camp.AddFeature(newShelter);

            GameDisplay.AddSuccess(_ctx, "You rebuilt your shelter with a log frame!");
            GameDisplay.AddNarrative(_ctx, newShelter.GetStatusText());

            if (salvage.Count > 0)
            {
                var salvageStr = string.Join(", ", salvage.Select(kvp => $"{kvp.Value} {kvp.Key.ToDisplayName()}"));
                GameDisplay.AddNarrative(_ctx, $"Salvaged: {salvageStr}");
            }

            _ctx.RecordItemCrafted(option.Name);
            GameDisplay.Render(_ctx, statusText: "Satisfied.");
            return true;
        }

        // Handle different recipe outputs
        if (option.ProducesFeature)
        {
            // Feature recipe (e.g., curing rack) - adds to camp location
            var feature = option.CraftFeature(_ctx.Inventory);
            if (feature == null)
                return false;

            // Special handling for bedding - replace existing
            if (feature is BeddingFeature)
            {
                var oldBedding = _ctx.Camp.GetFeature<BeddingFeature>();
                if (oldBedding != null)
                {
                    _ctx.Camp.RemoveFeature(oldBedding);
                    GameDisplay.AddNarrative(_ctx, "You replace your old bedding.");
                }
            }

            // Add the feature to camp
            _ctx.Camp.AddFeature(feature);

            // Check if this is a multi-session project or instant improvement
            if (feature is CraftingProjectFeature project)
            {
                GameDisplay.AddSuccess(_ctx, $"Started construction project: {project.ProjectName}");
                GameDisplay.AddNarrative(_ctx, $"Materials consumed. Work on it from the camp menu to make progress.");
                GameDisplay.AddNarrative(_ctx, $"Total work required: {project.TimeRequiredMinutes} minutes ({project.TimeRequiredMinutes / 60:F1} hours).");

                if (project.BenefitsFromShovel)
                {
                    bool hasShovel = _ctx.Inventory.GetTool(ToolType.Shovel) != null;
                    if (hasShovel)
                    {
                        GameDisplay.AddNarrative(_ctx, "Your shovel will double progress on this digging work.");
                    }
                    else
                    {
                        GameDisplay.AddNarrative(_ctx, "A shovel would double your progress on this digging work.");
                    }
                }
            }
            else
            {
                // Instant improvement
                GameDisplay.AddSuccess(_ctx, $"You built a {option.Name}!");
                GameDisplay.AddNarrative(_ctx, "It's now available at your camp.");
            }
        }
        else
        {
            // Create the gear (or process materials)
            var gear = option.Craft(_ctx.Inventory);

            if (gear == null)
            {
                // Check if this was a mending recipe
                if (option.IsMendingRecipe)
                {
                    var equipment = _ctx.Inventory.GetEquipment(option.MendSlot!.Value);
                    if (equipment != null)
                    {
                        GameDisplay.AddSuccess(_ctx, $"You mended your {equipment.Name}!");
                        GameDisplay.AddNarrative(_ctx, $"Condition: {equipment.ConditionPct:P0}");
                    }
                    else
                    {
                        GameDisplay.AddSuccess(_ctx, "You completed the mending work.");
                    }
                }
                else
                {
                    // Processing recipe - materials were added to inventory
                    GameDisplay.AddSuccess(_ctx, $"You produced {option.GetOutputDescription()}!");
                }
            }
            else
            {
                // Handle different gear categories
                switch (gear.Category)
                {
                    case GearCategory.Equipment:
                        var previous = _ctx.Inventory.Equip(gear);
                        GameDisplay.AddSuccess(_ctx, $"You crafted {gear.Name}!");
                        if (previous != null)
                        {
                            GameDisplay.AddNarrative(_ctx, $"You replace your {previous.Name}.");
                            // Note: previous item is lost - could add to camp storage in future
                        }
                        else
                        {
                            GameDisplay.AddNarrative(_ctx, $"You put on the {gear.Name}.");
                        }
                        break;

                    case GearCategory.Accessory:
                        _ctx.Inventory.Accessories.Add(gear);
                        GameDisplay.AddSuccess(_ctx, $"You crafted {gear.Name}!");
                        GameDisplay.AddNarrative(_ctx, $"You can now carry {_ctx.Inventory.MaxWeightKg:F1} kg total.");
                        break;

                    case GearCategory.Tool:
                        // Auto-equip weapons, otherwise add to tools
                        if (gear.IsWeapon)
                        {
                            var previousWeapon = _ctx.Inventory.EquipWeapon(gear);
                            if (previousWeapon != null)
                            {
                                _ctx.Inventory.Tools.Add(previousWeapon);
                                GameDisplay.AddSuccess(_ctx, $"You crafted a {gear.Name}!");
                                GameDisplay.AddNarrative(_ctx, $"You swap your {previousWeapon.Name} for the {gear.Name}.");
                            }
                            else
                            {
                                GameDisplay.AddSuccess(_ctx, $"You crafted a {gear.Name}!");
                                GameDisplay.AddNarrative(_ctx, $"You equip the {gear.Name}.");
                            }
                        }
                        else
                        {
                            _ctx.Inventory.Tools.Add(gear);
                            GameDisplay.AddSuccess(_ctx, $"You crafted a {gear.Name}!");
                        }
                        if (option.Durability > 0)
                        {
                            GameDisplay.AddNarrative(_ctx, $"It should last for about {option.Durability} uses.");
                        }
                        break;
                }
            }
        }

        // Record crafting discovery
        _ctx.RecordItemCrafted(option.Name);

        GameDisplay.Render(_ctx, statusText: "Satisfied.");

        return true;
    }

    private static string GetCategoryShortLabel(NeedCategory category) => category switch
    {
        NeedCategory.FireStarting => "Fire",
        NeedCategory.CuttingTool => "Tool",
        NeedCategory.HuntingWeapon => "Weapon",
        NeedCategory.Trapping => "Trap",
        NeedCategory.Processing => "Material",
        NeedCategory.Treatment => "Medical",
        NeedCategory.Equipment => "Clothing",
        NeedCategory.Lighting => "Light",
        NeedCategory.Carrying => "Storage",
        NeedCategory.CampInfrastructure => "Camp",
        NeedCategory.Mending => "Repair",
        _ => category.ToString()
    };

    private static string GetNeedLabel(NeedCategory need) => need switch
    {
        NeedCategory.FireStarting => "Fire-starting supplies",
        NeedCategory.CuttingTool => "A cutting tool",
        NeedCategory.HuntingWeapon => "A hunting weapon",
        NeedCategory.Trapping => "Trapping equipment",
        NeedCategory.Processing => "Process materials",
        NeedCategory.Treatment => "Medical treatments",
        NeedCategory.Equipment => "Clothing and gear",
        NeedCategory.Lighting => "Light sources",
        NeedCategory.Carrying => "Carrying gear",
        NeedCategory.Mending => "Equipment mending",
        _ => need.ToString()
    };

    private static string GetNeedDescription(NeedCategory need) => need switch
    {
        NeedCategory.FireStarting => "something to start a fire with",
        NeedCategory.CuttingTool => "a cutting tool",
        NeedCategory.HuntingWeapon => "a hunting weapon",
        NeedCategory.Trapping => "trapping equipment",
        NeedCategory.Processing => "processing raw materials",
        NeedCategory.Treatment => "medical treatments",
        NeedCategory.Equipment => "clothing and gear",
        NeedCategory.Lighting => "a light source",
        NeedCategory.Carrying => "something to carry more",
        NeedCategory.Mending => "mending worn equipment",
        _ => need.ToString().ToLower()
    };

    private static void ConsumeMaterial(Inventory inv, MaterialSpecifier material, int count)
    {
        switch (material)
        {
            case MaterialSpecifier.Specific(var resource):
                inv.Remove(resource, count);
                break;
            case MaterialSpecifier.Category(var category):
                var categoryResources = ResourceCategories.Items[category];
                int remaining = count;
                foreach (var res in categoryResources)
                {
                    while (remaining > 0 && inv.Count(res) > 0)
                    {
                        inv.Pop(res);
                        remaining--;
                    }
                    if (remaining <= 0) break;
                }
                break;
        }
    }

    private bool IsFeatureAlreadyBuilt(CraftOption option)
    {
        if (!option.ProducesFeature)
            return false;

        // Check specific feature types
        if (option.Name == "Curing Rack")
            return _ctx.Camp.GetFeature<CuringRackFeature>() != null;

        // Shelters - only one at camp
        if (option.Name.Contains("Shelter") || option.Name.Contains("Cabin"))
        {
            // Check for existing shelter or shelter project
            if (_ctx.Camp.GetFeature<ShelterFeature>() != null)
                return true;

            // Check for in-progress shelter project
            var projects = _ctx.Camp.Features.OfType<CraftingProjectFeature>();
            return projects.Any(p => p.ProjectName.Contains("Shelter") || p.ProjectName.Contains("Cabin"));
        }

        // Fire pit upgrades - can't have multiple ongoing projects
        if (option.Name.Contains("Fire Pit"))
        {
            var projects = _ctx.Camp.Features.OfType<CraftingProjectFeature>();
            return projects.Any(p => p.ProjectName.Contains("Fire Pit"));
        }

        return false;
    }
}
