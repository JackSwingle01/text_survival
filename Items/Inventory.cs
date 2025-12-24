using text_survival.Actions;

namespace text_survival.Items;

/// <summary>
/// Aggregate-based inventory using List of weights for resources.
/// Used for both player inventory (with weight limit) and camp storage (unlimited).
/// </summary>
public class Inventory
{
    // Capacity (-1 = unlimited, e.g., camp storage)
    public double MaxWeightKg { get; set; } = -1;

    // Fire supplies - each entry is item weight in kg
    public Stack<double> Logs { get; set; } = new();      // ~2kg each, ~60min burn
    public Stack<double> Sticks { get; set; } = new();    // ~0.3kg each, ~10min burn
    public Stack<double> Tinder { get; set; } = new();    // ~0.05kg each, for fire starting

    // Food - each entry is portion weight in kg
    public Stack<double> CookedMeat { get; set; } = new();
    public Stack<double> RawMeat { get; set; } = new();
    public Stack<double> Berries { get; set; } = new();

    // Water in liters
    public double WaterLiters { get; set; }

    // Crafting materials - each entry is item weight in kg
    public Stack<double> Stone { get; set; } = new();       // Generic stone, throwing/fire pits
    public Stack<double> Bone { get; set; } = new();        // From butchering
    public Stack<double> Hide { get; set; } = new();        // Fresh hide from butchering
    public Stack<double> PlantFiber { get; set; } = new();  // Processed plant fibers
    public Stack<double> Sinew { get; set; } = new();       // From butchering

    // Stone types - specialized knapping materials
    public Stack<double> Shale { get; set; } = new();       // Easy to knap, fragile tools
    public Stack<double> Flint { get; set; } = new();       // Durable tools, harder to find
    public double Pyrite { get; set; }                   // Strike-a-light material (kg, precious)

    // Wood types - replace generic logs with specific properties
    public Stack<double> Pine { get; set; } = new();        // Resinous, burns fast, good for starting
    public Stack<double> Birch { get; set; } = new();       // Flexible, moderate burn
    public Stack<double> Oak { get; set; } = new();         // Dense, slow burn, overnight fuel
    public Stack<double> BirchBark { get; set; } = new();   // Tinder, containers, waterproofing

    // Fungi - foraged from trees year-round
    public Stack<double> BirchPolypore { get; set; } = new();  // Wound treatment, antibacterial
    public Stack<double> Chaga { get; set; } = new();          // Anti-inflammatory, general health
    public Stack<double> Amadou { get; set; } = new();         // Fire-starting AND wound dressing

    // Persistent plants - forageable in winter
    public Stack<double> RoseHips { get; set; } = new();       // Vitamin C, immune
    public Stack<double> JuniperBerries { get; set; } = new(); // Antiseptic, digestive
    public Stack<double> WillowBark { get; set; } = new();     // Pain relief, sprains
    public Stack<double> PineNeedles { get; set; } = new();    // Vitamin C tea, respiratory

    // Tree products
    public Stack<double> PineResin { get; set; } = new();      // Wound sealing, waterproofing
    public Stack<double> Usnea { get; set; } = new();          // Old man's beard lichen, antimicrobial
    public Stack<double> Sphagnum { get; set; } = new();       // Peat moss, absorbent wound dressing

    // Produced (not foraged)
    public double Charcoal { get; set; }                       // From fire remnants

    // Food expansion
    public Stack<double> Nuts { get; set; } = new();        // Calorie-dense, forageable
    public Stack<double> Roots { get; set; } = new();       // Require cooking
    public Stack<double> DriedMeat { get; set; } = new();   // Preserved, lighter
    public Stack<double> DriedBerries { get; set; } = new();// Preserved

    // Processing states
    public Stack<double> ScrapedHide { get; set; } = new(); // Processed from fresh hide
    public Stack<double> CuredHide { get; set; } = new();   // Finished leather
    public Stack<double> RawFiber { get; set; } = new();    // Unprocessed plant material
    public Stack<double> RawFat { get; set; } = new();      // From butchering
    public Stack<double> Tallow { get; set; } = new();      // Rendered fat

    // Discrete items - identity matters
    public List<Tool> Tools { get; set; } = [];
    public List<Item> Special { get; set; } = [];  // Quest items, trophies

