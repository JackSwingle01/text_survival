using ImGui = text_survival.Desktop.UI.GameGui;
using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Crafting;
using text_survival.Desktop.Input;
using text_survival.Items;

namespace text_survival.Desktop.UI;

/// <summary>Browse recipes, inspect their costs, and commit a fully evaluated action.</summary>
public class CraftingOverlay
{
    public bool IsOpen { get; set; }
    public CraftOption? SelectedRecipe { get; private set; }
    public void ClearSelectedRecipe() => SelectedRecipe = null;

    private int _section;
    private string _search = "";
    private bool _readyOnly;
    private string? _optionId;
    private Gear? _comparison;
    private readonly Stack<string> _back = new();
    private static readonly Vector4 Accent = new(0.9f, 0.85f, 0.7f, 1);
    private static readonly Vector4 Ready = new(0.4f, 0.9f, 0.5f, 1);
    private static readonly Vector4 Missing = new(1f, 0.4f, 0.4f, 1);
    private static readonly Vector4 Warning = new(1, 0.65f, 0.4f, 1);

    public void Render(GameContext ctx, NeedCraftingSystem crafting, float deltaTime)
    {
        if (!IsOpen) return;
        var options = crafting.AllOptions.Where(o => !o.IsMendingRecipe)
            .Concat(GearCrafting.Options(ctx.Inventory, crafting).Where(o => o.TargetBlocker?.Invoke() == null)).ToList();
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
            ImGui.InputTextWithHint("##search", "Search recipes or items", ref _search, 150);
            float rowWidth = ImGui.GetContentRegionAvail().X;
            float usedWidth = 0;
            for (int i = 0; i < CraftFamilies.Sections.Length; i++)
            {
                string section = CraftFamilies.Sections[i];
                float width = ImGui.CalcTextSize(section).X + 22 + ImGui.FramePadding.X * 2;
                if (usedWidth > 0 && usedWidth + 8 + width <= rowWidth)
                {
                    ImGui.SameLine(0, 8);
                    usedWidth += 8;
                }
                else usedWidth = 0;
                bool active = i == _section;
                if (active) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.StyleColor(ImGuiCol.TabSelected));
                if (UiIcons.Button(SectionIcon(section), section, $"section-{i}", new Vector2(width, 0), selected: active))
                { _section = i; _optionId = null; }
                if (active) ImGui.PopStyleColor();
                usedWidth += width;
            }
            ImGui.Checkbox("Ready only", ref _readyOnly);
            if (_back.Count > 0 && ImGui.SmallButton("Back to your recipe"))
            {
                var previousId = _back.Pop();
                var previous = options.FirstOrDefault(o => o.Id == previousId);
                if (previous != null) Navigate(previous);
            }
            ImGui.Separator();

