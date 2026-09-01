using System.Text.Json.Serialization;
using text_survival.Actors.Animals;
using text_survival.Crafting;
using text_survival.Environments.Factories;
using text_survival.UI;

namespace text_survival.Discovery;

/// <summary>
/// Tracks player discoveries throughout a run for the Discovery Log system.
/// Five categories: Locations, Beasts, Provisions (food), Medicine, Works (crafted items).
/// Also tracks discovered resources for recipe unlocking (The Long Dark-style progression).
/// Binary tracking: either discovered or unknown (shown as ???).
/// </summary>
public class DiscoveryLog
{
    /// <summary>Named locations visited (location names).</summary>
    public HashSet<string> DiscoveredLocations { get; set; } = new();

    /// <summary>Animals encountered (any interaction: combat, fled, saw during event, hunted).</summary>
    public HashSet<AnimalType> EncounteredAnimals { get; set; } = new();

    /// <summary>Resources the player has acquired at least once. Used for recipe unlocking.</summary>
    public HashSet<Resource> DiscoveredResources { get; set; } = new();

    /// <summary>
    /// Single source of truth for resources the player knows about at game start.
    /// These don't require discovery to unlock recipes.
    /// </summary>
    private static readonly Resource[] StartingKnownResources =
    [
        Resource.Stick, Resource.Tinder, Resource.Stone, Resource.Water,
        Resource.PlantFiber, Resource.Pine, Resource.Birch, Resource.Oak
    ];

    /// <summary>
    /// Initialize the starting resource knowledge for a new game.
    /// Call this once when creating a new GameContext.
    /// </summary>
    public void InitializeStartingKnowledge()
    {
        foreach (var r in StartingKnownResources)
            DiscoveredResources.Add(r);
    }

    /// <summary>
    /// Discover a new resource. Returns true if this was a new discovery.
    /// </summary>
    public bool DiscoverResource(Resource resource) => DiscoveredResources.Add(resource);

    /// <summary>
    /// Check if the player has discovered all resources required for a recipe.
    /// Used by crafting system to filter visible recipes.
    /// </summary>
    public bool HasDiscoveredAllRequirements(IEnumerable<MaterialRequirement> requirements)
    {
        foreach (var req in requirements)
        {
            switch (req.Material)
            {
                case MaterialSpecifier.Specific(var resource):
                    if (!DiscoveredResources.Contains(resource)) return false;
                    break;
                case MaterialSpecifier.Category(var category):
                    // Category satisfied if ANY resource in category is known
                    if (!ResourceCategories.Items[category].Any(DiscoveredResources.Contains))
                        return false;
                    break;
            }
        }
        return true;
    }

    /// <summary>
    /// Get list of resources missing to unlock a recipe.
    /// Returns empty list if all requirements are discovered.
    /// </summary>
    public List<Resource> GetMissingResources(IEnumerable<MaterialRequirement> requirements)
    {
        var missing = new List<Resource>();
        foreach (var req in requirements)
        {
            switch (req.Material)
            {
                case MaterialSpecifier.Specific(var resource):
                    if (!DiscoveredResources.Contains(resource))
                        missing.Add(resource);
                    break;
                case MaterialSpecifier.Category(var category):
                    // Category satisfied if ANY resource in category is known
                    if (!ResourceCategories.Items[category].Any(DiscoveredResources.Contains))
                    {
                        // Return first undiscovered resource from category as hint
                        var hint = ResourceCategories.Items[category]
                            .FirstOrDefault(r => !DiscoveredResources.Contains(r));
                        if (hint != default)
                            missing.Add(hint);
                    }
                    break;
            }
        }
        return missing.Distinct().ToList();
    }

    /// <summary>Foods eaten (resource or food item names).</summary>
    public HashSet<string> FoodsEaten { get; set; } = new();

    /// <summary>Medicines/treatments used (treatment names).</summary>
    public HashSet<string> MedicinesUsed { get; set; } = new();

    /// <summary>Items crafted (gear/recipe names).</summary>
    public HashSet<string> ItemsCrafted { get; set; } = new();

    // Expected totals for each category (for "X / ~Y" display)
    // Calculated dynamically from game content at construction
    [JsonIgnore]
    public int ExpectedLocations { get; private set; }
    [JsonIgnore]
    public int ExpectedBeasts { get; private set; }
    [JsonIgnore]
    public int ExpectedFoods { get; private set; }
    [JsonIgnore]
    public int ExpectedMedicines { get; private set; }
    [JsonIgnore]
    public int ExpectedWorks { get; private set; }

    public DiscoveryLog()
    {
        ExpectedBeasts = CalculateExpectedBeasts();
        ExpectedFoods = CalculateExpectedFoods();
        ExpectedMedicines = CalculateExpectedMedicines();
        ExpectedWorks = CalculateExpectedWorks();
        ExpectedLocations = CalculateExpectedLocations();
    }

    private int CalculateExpectedBeasts() => Enum.GetValues<AnimalType>().Length;

    private int CalculateExpectedFoods() =>
        ResourceCategories.Items[ResourceCategory.Food].Count;

