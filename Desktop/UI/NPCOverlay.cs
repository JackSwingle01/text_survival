using ImGui = text_survival.Desktop.UI.GameGui;
using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actors;
using text_survival.Desktop.Input;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for viewing NPC companions at the current location.
/// Shows stats, current action, and active effects.
/// </summary>
public class NPCOverlay
{
    private bool _open, _initialFocus;
    public bool IsOpen { get => _open; set { if (value && !_open) _initialFocus = true; _open = value; } }
    public NPC? SelectedNpc { get; private set; }
    private bool _memories, _supplies, _inventory, _personality;
    private readonly HashSet<Resource> _resources = [];
    private string? _feedback;

    /// <summary>
    /// Render the NPC overlay. Returns true if overlay should close.
    /// </summary>
    public bool Render(GameContext ctx, float deltaTime)
    {
        if (!IsOpen) return false;

        var npcsHere = ctx.NPCs.Where(n => n.CurrentLocation == ctx.CurrentLocation).ToList();
        if (npcsHere.Count == 0)
        {
            IsOpen = false;
            return true;
        }

        bool shouldClose = false;

        OverlaySizes.SetupStandard();

        bool open = IsOpen;
        if (ImGui.Begin("Companions", ref open, ImGuiWindowFlags.NoCollapse))
        {
            if (SelectedNpc == null || !npcsHere.Contains(SelectedNpc)) { SelectedNpc = npcsHere[0]; _resources.Clear(); }
            UiText.Wrapped("F2 people · F3 actions · F4 supplies · F5 inventory · Esc close");
            ImGui.BeginChild("companion-feedback", new Vector2(0, ImGui.GetTextLineHeight() * 2 + 4));
            UiText.Wrapped(_feedback ?? " ");
            ImGui.EndChild();
            ImGui.BeginChild("people-list", new Vector2(ImGui.GetContentRegionAvail().X * .32f, -60 * UiStyle.Scale));
            UiNavigation.Initial(ref _initialFocus);
            UiNavigation.Region(Raylib_cs.KeyboardKey.F2);
            foreach (var npc in npcsHere)
                if (UiIcons.Button("person", npc.Name, $"person-{ctx.NPCs.IndexOf(npc)}", new(-1, 0), selected: npc == SelectedNpc))
                { if (SelectedNpc != npc) { SelectedNpc = npc; _resources.Clear(); _feedback = null; } }
            ImGui.EndChild(); ImGui.SameLine();
            ImGui.BeginChild("person-details", new Vector2(0, -60 * UiStyle.Scale));
            ImGui.PushID(ctx.NPCs.IndexOf(SelectedNpc));
            RenderNPC(SelectedNpc, ctx);
            ImGui.PopID(); ImGui.EndChild();
            if (UiIcons.Button("back", "Close [Esc]", "close", new Vector2(-1, 0)) || UiNavigation.Key(ImGuiKey.Escape)) shouldClose = true;
        }
        ImGui.End();

        if (!open) shouldClose = true;
        if (shouldClose) IsOpen = false;

        return shouldClose;
    }