    // Equipment slots (5 armor + 1 weapon)
    public Equipment? Head { get; set; }
    public Equipment? Chest { get; set; }
    public Equipment? Legs { get; set; }
    public Equipment? Feet { get; set; }
    public Equipment? Hands { get; set; }
    public Tool? Weapon { get; set; }

    // Active torch tracking
    public Tool? ActiveTorch { get; set; }
    public double TorchBurnTimeRemainingMinutes { get; set; }

    /// <summary>Check if player has a lit torch burning.</summary>
    public bool HasLitTorch => ActiveTorch != null && TorchBurnTimeRemainingMinutes > 0;

    /// <summary>Check if player has an unlit torch in inventory.</summary>
    public bool HasUnlitTorch => Tools.Any(t => t.Type == ToolType.Torch && t.Works);

    /// <summary>Check if player has tinder for fire-starting.</summary>
    public bool HasTinder => Tinder.Count > 0;

    /// <summary>
    /// Get torch heat bonus in °F (5°F when fresh, 3°F when nearly spent).
    /// </summary>
    public double GetTorchHeatBonusF()
    {
        if (!HasLitTorch) return 0;
        double burnPct = TorchBurnTimeRemainingMinutes / 60.0;
        return 3.0 + (2.0 * burnPct);  // 5°F fresh, 3°F nearly spent
    }

    /// <summary>
    /// Light a torch from inventory. Sets it as active with 60 min burn time.
    /// Returns true if successful.
    /// </summary>
    public bool LightTorch()
    {
        var torch = Tools.FirstOrDefault(t => t.Type == ToolType.Torch && t.Works);
        if (torch == null) return false;

        Tools.Remove(torch);
        ActiveTorch = torch;
        TorchBurnTimeRemainingMinutes = 60;
        return true;
    }

    // Weight calculations
    public double FuelWeightKg => Logs.Sum() + Sticks.Sum() + Tinder.Sum();
    public double FoodWeightKg => CookedMeat.Sum() + RawMeat.Sum() + Berries.Sum() +
        Nuts.Sum() + Roots.Sum() + DriedMeat.Sum() + DriedBerries.Sum();
    public double WaterWeightKg => WaterLiters;  // 1L water = 1kg
    public double CraftingMaterialsWeightKg =>
        Stone.Sum() + Bone.Sum() + Hide.Sum() + PlantFiber.Sum() + Sinew.Sum() +
        Shale.Sum() + Flint.Sum() + Pyrite +
        ScrapedHide.Sum() + CuredHide.Sum() + RawFiber.Sum() + RawFat.Sum() + Tallow.Sum();
    public double WoodWeightKg => Pine.Sum() + Birch.Sum() + Oak.Sum() + BirchBark.Sum();
    public double MedicinalWeightKg => BirchPolypore.Sum() + Chaga.Sum() + Amadou.Sum() +
        RoseHips.Sum() + JuniperBerries.Sum() + WillowBark.Sum() + PineNeedles.Sum() +
        PineResin.Sum() + Usnea.Sum() + Sphagnum.Sum() + Charcoal;
    public double ToolsWeightKg => Tools.Sum(t => t.Weight);
    public double SpecialWeightKg => Special.Sum(i => i.Weight);

    public double EquipmentWeightKg =>
        (Head?.Weight ?? 0) + (Chest?.Weight ?? 0) + (Legs?.Weight ?? 0) +
        (Feet?.Weight ?? 0) + (Hands?.Weight ?? 0) + (Weapon?.Weight ?? 0);

    public double CurrentWeightKg =>
        FuelWeightKg + FoodWeightKg + WaterWeightKg + CraftingMaterialsWeightKg +
        WoodWeightKg + MedicinalWeightKg + ToolsWeightKg + SpecialWeightKg + EquipmentWeightKg +
        (ActiveTorch?.Weight ?? 0);

    /// <summary>
    /// Total insulation from all worn equipment (0-1 scale per slot, summed).
    /// </summary>
    public double TotalInsulation =>
        (Head?.Insulation ?? 0) + (Chest?.Insulation ?? 0) + (Legs?.Insulation ?? 0) +
        (Feet?.Insulation ?? 0) + (Hands?.Insulation ?? 0);

    public bool CanCarry(double additionalKg) =>
        MaxWeightKg < 0 || CurrentWeightKg + additionalKg <= MaxWeightKg;

    public double RemainingCapacityKg =>
        MaxWeightKg < 0 ? double.MaxValue : Math.Max(0, MaxWeightKg - CurrentWeightKg);

