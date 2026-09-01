using text_survival.Items;

namespace text_survival.UI;

/// <summary>What the player asked the fire screen to do.</summary>
public enum FireAction
{
    StartFire,
    StartFromEmber,
    AddFuel,
    CollectCharcoal,
    LightTorch,
    CollectEmber
}

public class FireOverlayResult
{
    public FireAction Action { get; set; }
    public Gear? Tool { get; set; }
    public Resource? Tinder { get; set; }
    public Gear? EmberCarrier { get; set; }
    public Resource? FuelResource { get; set; }
}

/// <summary>What the player asked the food screen to do.</summary>
public enum FoodAction
{
    Eat,
    Drink,
    CookMeat,
    CookFish,
    MeltSnow
}

/// <summary>
/// A food action that passes game time, handed back for the caller to run under a
/// progress view. Instant actions (eating, drinking) are applied by the screen itself.
/// </summary>
public class PendingFoodAction
{
    public FoodAction Action { get; set; }
    public string ItemId { get; set; } = "";
    public int Minutes { get; set; }
}
