using text_survival.Environments;
using text_survival.Items;

namespace text_survival.Crafting;

public class RecipeBuilder
{
    private string _name = "";
    private string _description = "";
    private List<CraftingPropertyRequirement> _requiredProperties = [];
    private CraftingResultType _resultType = CraftingResultType.Item;
    private List<ItemResult> _resultItems = [];
    private LocationFeatureResult? _locationFeatureResult;
    private NewLocationResult? _newLocationResult;
    private int _requiredSkillLevel = 0;
    private string _requiredSkill = "Crafting";
    private int _craftingTimeMinutes = 10;
    private bool _requiresFire = false;

    public RecipeBuilder Named(string name)
    {
        _name = name;
        return this;
    }

    public RecipeBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public RecipeBuilder WithPropertyRequirement(CraftingPropertyRequirement requirement)
    {
        _requiredProperties.Add(requirement);
        return this;
    }
    public RecipeBuilder WithPropertyRequirement(ItemProperty property, double minQuantity = 1, bool isConsumed=true)
    {
        _requiredProperties.Add(new CraftingPropertyRequirement(property, minQuantity, isConsumed));
        return this;
    }

    public RecipeBuilder ResultingInItem(ItemResult itemResult)
    {
        _resultItems.Add(itemResult);
        _resultType = CraftingResultType.Item;
        return this;
    }

    public RecipeBuilder ResultingInItem(Func<Item> itemFactory)
    {
        _resultItems.Add(new ItemResult(itemFactory));
        _resultType = CraftingResultType.Item;
        return this;
    }

    public RecipeBuilder ResultingInLocationFeature(LocationFeatureResult locationResult)
    {
        _locationFeatureResult = locationResult;
        _resultType = CraftingResultType.LocationFeature;
        return this;
    }

    public RecipeBuilder ResultingInStructure(NewLocationResult newStructure)
    {
        _newLocationResult = newStructure;
        _resultType = CraftingResultType.Shelter;
        return this;
    }

    public RecipeBuilder ResultingInStructure(string structureName, Func<Zone, Location> locationFactory)
    {
        _newLocationResult = new NewLocationResult(structureName, locationFactory);
        _resultType = CraftingResultType.Shelter;
        return this;
    }

    public RecipeBuilder UtilizingSkill(string skillName = "Crafting")
    {
        _requiredSkill = skillName;
        return this;
    }
    public RecipeBuilder RequiringSkill(string skillName = "Crafting", int skillLevel = 0)
    {
        _requiredSkill = skillName;
        _requiredSkillLevel = skillLevel;
        return this;
    }

    public RecipeBuilder RequiringCraftingTime(int minutes)
    {
        _craftingTimeMinutes = minutes;
        return this;
    }

    public RecipeBuilder RequiringFire(bool requiresFire = true)
    {
        _requiresFire = requiresFire;
        return this;
    }

    public CraftingRecipe Build()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            throw new InvalidOperationException("Name is required");
        }

        var recipe = new CraftingRecipe(_name, _description)
        {
            RequiredProperties = _requiredProperties,
            ResultType = _resultType,
            ResultItems = _resultItems,
            LocationFeatureResult = _locationFeatureResult,
            NewLocationResult = _newLocationResult,
            RequiredSkillLevel = _requiredSkillLevel,
            RequiredSkill = _requiredSkill,
            CraftingTimeMinutes = _craftingTimeMinutes,
            RequiresFire = _requiresFire
        };
        return recipe;
    }
}