    // Fuel burn time estimates (rough: logs ~30min/kg, sticks ~30min/kg, tinder ~10min/kg)
    public double TotalFuelBurnTimeMinutes =>
        Logs.Sum() * 30 + Sticks.Sum() * 30 + Tinder.Sum() * 10;

    public double TotalFuelBurnTimeHours => TotalFuelBurnTimeMinutes / 60;

    /// <summary>
    /// Combine another inventory into this one (for foraging, harvesting, etc.).
    /// </summary>
    public void Combine(Inventory other)
    {
        // Fuel
        foreach (var item in other.Logs) Logs.Push(item);
        foreach (var item in other.Sticks) Sticks.Push(item);
        foreach (var item in other.Tinder) Tinder.Push(item);

        // Food
        foreach (var item in other.CookedMeat) CookedMeat.Push(item);
        foreach (var item in other.RawMeat) RawMeat.Push(item);
        foreach (var item in other.Berries) Berries.Push(item);
        foreach (var item in other.Nuts) Nuts.Push(item);
        foreach (var item in other.Roots) Roots.Push(item);
        foreach (var item in other.DriedMeat) DriedMeat.Push(item);
        foreach (var item in other.DriedBerries) DriedBerries.Push(item);
        WaterLiters += other.WaterLiters;

        // Base crafting materials
        foreach (var item in other.Stone) Stone.Push(item);
        foreach (var item in other.Bone) Bone.Push(item);
        foreach (var item in other.Hide) Hide.Push(item);
        foreach (var item in other.PlantFiber) PlantFiber.Push(item);
        foreach (var item in other.Sinew) Sinew.Push(item);

        // Stone types
        foreach (var item in other.Shale) Shale.Push(item);
        foreach (var item in other.Flint) Flint.Push(item);
        Pyrite += other.Pyrite;

        // Wood types
        foreach (var item in other.Pine) Pine.Push(item);
        foreach (var item in other.Birch) Birch.Push(item);
        foreach (var item in other.Oak) Oak.Push(item);
        foreach (var item in other.BirchBark) BirchBark.Push(item);

        // Medicinals - fungi
        foreach (var item in other.BirchPolypore) BirchPolypore.Push(item);
        foreach (var item in other.Chaga) Chaga.Push(item);
        foreach (var item in other.Amadou) Amadou.Push(item);
        // Medicinals - persistent plants
        foreach (var item in other.RoseHips) RoseHips.Push(item);
        foreach (var item in other.JuniperBerries) JuniperBerries.Push(item);
        foreach (var item in other.WillowBark) WillowBark.Push(item);
        foreach (var item in other.PineNeedles) PineNeedles.Push(item);
        // Medicinals - tree products
        foreach (var item in other.PineResin) PineResin.Push(item);
        foreach (var item in other.Usnea) Usnea.Push(item);
        foreach (var item in other.Sphagnum) Sphagnum.Push(item);
        Charcoal += other.Charcoal;

        // Processing states
        foreach (var item in other.ScrapedHide) ScrapedHide.Push(item);
        foreach (var item in other.CuredHide) CuredHide.Push(item);
        foreach (var item in other.RawFiber) RawFiber.Push(item);
        foreach (var item in other.RawFat) RawFat.Push(item);
        foreach (var item in other.Tallow) Tallow.Push(item);

        // Discrete items
        Tools.AddRange(other.Tools);
        Special.AddRange(other.Special);
    }

