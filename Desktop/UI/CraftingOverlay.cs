using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Crafting;
using text_survival.Desktop.Input;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Desktop.UI;

/// <summary>Choose a purpose, compare concrete methods, then commit a fully evaluated action.</summary>
public class CraftingOverlay
{
    public bool IsOpen { get; set; }
    public CraftOption? SelectedRecipe { get; private set; }
    public void ClearSelectedRecipe() => SelectedRecipe = null;

    private int _section;
    private string _search = "";
    private bool _readyOnly;
    private bool _repairOnly;
    private string? _familyId;
    private string? _optionId;
    private Gear? _comparison;
    private int _mode;
    private Guid? _targetId;
    private static readonly string[] Modes = ["Make", "Improve", "Maintain"];
    private readonly Stack<(string Family, string Option)> _back = new();
    private static readonly Vector4 Accent = new(0.9f, 0.85f, 0.7f, 1);
    private static readonly Vector4 Warning = new(1, 0.65f, 0.4f, 1);

    public void Render(GameContext ctx, NeedCraftingSystem crafting, float deltaTime)
    {
        if (!IsOpen) return;
        var options = crafting.AllOptions.Where(o => !o.IsMendingRecipe)
            .Concat(GearCrafting.Options(ctx.Inventory, crafting)).ToList();
        var evaluations = new Dictionary<string, CraftEvaluation>();
        CraftEvaluation Evaluate(CraftOption o)
        {
            if (!evaluations.TryGetValue(o.Id, out var e)) evaluations[o.Id] = e = CraftEvaluation.For(ctx, o);
            return e;
        }

        OverlaySizes.SetupWide();
        bool open = IsOpen;
        if (ImGui.Begin("Crafting", ref open, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##search", "Search plans, items, or purposes", ref _search, 150);
            RenderTabs("Sections", CraftFamilies.Sections, ref _section);
            ImGui.Checkbox("Ready only", ref _readyOnly);
            ImGui.SameLine();
            if (ImGui.Checkbox("Repair only", ref _repairOnly)) { _mode = _repairOnly ? 2 : 0; _optionId = null; }
            if (_back.Count > 0 && ImGui.Button("Back to your plan"))
            {
                (_familyId, _optionId) = _back.Pop();
                _section = Array.IndexOf(CraftFamilies.Sections, CraftFamilies.Get(_familyId).Section);
                _search = "";
                _readyOnly = false;
                _repairOnly = false;
            }
            ImGui.Separator();

            bool Matches(CraftOption o) => (!_readyOnly || Evaluate(o).Ready) &&
                (!_repairOnly || o.Method == "Maintain") &&
                (string.IsNullOrWhiteSpace(_search) || $"{o.Name} {CraftFamilies.Get(o.FamilyId).Name} {CraftFamilies.Get(o.FamilyId).Purpose}"
                    .Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase));
            var matching = options.Where(Matches).ToList();
            var families = CraftFamilies.All.Where(f =>
                (!string.IsNullOrWhiteSpace(_search) || f.Section == CraftFamilies.Sections[_section]) &&
                matching.Any(o => o.FamilyId == f.Id)).ToList();

            float height = Math.Max(150, ImGui.GetContentRegionAvail().Y - 36);
            bool narrow = ImGui.GetContentRegionAvail().X < 600;
            if (narrow)
            {
                if (ImGui.BeginCombo("Purpose", _familyId == null ? "Choose a purpose" : CraftFamilies.Get(_familyId).Name))
                {
                    foreach (var family in families)
                        if (ImGui.Selectable(family.Name, family.Id == _familyId)) SelectFamily(family.Id);
                    ImGui.EndCombo();
                }
            }
            else
            {
                ImGui.BeginChild("Families", new Vector2(210, height), ImGuiChildFlags.Borders);
                foreach (var family in families)
                {
                    bool ready = matching.Any(o => o.FamilyId == family.Id && Evaluate(o).Ready);
                    if (ImGui.Selectable($"{(ready ? "+ " : "")}{family.Name}", family.Id == _familyId)) SelectFamily(family.Id);
                }
                if (families.Count == 0) UiText.Wrapped("No matching plans. Try All or another search.");
                ImGui.EndChild();
                ImGui.SameLine();
            }
            ImGui.BeginChild("Plan", new Vector2(0, narrow ? Math.Max(100, ImGui.GetContentRegionAvail().Y - 36) : height), ImGuiChildFlags.Borders);
            if (_familyId == null)
                UiText.Wrapped("Choose what you need. Compare methods and materials before you spend time on work.");
            else
            {
                var family = CraftFamilies.Get(_familyId);
                UiText.Colored(Accent, family.Name);
                UiText.Wrapped(family.Purpose);
                var familyOptions = matching.Where(o => o.FamilyId == family.Id).ToList();
                if (_optionId != null && familyOptions.FirstOrDefault(o => o.Id == _optionId) is { } chosen)
                {
                    _mode = Mode(chosen);
                    _targetId = chosen.TargetGear?.InstanceId;
                }
                if (RenderTabs("Actions", Modes, ref _mode)) _optionId = null;
                var variants = familyOptions.Where(o => Mode(o) == _mode).ToList();
                if (_mode > 0)
                {
                    var targets = variants.Select(o => o.TargetGear).OfType<Gear>().Distinct().ToList();
                    if (!targets.Any(g => g.InstanceId == _targetId)) _targetId = targets.FirstOrDefault()?.InstanceId;
                    var target = targets.FirstOrDefault(g => g.InstanceId == _targetId);
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.BeginCombo("##target", target == null ? "No eligible equipment owned" : $"{target.Name} ({target.ConditionPct:P0})"))
                    {
                        for (int index = 0; index < targets.Count; index++)
                        {
                            var gear = targets[index];
                            if (ImGui.Selectable($"{gear.Name} ({gear.ConditionPct:P0}) - item {index + 1}##{gear.InstanceId}", gear == target))
                            {
                                _targetId = gear.InstanceId;
                                _optionId = null;
                            }
                        }
                        ImGui.EndCombo();
                    }
                    variants = variants.Where(o => o.TargetGear?.InstanceId == _targetId).ToList();
                }
                // Preserve the selected variant across filtering and inventory updates.
                var selected = options.FirstOrDefault(o => o.Id == _optionId);
                if (selected == null || selected.FamilyId != family.Id || Mode(selected) != _mode || (_mode > 0 && selected.TargetGear?.InstanceId != _targetId))
                {
                    selected = variants.FirstOrDefault(o => Evaluate(o).Ready) ?? variants.FirstOrDefault();
                    _optionId = selected?.Id;
                }
                var familiar = variants.Where(o => o.TargetGear != null || o == selected ||
                    ctx.Discoveries.HasDiscoveredAllRequirements(o.Requirements)).ToList();
                RenderVariants(familiar, Evaluate);
                var other = variants.Except(familiar).ToList();
                if (other.Count > 0 && ImGui.TreeNode($"Other plans and materials ({other.Count})"))
                {
                    RenderVariants(other, Evaluate);
                    ImGui.TreePop();
                }
                selected = options.FirstOrDefault(o => o.Id == _optionId);
                if (selected != null) RenderDetails(ctx, crafting, selected, Evaluate(selected));
                else UiText.Wrapped("No matching actions. Clear filters to browse all plans.");
            }
            ImGui.EndChild();
            if (ImGui.Button($"Close {HotkeyRegistry.GetTip(HotkeyAction.Cancel)}", new Vector2(-1, 0))) open = false;
        }
        ImGui.End();
        IsOpen = open && SelectedRecipe == null;
    }

    private static bool RenderTabs(string id, string[] labels, ref int selected)
    {
        int before = selected;
        for (int i = 0; i < labels.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            bool active = i == selected;
            if (active) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.TabSelected]);
            if (ImGui.Button($"{labels[i]}##{id}{i}")) selected = i;
            if (active) ImGui.PopStyleColor();
        }
        return before != selected;
    }

    private static int Mode(CraftOption option) => option.TargetGear == null ? 0 : option.Method == "Maintain" ? 2 : 1;

    private void SelectFamily(string id)
    {
        if (_familyId == id) return;
        _familyId = id;
        _optionId = null;
        _comparison = null;
        _mode = _repairOnly ? 2 : 0;
        _targetId = null;
    }

    private static string VariantName(CraftOption option) => option.TargetGear == null ? option.Name :
        option.Id.StartsWith("refit:") ? option.Name.Split(": ").Last() :
        option.Id.StartsWith("point:") ? option.Name.Split(": ").Last() :
        option.Id.StartsWith("handle:") ? "Add a handle" :
        option.Id.StartsWith("sharpen:") ? "Sharpen edge" : "Mend garment";

    private void RenderVariants(List<CraftOption> variants, Func<CraftOption, CraftEvaluation> evaluate)
    {
        foreach (var group in variants.GroupBy(o => o.Method))
        {
            UiText.Text(group.Key);
            foreach (var option in group)
            {
                var e = evaluate(option);
                string state = e.Ready ? "Ready" : e.Blockers.FirstOrDefault() ?? "Unavailable";
                if (ImGui.Selectable($"{VariantName(option)}##{option.Id}", _optionId == option.Id))
                {
                    _optionId = option.Id;
                    _comparison = null;
                }
                UiText.Wrapped($"  {e.Minutes} min{(e.LaterWorkMinutes > 0 ? " setup + later work" : "")} | {state}");
            }
        }
    }

    private void RenderDetails(GameContext ctx, NeedCraftingSystem crafting, CraftOption option, CraftEvaluation e)
    {
        ImGui.Separator();
        UiText.Colored(Accent, option.Name);
        UiText.Wrapped(option.Description);
        if (e.Output is { } output)
        {
            UiText.Text("Result");
            foreach (string line in CraftEvaluation.Describe(output)) UiText.Wrapped(line);
            var comparisons = CraftEvaluation.Comparisons(ctx.Inventory, output).ToList();
            if (option.TargetGear != null) _comparison = option.TargetGear;
            else if (_comparison == null || !comparisons.Contains(_comparison)) _comparison = comparisons.FirstOrDefault();
            if (_comparison != null)
            {
                ImGui.Spacing();
                if (option.TargetGear == null && comparisons.Count > 1 && ImGui.BeginCombo("Compare with", _comparison.Name))
                {
                    foreach (var gear in comparisons)
                        if (ImGui.Selectable($"{gear.Name} ({gear.ConditionPct:P0})##{gear.InstanceId}", gear == _comparison)) _comparison = gear;
                    ImGui.EndCombo();
                }
                UiText.Text($"Yours: {_comparison.Name}");
                foreach (string line in CraftEvaluation.Describe(_comparison)) UiText.Wrapped(line);
            }
            else UiText.Wrapped("You do not currently own comparable equipment.");
        }
        if (option.ProducesMaterials) UiText.Text($"Produces: {option.GetOutputDescription()}");
        ImGui.Separator();
        UiText.Text(e.LaterWorkMinutes > 0 ? $"Setup: {e.Minutes} min; later work: about {e.LaterWorkMinutes} min" : $"Time now: {e.Minutes} minutes");
        if (e.LaterWorkMinutes > 0) UiText.Wrapped($"About {e.Minutes + e.LaterWorkMinutes} minutes of active work in total. Later work can be split into sessions; conditions can change its duration.");
        foreach (string warning in e.Warnings) UiText.Colored(Warning, warning);
        UiText.Text("Consumed materials");
        if (option.Requirements.Count == 0) UiText.Text("None");
        foreach (var req in option.Requirements)
            UiText.Text($"{req.Count} {CraftInputs.MaterialName(req.Material)}");
        if (e.Inputs.Materials.Count > 0)
            UiText.Wrapped("Will use: " + string.Join(", ", e.Inputs.Materials.Select(m => $"{m.Value} {m.Key.ToDisplayName()}")));
        foreach (var (tool, wear) in e.Inputs.Tools)
            UiText.Wrapped(tool.Durability == -1 ? $"Working tool: {tool.Name} (no wear)" : $"Working tool: {tool.Name}, {tool.Durability} -> {tool.Durability - wear} condition");
        if (option.TargetGear != null) UiText.Wrapped($"Work on your {option.TargetGear.Name}; no second item is created.");
        foreach (string blocker in e.Blockers) UiText.Colored(Warning, $"Need: {blocker}");
        if (!e.Ready)
        {
            var producers = crafting.AllOptions.Where(o => o.Id != option.Id && (
                o.GearFactory != null && option.RequiredTools.Contains(o.GearFactory(o.Durability).ToolType ?? ToolType.Unarmed) &&
                    !CraftInputs.OwnedGear(ctx.Inventory).Any(g => g.ToolType == o.GearFactory(o.Durability).ToolType && g.Works) ||
                o.MaterialOutputs?.Any(m => option.Requirements.Any(r => r.Material is MaterialSpecifier.Specific(var resource) &&
                    resource.ToString() == m.Material && ctx.Inventory.Count(resource) < r.Count)) == true)).ToList();
            if (producers.Count > 0 && ImGui.TreeNode("Plans for missing supplies"))
            {
                foreach (var producer in producers)
                    if (ImGui.SmallButton($"View {producer.Name}##link-{producer.Id}"))
                    {
                        _back.Push((option.FamilyId, option.Id));
                        SelectFamily(producer.FamilyId);
                        _optionId = producer.Id;
                        _section = Array.IndexOf(CraftFamilies.Sections, CraftFamilies.Get(producer.FamilyId).Section);
                        _readyOnly = false; _repairOnly = false; _search = "";
                    }
                ImGui.TreePop();
            }
        }
        ImGui.BeginDisabled(!e.Ready);
        string verb = option.TargetGear != null ? option.Name : option.ProjectWorkMinutes > 0 ? "Start project" : $"Make {option.Name}";
        if (ImGui.Button(verb, new Vector2(-1, 32))) SelectedRecipe = option;
        ImGui.EndDisabled();
    }
}