    private void RenderNPC(NPC npc, GameContext ctx)
    {
        // Name header
        UiIcons.LabelColored("person", HudWidgets.Heading, npc.Name);

        UiText.Text($"Opinion of you: {npc.GetRelationship(ctx.player):+0.00;-0.00;0.00}");
        UiNavigation.Region(Raylib_cs.KeyboardKey.F3);
        if (npc.Following != null)
        {
            UiText.Text($"Following {npc.Following.Target.Name}");
            UiText.Disabled(npc.DecisionReason switch
            {
                CompanionDecisionReason.SelfCare => "Taking care of survival needs",
                CompanionDecisionReason.BlockedNeed => "Unable to meet an urgent need; resting and reconsidering",
                CompanionDecisionReason.Pursuit => npc.Following.Status == PursuitStatus.Traveling ? "Catching up" : "Looking for a way to catch up",
                CompanionDecisionReason.RequestWait => "Waiting for an answer",
                CompanionDecisionReason.AgreedWait => "Agreed to wait briefly",
                CompanionDecisionReason.Combat => "In combat",
                _ => "Finishing work nearby"
            });
            if (npc.Following.Target == ctx.player && UiIcons.Button("unfollow", "Go your own way", "dismiss"))
            {
                CompanionInteractions.EndAgreement(npc, ctx.TotalMinutesElapsed, "Dismissed");
            }
        }
        else if (UiIcons.Button("follow", "Come with me", "invite"))
            _feedback = CompanionInteractions.Invite(ctx.player, npc, ctx.TotalMinutesElapsed);
        if (UiIcons.Button("journal", "Memories", "memories")) _memories = !_memories;
        if (_memories)
        {
            foreach (var memory in npc.Relationships.MemoryEvents.Where(m => m.Subject == ctx.player))
            {
                string label = memory.Type switch
                {
                    MemoryType.SavedMe => "You helped save them",
                    MemoryType.AbandonedMe => "You abandoned them",
                    MemoryType.SharedFood => "You shared needed supplies",
                    MemoryType.WarmedMe => "You kept the fire up when they were cold",
                    MemoryType.SmallKindness => "You handed over supplies",
                    MemoryType.FoughtTogether => "You fought together",
                    MemoryType.PressuredMe => "You pressured them to stay",
                    MemoryType.TimeTogether => "Familiarity from time together (limited influence)",
                    _ => memory.Type.ToString()
                };
                UiText.Text(memory.Type == MemoryType.TimeTogether ? label : $"{label}: {memory.Count}");
            }
        }
        UiNavigation.Region(Raylib_cs.KeyboardKey.F4);
        if (UiIcons.Button("transfer", "Give or ask for supplies", "supplies")) _supplies = !_supplies;
        if (_supplies)
        {
            foreach (Resource resource in Enum.GetValues<Resource>())
            {
                double theirs = npc.Inventory.Weight(resource), ours = ctx.Inventory.Weight(resource);
                if (theirs > 0 || ours > 0) _resources.Add(resource);
                if (!_resources.Contains(resource)) continue;
                ImGui.PushID(resource.ToString());
                UiIcons.LabelWrapped(UiIcons.ForResource(resource), UiStyle.Text, $"{resource.ToDisplayName()}: you {ours:F1}, {npc.Name} {theirs:F1} kg");
                if (UiControls.Button(new("give", "Give up to 0.5 kg", "give", DisabledReason: ours > 0 ? null : "You have none to give.")))
                {
                    bool needed = CompanionInteractions.Needs(npc, resource);
                    _feedback = !CompanionInteractions.Give(ctx.player, npc, resource, Math.Min(0.5, ours), ctx.TotalMinutesElapsed)
                        ? "They cannot take that right now."
                        : needed ? "Supplies given. They needed that."
                        : "Supplies given. They did not need them, but they noticed.";
                }
                if (UiControls.Button(new("ask", "Ask for up to 0.5 kg", "ask", DisabledReason: theirs > 0 ? null : "They have none to share.")))
                    _feedback = CompanionInteractions.RequestResource(ctx.player, npc, resource, Math.Min(0.5, theirs), ctx.TotalMinutesElapsed);
                ImGui.PopID();
            }
        }

        // Current action + need
        string action = npc.CurrentAction?.Name ?? "Idle";
        string needText = npc.CurrentNeed.HasValue ? $" ({npc.CurrentNeed})" : "";
        UiIcons.LabelWrapped("person", npc.CurrentNeed.HasValue ? UiStyle.Caution : UiStyle.Text, $"{action}{needText}");

        ImGui.Spacing();

        // Stat bars
        UiIcons.LabelColored("temperature", npc.Body.BodyTemperature < 95 ? UiStyle.Danger : npc.Body.BodyTemperature < 97 ? UiStyle.Caution : UiStyle.Text, "Warmth");
        HudWidgets.VitalBar((float)Math.Clamp((npc.Body.BodyTemperature - 90) / 9, 0, 1), npc.Body.BodyTemperature < 95 ? UiStyle.Danger : npc.Body.BodyTemperature < 97 ? UiStyle.Caution : UiStyle.Success, $"{(npc.Body.BodyTemperature < 97 ? "! " : "")}{npc.Body.BodyTemperature:F1}°F", npc.Body.BodyTemperature < 97);
        RenderStatBar("Energy", (float)npc.Body.EnergyPct, GetStatColor(npc.Body.EnergyPct));
        RenderStatBar("Food", (float)npc.Body.FullPct, GetStatColor(npc.Body.FullPct));
        RenderStatBar("Water", (float)npc.Body.HydratedPct, GetStatColor(npc.Body.HydratedPct));
        RenderStatBar("Clothing Warmth", (float)npc.Body.ClothingHeatBufferPct, new Vector4(1f, 0.8f, 0.3f, 1f));

        // Active effects
        var effects = npc.EffectRegistry.GetAll().ToList();
        if (effects.Count > 0)
        {
            ImGui.Spacing();
            string effectNames = string.Join(", ", effects.Select(e => e.EffectKind).OrderBy(e => e, StringComparer.Ordinal));
            UiIcons.LabelColored("effects", UiStyle.Caution, $"Effects: {effectNames}");
        }

        // Activity log
        var logs = npc.GetRecentLogs(5);
        if (logs.Count > 0)
        {
            ImGui.Spacing();
            UiIcons.Label("journal", "Recent:");
            foreach (var entry in logs)
            {
                UiText.Text($"  {entry}");
            }
        }

        // Inventory section
        ImGui.Spacing();
        RenderNPCInventory(npc.Inventory);
        if (UiIcons.Button("person", "Personality and willingness", "personality")) _personality = !_personality;
        if (_personality)
        {
            UiText.Wrapped($"Sociability {npc.Personality.Sociability:P0} · Selfishness {npc.Personality.Selfishness:P0} · Boldness {npc.Personality.Boldness:P0}");
            UiText.Wrapped("Opinion affects willingness; sociability draws them to others; selfishness affects sharing; boldness affects risk.");
        }
    }