    /// <summary>
    /// Get material count by name (for crafting system).
    /// </summary>
    public int GetCount(string material) => material switch
    {
        // Fuel
        "Sticks" => Sticks.Count,
        "Logs" => Logs.Count,
        "Tinder" => Tinder.Count,

        // Base materials
        "Stone" => Stone.Count,
        "Bone" => Bone.Count,
        "Hide" => Hide.Count,
        "PlantFiber" => PlantFiber.Count,
        "Sinew" => Sinew.Count,

        // Stone types
        "Shale" => Shale.Count,
        "Flint" => Flint.Count,
        "Pyrite" => (int)(Pyrite / 0.05),  // 0.05kg per "unit" for crafting

        // Wood types
        "Pine" => Pine.Count,
        "Birch" => Birch.Count,
        "Oak" => Oak.Count,
        "BirchBark" => BirchBark.Count,

        // Medicinals - fungi
        "BirchPolypore" => BirchPolypore.Count,
        "Chaga" => Chaga.Count,
        "Amadou" => Amadou.Count,
        // Medicinals - persistent plants
        "RoseHips" => RoseHips.Count,
        "JuniperBerries" => JuniperBerries.Count,
        "WillowBark" => WillowBark.Count,
        "PineNeedles" => PineNeedles.Count,
        // Medicinals - tree products
        "PineResin" => PineResin.Count,
        "Usnea" => Usnea.Count,
        "Sphagnum" => Sphagnum.Count,

        // Food
        "Nuts" => Nuts.Count,
        "Roots" => Roots.Count,
        "DriedMeat" => DriedMeat.Count,
        "DriedBerries" => DriedBerries.Count,

        // Processing states
        "ScrapedHide" => ScrapedHide.Count,
        "CuredHide" => CuredHide.Count,
        "RawFiber" => RawFiber.Count,
        "RawFat" => RawFat.Count,
        "Tallow" => Tallow.Count,

        _ => 0
    };

    /// <summary>
    /// Take one unit of material by name (for crafting system).
    /// </summary>
    public double Take(string material) => material switch
    {
        // Fuel
        "Sticks" => Sticks.TryPop(out var s) ? s : 0,
        "Logs" => Logs.TryPop(out var l) ? l : 0,
        "Tinder" => Tinder.TryPop(out var t) ? t : 0,

        // Base materials
        "Stone" => Stone.TryPop(out var st) ? st : 0,
        "Bone" => Bone.TryPop(out var b) ? b : 0,
        "Hide" => Hide.TryPop(out var h) ? h : 0,
        "PlantFiber" => PlantFiber.TryPop(out var pf) ? pf : 0,
        "Sinew" => Sinew.TryPop(out var si) ? si : 0,

        // Stone types
        "Shale" => Shale.TryPop(out var sh) ? sh : 0,
        "Flint" => Flint.TryPop(out var fl) ? fl : 0,
        "Pyrite" => TakePyrite(),

        // Wood types
        "Pine" => Pine.TryPop(out var pi) ? pi : 0,
        "Birch" => Birch.TryPop(out var bi) ? bi : 0,
        "Oak" => Oak.TryPop(out var o) ? o : 0,
        "BirchBark" => BirchBark.TryPop(out var bb) ? bb : 0,

        // Medicinals - fungi
        "BirchPolypore" => BirchPolypore.TryPop(out var bp) ? bp : 0,
        "Chaga" => Chaga.TryPop(out var ch) ? ch : 0,
        "Amadou" => Amadou.TryPop(out var a) ? a : 0,
        // Medicinals - persistent plants
        "RoseHips" => RoseHips.TryPop(out var rh) ? rh : 0,
        "JuniperBerries" => JuniperBerries.TryPop(out var jb) ? jb : 0,
        "WillowBark" => WillowBark.TryPop(out var wb) ? wb : 0,
        "PineNeedles" => PineNeedles.TryPop(out var pn) ? pn : 0,
        // Medicinals - tree products
        "PineResin" => PineResin.TryPop(out var pr) ? pr : 0,
        "Usnea" => Usnea.TryPop(out var us) ? us : 0,
        "Sphagnum" => Sphagnum.TryPop(out var sp) ? sp : 0,

        // Food
        "Nuts" => Nuts.TryPop(out var n) ? n : 0,
        "Roots" => Roots.TryPop(out var r) ? r : 0,
        "DriedMeat" => DriedMeat.TryPop(out var dm) ? dm : 0,
        "DriedBerries" => DriedBerries.TryPop(out var db) ? db : 0,

        // Processing states
        "ScrapedHide" => ScrapedHide.TryPop(out var sch) ? sch : 0,
        "CuredHide" => CuredHide.TryPop(out var ch) ? ch : 0,
        "RawFiber" => RawFiber.TryPop(out var rf) ? rf : 0,
        "RawFat" => RawFat.TryPop(out var rft) ? rft : 0,
        "Tallow" => Tallow.TryPop(out var ta) ? ta : 0,

        _ => 0
    };

    /// <summary>
    /// Take multiple units of material by name.
    /// </summary>
    public void Take(string material, int count)
    {
        for (int i = 0; i < count; i++)
            Take(material);
    }