            var matching = options.Where(o =>
                (!_readyOnly || Evaluate(o).Ready) &&
                (string.IsNullOrWhiteSpace(_search)
                    ? CraftFamilies.Get(o.FamilyId).Section == CraftFamilies.Sections[_section]
                    : $"{o.Name} {CraftFamilies.Get(o.FamilyId).Name} {CraftFamilies.Get(o.FamilyId).Purpose}"
                        .Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();
            var selected = matching.FirstOrDefault(o => o.Id == _optionId);
            if (selected == null)
            {
                selected = matching.FirstOrDefault(o => Evaluate(o).Ready) ?? matching.FirstOrDefault();
                Select(selected?.Id);
            }

            float height = Math.Max(100, ImGui.GetContentRegionAvail().Y - 36);
            bool narrow = ImGui.GetContentRegionAvail().X < 650;
            if (narrow)
            {
                ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo("##recipe", selected?.Name ?? "No matching recipes"))
                {
                    RenderRecipes(matching, Evaluate);
                    ImGui.EndCombo();
                }
            }
            else
            {
                ImGui.BeginChild("Recipes", new Vector2(ImGui.GetContentRegionAvail().X * 0.43f, height), ImGuiChildFlags.Borders);
                RenderRecipes(matching, Evaluate);
                ImGui.EndChild();
                ImGui.SameLine();
            }
            selected = matching.FirstOrDefault(o => o.Id == _optionId);
            ImGui.BeginChild("Recipe", new Vector2(0, narrow ? Math.Max(100, ImGui.GetContentRegionAvail().Y - 36) : height), ImGuiChildFlags.Borders);
            if (selected != null)
            {
                var e = Evaluate(selected);
                // Keep the action outside the scrolling details.
                ImGui.BeginChild("Details", new Vector2(0, Math.Max(40, ImGui.GetContentRegionAvail().Y - 65)));
                RenderDetails(ctx, crafting, selected, e);
                ImGui.EndChild();
                UiText.Colored(e.Ready ? Ready : Missing, e.Ready ? "Ready to craft" : "Missing requirements");
                ImGui.BeginDisabled(!e.Ready);
                string verb = selected.TargetGear != null ? selected.Name : selected.ProjectWorkMinutes > 0 ? "Start project" : $"Make {selected.Name}";
                if (ImGui.Button(verb, new Vector2(-1, 32))) SelectedRecipe = selected;
                ImGui.EndDisabled();
            }
            else UiText.Wrapped("No matching recipes. Clear your search or turn off Ready only.");
            ImGui.EndChild();
            if (ImGui.Button($"Close {HotkeyRegistry.GetTip(HotkeyAction.Cancel)}", new Vector2(-1, 0))) open = false;
        }
        ImGui.End();
        IsOpen = open && SelectedRecipe == null;
    }

    private void Select(string? id)
    {
        if (_optionId == id) return;
        _optionId = id;
        _comparison = null;
    }

    private void Navigate(CraftOption option)
    {
        Select(option.Id);
        _section = Array.IndexOf(CraftFamilies.Sections, CraftFamilies.Get(option.FamilyId).Section);
        _search = "";
        _readyOnly = false;
    }

    private static string SectionIcon(string section) => section switch
    {
        "Tools" => "gear", "Fire & light" => "fire", "Food gathering" => "food",
        "Clothing & carrying" => "clothing", "Camp" => "shelter", _ => "materials"
    };

    private static string RecipeIcon(CraftOption option, CraftEvaluation e) => e.Output is { } gear ? UiIcons.ForGear(gear)
        : option.MaterialOutputs?.FirstOrDefault() is { } output && Enum.TryParse<Resource>(output.Material, out var resource)
            ? UiIcons.ForResource(resource) : SectionIcon(CraftFamilies.Get(option.FamilyId).Section);

    private void RenderRecipes(List<CraftOption> options, Func<CraftOption, CraftEvaluation> evaluate)
    {
        foreach (var family in options.GroupBy(o => o.FamilyId))
        {
            UiText.Disabled(CraftFamilies.Get(family.Key).Name);
            foreach (var option in family.OrderBy(o => o.TargetGear != null))
            {
                var e = evaluate(option);
                if (e.Ready) ImGui.PushStyleColor(ImGuiCol.Text, Ready);
                string label = $"{option.Name}  ·  {e.Minutes}m{(e.LaterWorkMinutes > 0 ? "+" : "")}  ·  {(e.Ready ? "Ready" : "Missing")}";
                if (RecipeRow(option, e, label)) Select(option.Id);
                if (e.Ready) ImGui.PopStyleColor();
                if (ImGui.IsItemHovered()) UiText.Tooltip($"{option.Name}\n{e.Minutes} min{(e.LaterWorkMinutes > 0 ? $" setup + about {e.LaterWorkMinutes} min work" : "")}\n{(e.Ready ? "Ready" : string.Join("\n", e.Blockers))}");
            }
            ImGui.Spacing();
        }
        if (options.Count == 0) UiText.Wrapped("No matching recipes.");
    }

    private bool RecipeRow(CraftOption option, CraftEvaluation e, string label)
    {
        if (ImGui.Capture != null)
            return UiIcons.Selectable(RecipeIcon(option, e), label, option.Id, _optionId == option.Id);
        var position = ImGui.GetCursorScreenPos();
        var size = new Vector2(ImGui.GetContentRegionAvail().X, Math.Max(24, ImGui.GetTextLineHeight() + 8));
        bool clicked = ImGui.Selectable($"##{option.Id}", _optionId == option.Id, ImGuiSelectableFlags.None, size);
        var draw = ImGui.GetWindowDrawList();
        float y = (size.Y - ImGui.GetTextLineHeight()) / 2;
        string status = $"{e.Minutes}m{(e.LaterWorkMinutes > 0 ? "+" : "")}  {(e.Ready ? "Ready" : "Missing")}";
        float statusWidth = ImGui.CalcTextSize(status).X;
        float nameEnd = Math.Max(24, size.X - statusWidth - 12);
        UiIcons.Draw(RecipeIcon(option, e), position + new Vector2(2, (size.Y - 16) / 2));
        draw.PushClipRect(position + new Vector2(24, 0), position + new Vector2(nameEnd, size.Y), true);
        draw.AddText(position + new Vector2(24, y), ImGui.GetColorU32(ImGuiCol.Text), option.Name);
        draw.PopClipRect();
        draw.AddText(position + new Vector2(size.X - statusWidth, y),
            ImGui.ColorConvertFloat4ToU32(e.Ready ? Ready : Missing), status);
        return clicked;
    }

    private void RenderDetails(GameContext ctx, NeedCraftingSystem crafting, CraftOption option, CraftEvaluation e)
    {
        UiIcons.LabelColored(RecipeIcon(option, e), Accent, option.Name);
        if (option.TargetGear is { } target)
            UiText.Wrapped($"{(option.Method == "Maintain" ? "Repair" : "Improve")}: {target.Name} ({target.ConditionPct:P0} condition)");
        if (e.Output is { } output)
            UiText.Wrapped(CraftEvaluation.Describe(output).FirstOrDefault() ?? option.Description);
        else UiText.Wrapped(option.ProducesMaterials ? $"Produces {option.GetOutputDescription()}" : option.Description);
        UiText.Text(e.LaterWorkMinutes > 0 ? $"{e.Minutes} min setup + about {e.LaterWorkMinutes} min later work" : $"{e.Minutes} minutes");
        foreach (string warning in e.Warnings) UiText.Colored(Warning, warning);
        ImGui.Separator();
        UiText.Text("Materials");
        if (e.Inputs.Requirements.Count == 0) UiText.Disabled("None needed");
        foreach (var (req, available) in e.Inputs.Requirements)
        {
            string icon = req.Material is MaterialSpecifier.Specific(var resource) ? UiIcons.ForResource(resource) : "materials";
            UiIcons.LabelColored(icon, available >= req.Count ? Ready : Missing, $"{CraftInputs.MaterialName(req.Material)} · {req.Count} needed");
            UiText.Colored(available > 0 || req.Count == 0 ? Ready : Missing, $"Have {available}/{req.Count}");
            if (available < req.Count)
            {
                ImGui.SameLine(0, 12);
                UiText.Colored(Missing, $"Missing {req.Count - available}");
            }
        }
        if (option.RequiredTools.Count > 0)
        {
            ImGui.Spacing();
            UiText.Text("Tools · kept after crafting");
            foreach (var type in option.RequiredTools.Distinct())
            {
                var resolved = e.Inputs.Tools.FirstOrDefault(t => t.Tool.ToolType == type);
                if (resolved.Tool is { } tool)
                    UiIcons.LabelColored(UiIcons.ForGear(tool), Ready, $"Have {tool.Name}");
                else UiIcons.LabelColored("gear", Missing, $"Need usable {System.Text.RegularExpressions.Regex.Replace(type.ToString(), "([a-z])([A-Z])", "$1 $2")}");
            }
        }
        foreach (string blocker in e.Blockers.Except(e.Inputs.Missing))
            UiText.Colored(Missing, blocker);
        // Tool condition failures carry information beyond the generic missing tool row.
        foreach (string blocker in e.Inputs.Missing.Where(b => b.Contains("condition remaining")))
            UiText.Colored(Missing, blocker);

        if (!e.Ready)
        {
            var producers = crafting.AllOptions.Where(o => o.Id != option.Id && (
                o.GearFactory != null && option.RequiredTools.Contains(o.GearFactory(o.Durability).ToolType ?? ToolType.Unarmed) &&
                    !e.Inputs.Tools.Any(t => t.Tool.ToolType == o.GearFactory(o.Durability).ToolType) ||
                o.MaterialOutputs?.Any(m => e.Inputs.Requirements.Any(r => r.Available < r.Requirement.Count &&
                    Enum.TryParse<Resource>(m.Material, out var resource) && (r.Requirement.Material switch
                    {
                        MaterialSpecifier.Specific(var specific) => specific == resource,
                        MaterialSpecifier.Category(var category) => ResourceCategories.Items[category].Contains(resource),
                        _ => false
                    }))) == true)).ToList();
            if (producers.Count > 0 && ImGui.TreeNode("Craft missing supplies"))
            {
                foreach (var producer in producers)
                    if (ImGui.SmallButton($"View {producer.Name}##link-{producer.Id}"))
                    {
                        _back.Push(option.Id);
                        Navigate(producer);
                    }
                ImGui.TreePop();
            }
        }
        ImGui.Spacing();
        if (ImGui.TreeNode($"More details##{option.Id}"))
        {
            UiText.Wrapped(option.Description);
            if (e.Output is { } result)
                foreach (string line in CraftEvaluation.Describe(result)) UiText.Wrapped(line);
            if (e.Inputs.Materials.Count > 0)
                UiText.Wrapped("Will use: " + string.Join(", ", e.Inputs.Materials.Select(m => $"{m.Value} {m.Key.ToDisplayName()}")));
            foreach (var (tool, wear) in e.Inputs.Tools)
                UiText.Wrapped(tool.Durability == -1 ? $"{tool.Name}: no wear" : $"{tool.Name}: {tool.Durability} -> {tool.Durability - wear} condition");
            if (e.LaterWorkMinutes > 0) UiText.Wrapped("Later work can be split into sessions; conditions can change its duration.");
            ImGui.TreePop();
        }
        if (e.Output is { } comparable)
        {
            var comparisons = CraftEvaluation.Comparisons(ctx.Inventory, comparable).ToList();
            if (option.TargetGear != null) _comparison = option.TargetGear;
            else if (_comparison == null || !comparisons.Contains(_comparison)) _comparison = comparisons.FirstOrDefault();
            if (_comparison != null && ImGui.TreeNode($"Compare equipment##{option.Id}"))
            {
                if (option.TargetGear == null && comparisons.Count > 1 && ImGui.BeginCombo("Compare with", _comparison.Name))
                {
                    foreach (var gear in comparisons)
                        if (ImGui.Selectable($"{gear.Name} ({gear.ConditionPct:P0})##{gear.InstanceId}", gear == _comparison)) _comparison = gear;
                    ImGui.EndCombo();
                }
                UiText.Text($"Result: {comparable.Name}");
                foreach (string line in CraftEvaluation.Describe(comparable)) UiText.Wrapped(line);
                UiText.Text($"Yours: {_comparison.Name}");
                foreach (string line in CraftEvaluation.Describe(_comparison)) UiText.Wrapped(line);
                ImGui.TreePop();
            }
        }
    }
}
