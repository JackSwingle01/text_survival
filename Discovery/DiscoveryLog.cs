using System.Text.Json.Serialization;
using text_survival.Actors.Animals;
using text_survival.Crafting;
using text_survival.Environments.Factories;
using text_survival.Desktop.Dto;

namespace text_survival.Discovery;

/// <summary>
/// Tracks player discoveries throughout a run for the Discovery Log system.
/// Five categories: Locations, Beasts, Provisions (food), Medicine, Works (crafted items).
/// Binary tracking: either discovered or unknown (shown as ???).
/// </summary>
public class DiscoveryLog
{
    /// <summary>Named locations visited (location names).</summary>
    public HashSet<string> DiscoveredLocations { get; set; } = new();

    /// <summary>Animals encountered (any interaction: combat, fled, saw during event, hunted).</summary>
    public HashSet<AnimalType> EncounteredAnimals { get; set; } = new();

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
    /// Check if a specific location has been discovered.
    /// </summary>
    public bool HasDiscoveredLocation(string locationName) =>
        DiscoveredLocations.Contains(locationName);

    /// <summary>
    /// Check if a specific animal has been encountered.
    /// </summary>
    public bool HasEncounteredAnimal(AnimalType animalType) =>
        EncounteredAnimals.Contains(animalType);

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