    /// <summary>
    /// Check if inventory has no resources (used for forage results).
    /// </summary>
    public bool IsEmpty =>
        // Fuel
        Logs.Count == 0 && Sticks.Count == 0 && Tinder.Count == 0 &&
        // Food
        CookedMeat.Count == 0 && RawMeat.Count == 0 && Berries.Count == 0 &&
        Nuts.Count == 0 && Roots.Count == 0 &&
        DriedMeat.Count == 0 && DriedBerries.Count == 0 && WaterLiters == 0 &&
        // Base materials
        Stone.Count == 0 && Bone.Count == 0 && Hide.Count == 0 &&
        PlantFiber.Count == 0 && Sinew.Count == 0 &&
        // Stone types
        Shale.Count == 0 && Flint.Count == 0 && Pyrite == 0 &&
        // Wood types
        Pine.Count == 0 && Birch.Count == 0 && Oak.Count == 0 && BirchBark.Count == 0 &&
        // Medicinals
        BirchPolypore.Count == 0 && Chaga.Count == 0 && Amadou.Count == 0 &&
        RoseHips.Count == 0 && JuniperBerries.Count == 0 && WillowBark.Count == 0 &&
        PineNeedles.Count == 0 && PineResin.Count == 0 && Usnea.Count == 0 &&
        Sphagnum.Count == 0 && Charcoal == 0 &&
        // Processing states
        ScrapedHide.Count == 0 && CuredHide.Count == 0 && RawFiber.Count == 0 &&
        RawFat.Count == 0 && Tallow.Count == 0 &&
        // Discrete items
        Tools.Count == 0 && Special.Count == 0;

    /// <summary>
    /// Get a readable description of contents (for display after foraging, etc.).
    /// </summary>
    public string GetDescription()
    {
        var parts = new List<string>();

        // Fuel
        if (Logs.Count > 0) parts.Add($"{Logs.Count} log{(Logs.Count > 1 ? "s" : "")}");
        if (Sticks.Count > 0) parts.Add($"{Sticks.Count} stick{(Sticks.Count > 1 ? "s" : "")}");
        if (Tinder.Count > 0) parts.Add($"{Tinder.Count} tinder");

        // Wood types
        if (Pine.Count > 0) parts.Add($"{Pine.Count} pine log{(Pine.Count > 1 ? "s" : "")}");
        if (Birch.Count > 0) parts.Add($"{Birch.Count} birch log{(Birch.Count > 1 ? "s" : "")}");
        if (Oak.Count > 0) parts.Add($"{Oak.Count} oak log{(Oak.Count > 1 ? "s" : "")}");
        if (BirchBark.Count > 0) parts.Add($"{BirchBark.Count} birch bark");

        // Food
        if (CookedMeat.Count > 0) parts.Add($"{CookedMeat.Count} cooked meat");
        if (RawMeat.Count > 0) parts.Add($"{RawMeat.Count} raw meat");
        if (DriedMeat.Count > 0) parts.Add($"{DriedMeat.Count} dried meat");
        if (Berries.Count > 0) parts.Add($"{Berries.Count} berries");
        if (DriedBerries.Count > 0) parts.Add($"{DriedBerries.Count} dried berries");
        if (Nuts.Count > 0) parts.Add($"{Nuts.Count} nut{(Nuts.Count > 1 ? "s" : "")}");
        if (Roots.Count > 0) parts.Add($"{Roots.Count} root{(Roots.Count > 1 ? "s" : "")}");
        if (WaterLiters > 0) parts.Add($"{WaterLiters:F1}L water");

        // Base materials
        if (Stone.Count > 0) parts.Add($"{Stone.Count} stone{(Stone.Count > 1 ? "s" : "")}");
        if (Bone.Count > 0) parts.Add($"{Bone.Count} bone{(Bone.Count > 1 ? "s" : "")}");
        if (Hide.Count > 0) parts.Add($"{Hide.Count} hide{(Hide.Count > 1 ? "s" : "")}");
        if (PlantFiber.Count > 0) parts.Add($"{PlantFiber.Count} plant fiber");
        if (Sinew.Count > 0) parts.Add($"{Sinew.Count} sinew");

        // Stone types
        if (Shale.Count > 0) parts.Add($"{Shale.Count} shale");
        if (Flint.Count > 0) parts.Add($"{Flint.Count} flint");
        if (Pyrite > 0) parts.Add($"{Pyrite:F2}kg pyrite");

        // Medicinals - fungi
        if (BirchPolypore.Count > 0) parts.Add($"{BirchPolypore.Count} birch polypore");
        if (Chaga.Count > 0) parts.Add($"{Chaga.Count} chaga");
        if (Amadou.Count > 0) parts.Add($"{Amadou.Count} amadou");
        // Medicinals - plants
        if (RoseHips.Count > 0) parts.Add($"{RoseHips.Count} rose hip{(RoseHips.Count > 1 ? "s" : "")}");
        if (JuniperBerries.Count > 0) parts.Add($"{JuniperBerries.Count} juniper berries");
        if (WillowBark.Count > 0) parts.Add($"{WillowBark.Count} willow bark");
        if (PineNeedles.Count > 0) parts.Add($"{PineNeedles.Count} pine needles");
        // Medicinals - tree products
        if (PineResin.Count > 0) parts.Add($"{PineResin.Count} pine resin");
        if (Usnea.Count > 0) parts.Add($"{Usnea.Count} usnea");
        if (Sphagnum.Count > 0) parts.Add($"{Sphagnum.Count} sphagnum moss");
        if (Charcoal > 0) parts.Add($"{Charcoal:F2}kg charcoal");

        // Processing states
        if (ScrapedHide.Count > 0) parts.Add($"{ScrapedHide.Count} scraped hide{(ScrapedHide.Count > 1 ? "s" : "")}");
        if (CuredHide.Count > 0) parts.Add($"{CuredHide.Count} cured hide{(CuredHide.Count > 1 ? "s" : "")}");
        if (RawFiber.Count > 0) parts.Add($"{RawFiber.Count} raw fiber");
        if (RawFat.Count > 0) parts.Add($"{RawFat.Count} raw fat");
        if (Tallow.Count > 0) parts.Add($"{Tallow.Count} tallow");

        // Discrete items
        foreach (var tool in Tools) parts.Add(tool.Name);
        foreach (var item in Special) parts.Add(item.Name);

        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }

    /// <summary>
    /// Apply a multiplier to butchering yields (meat, bone, hide, sinew).
    /// </summary>
    public void ApplyYieldMultiplier(double multiplier)
    {
        MultiplyStack(RawMeat, multiplier);
        MultiplyStack(CookedMeat, multiplier);
        MultiplyStack(Bone, multiplier);
        MultiplyStack(Hide, multiplier);
        MultiplyStack(Sinew, multiplier);
        MultiplyStack(RawFat, multiplier);
    }

    /// <summary>
    /// Apply a multiplier to forageable resources (for perception impairment).
    /// </summary>
    public void ApplyForageMultiplier(double multiplier)
    {
        MultiplyStack(Logs, multiplier);
        MultiplyStack(Sticks, multiplier);
        MultiplyStack(Tinder, multiplier);
        MultiplyStack(Berries, multiplier);
        MultiplyStack(Stone, multiplier);
        MultiplyStack(PlantFiber, multiplier);
        WaterLiters *= multiplier;
    }

    private static void MultiplyStack(Stack<double> stack, double multiplier)
    {
        var items = stack.ToArray();
        stack.Clear();
        foreach (var item in items)
            stack.Push(item * multiplier);
    }

    /// <summary>
    /// Take one "unit" of pyrite (0.05kg) for crafting.
    /// </summary>
    private double TakePyrite()
    {
        const double unitKg = 0.05;
        if (Pyrite >= unitKg)
        {
            Pyrite -= unitKg;
            return unitKg;
        }
        return 0;
    }

    /// <summary>
    /// Check if there's enough fuel and tinder to start a fire.
    /// </summary>
    public bool CanStartFire => Tinder.Count > 0 && (Sticks.Count > 0 || Logs.Count > 0);

    /// <summary>
    /// Check if there's any food available.
    /// </summary>
    public bool HasFood => CookedMeat.Count > 0 || RawMeat.Count > 0 || Berries.Count > 0;

    /// <summary>
    /// Check if carrying any meat (raw or cooked).
    /// </summary>
    public bool HasMeat => RawMeat.Count > 0 || CookedMeat.Count > 0;

    /// <summary>
    /// Drops all meat (raw and cooked). Returns total weight dropped.
    /// Used for predator encounters where player sacrifices meat to escape.
    /// </summary>
    public double DropAllMeat()
    {
        double total = RawMeat.Sum() + CookedMeat.Sum();
        RawMeat.Clear();
        CookedMeat.Clear();
        return total;
    }