    private void RenderNPCInventory(Inventory inv)
    {
        float weightPct = (float)(inv.CurrentWeightKg / inv.MaxWeightKg);
        string header = $"Inventory ({inv.CurrentWeightKg:F1} / {inv.MaxWeightKg:F1} kg)";

        UiNavigation.Region(Raylib_cs.KeyboardKey.F5);
        if (UiIcons.Button("backpack", header, "npc-inventory")) _inventory = !_inventory;
        if (_inventory)
        {
            // Weight bar
            Vector4 weightColor = weightPct > 0.9f
                ? new Vector4(1f, 0.3f, 0.3f, 1f)
                : weightPct > 0.7f
                    ? new Vector4(1f, 0.8f, 0.3f, 1f)
                    : new Vector4(0.7f, 0.9f, 0.7f, 1f);

            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, UiStyle.BarFill(weightColor));
            ImGui.ProgressBar(weightPct, new Vector2(-1, OverlaySizes.CompactBarHeight), "");
            ImGui.PopStyleColor();

            ImGui.Spacing();

            // Resource summary by category
            RenderResourceCategory(inv, "Food", ResourceCategory.Food);
            RenderResourceCategory(inv, "Fuel", ResourceCategory.Fuel);
            RenderResourceCategory(inv, "Medicine", ResourceCategory.Medicine);
            RenderResourceCategory(inv, "Material", ResourceCategory.Material);

            // Water
            double waterWeight = inv.Weight(Resource.Water);
            if (waterWeight > 0)
            {
                UiText.Colored(new Vector4(0.5f, 0.8f, 1f, 1f), $"Water: {waterWeight:F1}L");
            }

            // Gear section
            bool hasGear = inv.Weapon != null || inv.Tools.Count > 0 ||
                          inv.Equipment.Values.Any(e => e != null) ||
                          inv.Accessories.Count > 0;

            if (hasGear)
            {
                ImGui.Spacing();
                UiText.Disabled("Gear:");

                // Weapon
                if (inv.Weapon != null)
                {
                    RenderGearLine(inv.Weapon, "Weapon");
                }

                // Equipment
                foreach (var (slot, gear) in inv.Equipment)
                {
                    if (gear != null)
                    {
                        RenderGearLine(gear, slot.ToString());
                    }
                }

                // Tools
                foreach (var tool in inv.Tools)
                {
                    RenderGearLine(tool, "Tool");
                }

                // Accessories
                foreach (var acc in inv.Accessories)
                {
                    UiText.Text($"  {acc.Name} (+{acc.CapacityBonusKg:F0}kg cap)");
                }
            }
        }
    }

    private static void RenderResourceCategory(Inventory inv, string label, ResourceCategory category)
    {
        int count = inv.GetCount(category);
        if (count > 0)
        {
            double weight = inv.GetWeight(category);
            UiIcons.Label(UiIcons.ForCategory(label), $"{label}: {count} items ({weight:F1}kg)");
        }
    }

    private static void RenderGearLine(Gear gear, string prefix)
    {
        Vector4 condColor = gear.ConditionPct > 0.5
            ? new Vector4(0.7f, 0.9f, 0.7f, 1f)
            : gear.ConditionPct > 0.25
                ? new Vector4(1f, 0.8f, 0.3f, 1f)
                : new Vector4(1f, 0.4f, 0.4f, 1f);

        UiIcons.Label(UiIcons.ForGear(gear), $"{prefix}: {gear.Name}");
        ImGui.SameLine();
        UiText.Colored(condColor, $"({gear.ConditionPct * 100:F0}%)");
    }

    private void RenderStatBar(string label, float value, Vector4 color)
    {
        string icon = label == "Warmth" ? "temperature" : label == "Clothing Warmth" ? "clothing" : label.ToLowerInvariant();
        UiIcons.LabelColored(icon, color, label);
        HudWidgets.VitalBar(value, color, $"{(value <= .3 ? "! " : "")}{value:P0}", value <= .5);
    }

    private static Vector4 GetStatColor(double value) => value <= .3 ? UiStyle.Danger : value <= .5 ? UiStyle.Caution : UiStyle.Success;
}