    private int CalculateExpectedMedicines()
    {
        int resources = ResourceCategories.Items[ResourceCategory.Medicine].Count;
        var crafting = new NeedCraftingSystem();
        int treatments = crafting.AllOptions.Count(opt => opt.Category == NeedCategory.Treatment);
        return resources + treatments;
    }

    private int CalculateExpectedWorks()
    {
        var crafting = new NeedCraftingSystem();
        return crafting.AllOptions.Count;
    }

    private int CalculateExpectedLocations() =>
        GridWorldGenerator.GetUniqueLocationCount();

    /// <summary>
    /// Record discovery of a named location.
    /// Returns true if this was a new discovery.
    /// </summary>
    public bool DiscoverLocation(string locationName)
    {
        if (string.IsNullOrWhiteSpace(locationName)) return false;
        return DiscoveredLocations.Add(locationName);
    }

    /// <summary>
    /// Record encounter with an animal (any interaction).
    /// Returns true if this was a new discovery.
    /// </summary>
    public bool EncounterAnimal(AnimalType animalType)
    {
        return EncounteredAnimals.Add(animalType);
    }

    /// <summary>
    /// Record eating a food item.
    /// Returns true if this was a new discovery.
    /// </summary>
    public bool EatFood(string foodName)
    {
        if (string.IsNullOrWhiteSpace(foodName)) return false;
        return FoodsEaten.Add(foodName);
    }

    /// <summary>
    /// Record using a medicine/treatment.
    /// Returns true if this was a new discovery.
    /// </summary>
    public bool UseMedicine(string medicineName)
    {
        if (string.IsNullOrWhiteSpace(medicineName)) return false;
        return MedicinesUsed.Add(medicineName);
    }

    /// <summary>
    /// Record crafting an item.
    /// Returns true if this was a new discovery.
    /// </summary>
    public bool CraftItem(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName)) return false;
        return ItemsCrafted.Add(itemName);
    }

    /// <summary>
    /// Get total discoveries across all categories.
    /// </summary>
    public int TotalDiscoveries =>
        DiscoveredLocations.Count +
        EncounteredAnimals.Count +
        FoodsEaten.Count +
        MedicinesUsed.Count +
        ItemsCrafted.Count;

    /// <summary>
    /// Get display string for a category showing discovery progress.
    /// Early game: just "X discovered"
    /// Later (after finding 5+ in category): "X / ~Y"
    /// </summary>
    public string GetCategoryDisplay(DiscoveryCategory category)
    {
        var (count, expected) = category switch
        {
            DiscoveryCategory.Locations => (DiscoveredLocations.Count, ExpectedLocations),
            DiscoveryCategory.Beasts => (EncounteredAnimals.Count, ExpectedBeasts),
            DiscoveryCategory.Provisions => (FoodsEaten.Count, ExpectedFoods),
            DiscoveryCategory.Medicine => (MedicinesUsed.Count, ExpectedMedicines),
            DiscoveryCategory.Works => (ItemsCrafted.Count, ExpectedWorks),
            _ => (0, 0)
        };

        // Show approximate total once player has found enough to know there's more
        if (count >= 5)
            return $"{count} / ~{expected}";
        return $"{count} discovered";
    }

    /// <summary>
    /// Build the DTO for the Discovery Log overlay.
    /// </summary>
    public DiscoveryLogDto ToDto()
    {
        var categories = new List<DiscoveryLogCategoryDto>
        {
            BuildCategory("The Land", DiscoveryCategory.Locations,
                DiscoveredLocations.OrderBy(x => x).ToList(),
                ExpectedLocations),
            BuildCategory("Beasts", DiscoveryCategory.Beasts,
                EncounteredAnimals.Select(a => a.DisplayName()).OrderBy(x => x).ToList(),
                ExpectedBeasts),
            BuildCategory("Provisions", DiscoveryCategory.Provisions,
                FoodsEaten.OrderBy(x => x).ToList(),
                ExpectedFoods),
            BuildCategory("Medicine", DiscoveryCategory.Medicine,
                MedicinesUsed.OrderBy(x => x).ToList(),
                ExpectedMedicines),
            BuildCategory("Works", DiscoveryCategory.Works,
                ItemsCrafted.OrderBy(x => x).ToList(),
                ExpectedWorks)
        };

        return new DiscoveryLogDto(categories);
    }

    private DiscoveryLogCategoryDto BuildCategory(
        string name,
        DiscoveryCategory category,
        List<string> discovered,
        int expectedTotal)
    {
        return new DiscoveryLogCategoryDto(
            Name: name,
            CountDisplay: GetCategoryDisplay(category),
            Discovered: discovered,
            RemainingCount: Math.Max(0, expectedTotal - discovered.Count)
        );
    }
}

/// <summary>
/// Categories for the Discovery Log display.
/// </summary>
public enum DiscoveryCategory
{
    Locations,   // The Land - named locations visited
    Beasts,      // Animals encountered
    Provisions,  // Foods eaten
    Medicine,    // Treatments used
    Works        // Items crafted
}