    /// <summary>
    /// Check if there's any water available.
    /// </summary>
    public bool HasWater => WaterLiters > 0;

    /// <summary>
    /// Check if there's any fuel available.
    /// Tinder burns fast (inefficient) but counts as fuel.
    /// </summary>
    public bool HasFuel => Logs.Count > 0 || Sticks.Count > 0 || Tinder.Count > 0;

    /// <summary>
    /// Check if inventory has a cutting tool (knife or axe) for butchering.
    /// Checks both equipped weapon and unequipped tools.
    /// </summary>
    public bool HasCuttingTool =>
        (Weapon != null && (Weapon.Type == ToolType.Knife || Weapon.Type == ToolType.Axe)) ||
        Tools.Any(t => t.Type == ToolType.Knife || t.Type == ToolType.Axe);

    /// <summary>
    /// Check if there are any crafting materials available.
    /// Includes sticks (can be used for crafting) and dedicated materials.
    /// </summary>
    public bool HasCraftingMaterials =>
        Stone.Count > 0 || Bone.Count > 0 || Hide.Count > 0 ||
        PlantFiber.Count > 0 || Sinew.Count > 0 || Sticks.Count > 1 || Logs.Count > 0;

    /// <summary>
    /// Equip armor/clothing to the appropriate slot.
    /// Returns the previously equipped item if any.
    /// </summary>
    public Equipment? Equip(Equipment equipment)
    {
        Equipment? previous = equipment.Slot switch
        {
            EquipSlot.Head => Head,
            EquipSlot.Chest => Chest,
            EquipSlot.Legs => Legs,
            EquipSlot.Feet => Feet,
            EquipSlot.Hands => Hands,
            _ => null
        };

        switch (equipment.Slot)
        {
            case EquipSlot.Head: Head = equipment; break;
            case EquipSlot.Chest: Chest = equipment; break;
            case EquipSlot.Legs: Legs = equipment; break;
            case EquipSlot.Feet: Feet = equipment; break;
            case EquipSlot.Hands: Hands = equipment; break;
        }

        return previous;
    }

    /// <summary>
    /// Unequip armor/clothing from a slot.
    /// Returns the unequipped item if any.
    /// </summary>
    public Equipment? Unequip(EquipSlot slot)
    {
        Equipment? removed = slot switch
        {
            EquipSlot.Head => Head,
            EquipSlot.Chest => Chest,
            EquipSlot.Legs => Legs,
            EquipSlot.Feet => Feet,
            EquipSlot.Hands => Hands,
            _ => null
        };

        switch (slot)
        {
            case EquipSlot.Head: Head = null; break;
            case EquipSlot.Chest: Chest = null; break;
            case EquipSlot.Legs: Legs = null; break;
            case EquipSlot.Feet: Feet = null; break;
            case EquipSlot.Hands: Hands = null; break;
        }

        return removed;
    }

    /// <summary>
    /// Equip a tool as weapon (must have combat stats).
    /// Returns the previously equipped weapon if any.
    /// </summary>
    public Tool? EquipWeapon(Tool weapon)
    {
        if (!weapon.IsWeapon)
            throw new InvalidOperationException($"Tool '{weapon.Name}' cannot be equipped as weapon (no combat stats)");

        var previous = Weapon;
        Weapon = weapon;
        return previous;
    }

    /// <summary>
    /// Unequip the current weapon.
    /// Returns the unequipped weapon if any.
    /// </summary>
    public Tool? UnequipWeapon()
    {
        var removed = Weapon;
        Weapon = null;
        return removed;
    }

    /// <summary>
    /// Gets or equips a weapon of the specified type.
    /// If no matching weapon is equipped, checks Tools and auto-equips.
    /// Prompts player if multiple matching weapons are available.
    /// </summary>
    public Tool? GetOrEquipWeapon(GameContext ctx, ToolType? type = null)
    {
        // Already have matching weapon equipped?
        if (Weapon != null && (type == null || Weapon.Type == type))
            return Weapon;

        // Find matching weapons in Tools
        var available = Tools.Where(t => t.IsWeapon && (type == null || t.Type == type)).ToList();

        if (available.Count == 0)
            return null;

        Tool toEquip;
        if (available.Count == 1)
        {
            toEquip = available[0];
        }
        else
        {
            // Prompt player to choose
            var choice = new Actions.Choice<Tool>("Which weapon?");
            foreach (var w in available)
                choice.AddOption($"{w.Name} ({w.Damage:F0} dmg)", w);
            toEquip = choice.GetPlayerChoice(ctx);
        }

        Tools.Remove(toEquip);
        var previous = EquipWeapon(toEquip);
        if (previous != null)
            Tools.Add(previous);

        return toEquip;
    }

