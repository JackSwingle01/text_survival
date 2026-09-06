namespace text_survival.Crafting;

public record CraftFamily(string Id, string Section, string Name, string Purpose);

/// <summary>Presentation groups wrap concrete recipes; ingredient rules stay with the recipe.</summary>
public static class CraftFamilies
{
    public static readonly string[] Sections = ["Tools", "Fire & light", "Food gathering", "Clothing & carrying", "Camp", "Supplies"];
    public static readonly CraftFamily[] All =
    [
        new("cutting", "Tools", "Cutting tool", "Make a quick edge or invest in a handled knife."),
        new("axe", "Tools", "Axe", "Fell standing trees for wood."),
        new("shovel", "Tools", "Shovel", "Dig and improve camp structures."),
        new("knapping", "Tools", "Knapping stone", "Shape edges for tools."),
        new("needle", "Tools", "Needle", "Stitch and mend clothing."),
        new("friction", "Fire & light", "Friction fire kit", "Make embers by friction."),
        new("spark", "Fire & light", "Spark fire kit", "Start fires with sparks."),
        new("tinder", "Fire & light", "Prepared tinder", "Prepare a small fire-starting bundle."),
        new("torch", "Fire & light", "Torch", "Carry light into darkness."),
        new("ember", "Fire & light", "Ember carrier", "Take an existing fire with you."),
        new("spear", "Food gathering", "Spear", "Hunt and defend yourself."),
        new("snare", "Food gathering", "Snare", "Set traps for small game."),
        new("rod", "Food gathering", "Fishing rod", "Fish with a line and hook."),
        new("net", "Food gathering", "Fishing net", "Leave a net to gather fish."),
        new("hands", "Clothing & carrying", "Gloves", "Protect your hands from cold."),
        new("head", "Clothing & carrying", "Headwear", "Keep your head warm."),
        new("chest", "Clothing & carrying", "Body covering", "Insulate your torso."),
        new("legs", "Clothing & carrying", "Leggings", "Protect your legs from cold."),
        new("feet", "Clothing & carrying", "Footwear", "Keep your feet protected."),
        new("belt", "Clothing & carrying", "Belt", "Carry tools at your waist."),
        new("pouch", "Clothing & carrying", "Pouch", "Add a little carrying space."),
        new("pack", "Clothing & carrying", "Pack", "Carry supplies for longer journeys."),
        new("shelter", "Camp", "Shelter frame", "Build or improve protection at camp."),
        new("tent", "Camp", "Portable tent", "Bring shelter on your journey."),
        new("bedding", "Camp", "Bedding", "Improve rest and ground insulation."),
        new("rack", "Camp", "Curing rack", "Preserve hides and food."),
        new("pit", "Camp", "Fire pit", "Invest in a more protected fire."),
        new("hide", "Supplies", "Hide preparation", "Prepare hides for curing."),
        new("fat", "Supplies", "Fat rendering", "Turn raw fat into tallow."),
        new("cordage", "Supplies", "Cordage preparation", "Prepare fiber and rope for equipment."),
        new("tea", "Supplies", "Tea", "Prepare a treatment to carry."),
        new("dressing", "Supplies", "Dressings", "Prepare bandages and poultices.")
    ];
    public static CraftFamily Get(string id) => All.Single(f => f.Id == id);

    public static void Assign(CraftOption option)
    {
        if (string.IsNullOrWhiteSpace(option.Id)) throw new InvalidOperationException($"Missing recipe ID: {option.Name}");
        option.FamilyId = option.Category switch
        {
            NeedCategory.CuttingTool => option.Name switch
            {
                "Stone Axe" => "axe", "Bone Shovel" => "shovel", "Knapping Stone" => "knapping", "Bone Needle" => "needle", _ => "cutting"
            },
            NeedCategory.FireStarting => option.Name switch
            {
                "Hand Drill" or "Bow Drill" => "friction", "Tinder Bundle" => "tinder", _ => "spark"
            },
            NeedCategory.Lighting => option.Name.Contains("Torch") ? "torch" : "ember",
            NeedCategory.HuntingWeapon => "spear",
            NeedCategory.Trapping => "snare",
            NeedCategory.Fishing => option.Name == "Fishing Rod" ? "rod" : "net",
            NeedCategory.Carrying => option.Name.Contains("Belt") ? "belt" : option.Name.Contains("Pouch") ? "pouch" : "pack",
            NeedCategory.Equipment or NeedCategory.Mending => (option.MendSlot ?? option.GearFactory?.Invoke(option.Durability).Slot) switch
            {
                Items.EquipSlot.Hands => "hands", Items.EquipSlot.Head => "head", Items.EquipSlot.Chest => "chest",
                Items.EquipSlot.Legs => "legs", Items.EquipSlot.Feet => "feet", _ => throw new InvalidOperationException(option.Name)
            },
            NeedCategory.Processing => option.Name switch
            {
                "Scrape Hide" => "hide", "Render Fat" => "fat", _ => "cordage"
            },
            NeedCategory.Treatment => option.Name.Contains("Tea") ? "tea" : "dressing",
            NeedCategory.CampInfrastructure => option.Name switch
            {
                "Curing Rack" => "rack",
                _ when option.Name.Contains("Tent") => "tent",
                _ when option.Name.Contains("Bedding") || option.Name.Contains("Sleeping Bag") => "bedding",
                _ when option.Name.Contains("Fire Pit") => "pit",
                _ => "shelter"
            },
            _ => throw new InvalidOperationException($"No family for {option.Name}")
        };
        option.Method = option.IsMendingRecipe ? "Maintain" : option.RebuildShelter ? "Improve" :
            option.Name is "Crude Edge" or "Sharp Rock" ? "Quick edge" : option.FamilyId == "cutting" ? "Handled knife" : "Make";
    }
}