    /// <summary>
    /// Get a tool by type. Checks equipped weapon first, then tools list.
    /// </summary>
    public Tool? GetTool(ToolType type)
    {
        if (Weapon != null && Weapon.Type == type)
            return Weapon;
        return Tools.FirstOrDefault(t => t.Type == type);
    }

    /// <summary>
    /// Get equipped equipment by slot.
    /// </summary>
    public Equipment? GetEquipment(EquipSlot slot)
    {
        return slot switch
        {
            EquipSlot.Head => Head,
            EquipSlot.Chest => Chest,
            EquipSlot.Legs => Legs,
            EquipSlot.Feet => Feet,
            EquipSlot.Hands => Hands,
            _ => null
        };
    }

    /// <summary>
    /// Create a player inventory with default carry capacity.
    /// </summary>
    public static Inventory CreatePlayerInventory(double maxWeightKg = 15.0) =>
        new() { MaxWeightKg = maxWeightKg };

    /// <summary>
    /// Create a camp storage with unlimited capacity.
    /// </summary>
    public static Inventory CreateCampStorage() =>
        new() { MaxWeightKg = 500.0 };

    /// <summary>
    /// Get a list of all transferable items with descriptions for UI.
    /// Returns tuples of (category, description, weight, transferAction).
    /// Since resources are fungible, transfer actions pop from source and push to target.
    /// </summary>
    public List<(string Category, string Description, double Weight, Action TransferTo)> GetTransferableItems(Inventory target)
    {
        var items = new List<(string, string, double, Action)>();

        // Fuel
        foreach (var w in Logs)
            items.Add(("Fuel", $"Log ({w:F1}kg)", w, () => target.Logs.Push(Logs.Pop())));
        foreach (var w in Sticks)
            items.Add(("Fuel", $"Stick ({w:F2}kg)", w, () => target.Sticks.Push(Sticks.Pop())));
        foreach (var w in Tinder)
            items.Add(("Fuel", $"Tinder ({w:F2}kg)", w, () => target.Tinder.Push(Tinder.Pop())));

        // Food
        foreach (var w in CookedMeat)
            items.Add(("Food", $"Cooked meat ({w:F1}kg)", w, () => target.CookedMeat.Push(CookedMeat.Pop())));
        foreach (var w in RawMeat)
            items.Add(("Food", $"Raw meat ({w:F1}kg)", w, () => target.RawMeat.Push(RawMeat.Pop())));
        foreach (var w in Berries)
            items.Add(("Food", $"Berries ({w:F2}kg)", w, () => target.Berries.Push(Berries.Pop())));

        // Water (transfer in 0.5L increments)
        if (WaterLiters >= 0.5)
            items.Add(("Water", $"Water (0.5L)", 0.5, () => { target.WaterLiters += 0.5; WaterLiters -= 0.5; }));

        // Materials
        foreach (var w in Stone)
            items.Add(("Materials", $"Stone ({w:F1}kg)", w, () => target.Stone.Push(Stone.Pop())));
        foreach (var w in Bone)
            items.Add(("Materials", $"Bone ({w:F1}kg)", w, () => target.Bone.Push(Bone.Pop())));
        foreach (var w in Hide)
            items.Add(("Materials", $"Hide ({w:F1}kg)", w, () => target.Hide.Push(Hide.Pop())));
        foreach (var w in PlantFiber)
            items.Add(("Materials", $"Plant fiber ({w:F2}kg)", w, () => target.PlantFiber.Push(PlantFiber.Pop())));
        foreach (var w in Sinew)
            items.Add(("Materials", $"Sinew ({w:F2}kg)", w, () => target.Sinew.Push(Sinew.Pop())));

        // Tools (not fungible - track specific tool)
        foreach (var tool in Tools.ToList())
        {
            items.Add(("Tools", $"{tool.Name} ({tool.Weight:F1}kg)", tool.Weight, () => {
                target.Tools.Add(tool);
                Tools.Remove(tool);
            }));
        }

        return items;
    }
}
