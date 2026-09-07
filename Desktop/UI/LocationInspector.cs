using ImGui = text_survival.Desktop.UI.GameGui;
using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.UI;
using static text_survival.Environments.Grid.TerrainTypeExtensions;

namespace text_survival.Desktop.UI;

/// <summary>Inspects a live location. Selection belongs to HudState, never to this renderer.</summary>
public sealed class LocationInspector
{
    public PlayerAction? Render(GameContext ctx, HudRect rect, HudState state, IReadOnlyList<HudAction> actions, bool interactive)
    {
        PlayerAction? result = null;
        HudWidgets.Begin("##LocationInspector", rect);
        var selected = state.SelectedTile;
        var location = selected is { } tile ? ctx.Map?.GetLocationAt(tile.x, tile.y) : ctx.CurrentLocation;
        if (location == null) { ImGui.End(); return null; }
        bool destination = selected.HasValue;
        UiText.Colored(HudWidgets.Heading, destination ? "SELECTED DESTINATION" : "YOU ARE HERE");
        bool concealed = ctx.Map?.IsCaveConcealed(location) == true;
        UiText.Wrapped(concealed ? "Mountain" : location.Name);
        UiText.Disabled(concealed ? "Mountain" : location.Terrain.ToString());
        ImGui.Separator();
        float footer = ImGui.GetFrameHeight() + ImGui.GetTextLineHeightWithSpacing() * 2 + 20;
        ImGui.BeginChild("location-content", new Vector2(0, Math.Max(30, ImGui.GetContentRegionAvail().Y - footer)));
        ImGui.PushTextWrapPos(0);
        if (destination)
        {
            if (!concealed && location.Visibility == TileVisibility.Visible)
            {
                RenderGround(location);
                RenderFeatures(ctx, location);
                RenderTracks(ctx, selected!.Value);
                RenderNPCs(ctx, location, false);
            }
            else UiText.Disabled("Explored · outside your sight");
            HudWidgets.Section("Travel");
            var travel = TravelInspection.Build(ctx, selected!.Value);
            if (travel.Reason != null) UiText.Wrapped(travel.Reason);
            foreach (var action in travel.Actions)
                if (HudWidgets.Action(action, interactive)) result = action.Payload;
            ImGui.BeginDisabled(!interactive);
            if (ImGui.Button("Current location [Esc]", new Vector2(-1, 0))) state.ClearSelection();
            ImGui.EndDisabled();
        }
        else
        {
            RenderGround(location);
            foreach (var group in new[] { HudActionGroup.Personal, HudActionGroup.Fire, HudActionGroup.Shelter, HudActionGroup.Resources, HudActionGroup.Storage, HudActionGroup.People, HudActionGroup.Other })
            {
                var grouped = actions.Where(a => a.Group == group).ToList();
                bool hasResourceInfo = group == HudActionGroup.Resources &&
                    (location.Features.Any(f => f is ForageFeature or SmallGameFeature or WaterFeature or SnareLineFeature) ||
                     (ctx.Map is { } currentMap && currentMap.Tracks.At(currentMap.CurrentPosition).Count > 0));
                if (grouped.Count == 0 && !hasResourceInfo) continue;
                HudWidgets.Section(group switch {
                    HudActionGroup.Personal => "Personal", HudActionGroup.Shelter => "Shelter & rest", HudActionGroup.Resources => "Resources & work",
                    HudActionGroup.Storage => "Storage & processing", HudActionGroup.Other => "Other work", _ => group.ToString() });
                RenderFeatures(ctx, location, group);
                if (group == HudActionGroup.Resources && ctx.Map is { } map)
                    RenderTracks(ctx, (map.CurrentPosition.X, map.CurrentPosition.Y));
                if (group == HudActionGroup.People) RenderNPCs(ctx, location, true);
                foreach (var action in grouped)
                    if (HudWidgets.Action(action, interactive)) result = action.Payload;
            }
        }
        ImGui.PopTextWrapPos();
        ImGui.EndChild();
        ImGui.Separator();
        UiText.Disabled("AT YOUR LOCATION");
        UiText.Text(ctx.CurrentLocation.Name);
        var wait = actions.First(a => a.Group == HudActionGroup.Wait);
        if (HudWidgets.Action(wait, interactive)) result = wait.Payload;
        ImGui.End();
        return result;
    }

    /// <summary>Burning fuel over unburned, against pit capacity.</summary>
    private static void RenderFuelBar(HeatSourceFeature fire, Vector4 phaseColor)
    {
        double max = fire.MaxFuelCapacityKg;
        if (max <= 0) return;
        float burning = (float)(fire.BurningMassKg / max);
        float total = (float)Math.Clamp((fire.BurningMassKg + fire.UnburnedMassKg) / max, 0, 1);
        string label = fire.UnburnedMassKg > 0.1
            ? $"{fire.BurningMassKg:F1} (+{fire.UnburnedMassKg:F1}) / {max:F0} kg"
            : $"{fire.BurningMassKg:F1} / {max:F0} kg";

        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(0.6f, 0.4f, 0.2f, 1f));
        ImGui.ProgressBar(total, new Vector2(-1, OverlaySizes.CompactBarHeight), "");
        ImGui.PopStyleColor();
        // Burning portion drawn over the unburned bar in the phase color.
        var min = ImGui.GetItemRectMin();
        var size = ImGui.GetItemRectSize();
        ImGui.GetWindowDrawList().AddRectFilled(min, min + new Vector2(size.X * Math.Clamp(burning, 0, 1), size.Y),
            ImGui.GetColorU32(phaseColor));
        ImGui.GetWindowDrawList().AddText(min + new Vector2((size.X - ImGui.CalcTextSize(label).X) / 2, (size.Y - ImGui.GetTextLineHeight()) / 2),
            ImGui.GetColorU32(ImGuiCol.Text), label);
    }

    /// <summary>What the ground is like here today, if it is worth saying.</summary>
    private static void RenderGround(Location location)
    {
        if (location == null || !location.IsPassable) return;

        var condition = location.Surface.ConditionText();
        if (condition == null) return;

        UiText.Colored(new Vector4(0.72f, 0.82f, 0.92f, 1f), condition);
    }

    private static void RenderFeatures(GameContext ctx, Location location, HudActionGroup? group = null)
    {
        if (location == null) return;

        bool hasFeatures = false;

        // Fire status
        var fire = location.GetFeature<HeatSourceFeature>();
        if (fire != null && (group == null || group == HudActionGroup.Fire))
        {
            hasFeatures = true;
            if (fire.IsActive)
            {
                string phase = fire.GetFirePhase();
                // Unburned fuel is time you already paid for - showing only the burning mass
                // told the player their fire was minutes from out with an hour of wood on it.
                int minutes = (int)((fire.UnburnedMassKg > 0.1 ? fire.TotalHoursRemaining : fire.BurningHoursRemaining) * 60);
                Vector4 color = minutes <= 5
                    ? new Vector4(1f, 0.3f, 0.3f, 1f)
                    : minutes <= 15
                        ? new Vector4(1f, 0.7f, 0.3f, 1f)
                        : new Vector4(1f, 0.6f, 0.2f, 1f);
                UiText.Colored(color, $"Fire: {phase} ({HudWidgets.Duration(minutes)})");
                RenderFuelBar(fire, color);
            }
            else if (fire.HasEmbers)
            {
                int minutes = (int)(fire.EmberTimeRemaining * 60);
                UiText.Colored(new Vector4(0.8f, 0.4f, 0.2f, 1f), $"Embers ({HudWidgets.Duration(minutes)})");
            }
        }

        // Shelter
        var shelter = location.GetFeature<ShelterFeature>();
        if (shelter != null && (group == null || group == HudActionGroup.Shelter))
        {
            hasFeatures = true;
            int insulation = (int)Math.Round(shelter.TemperatureInsulation * 100);
            int wind = (int)Math.Round(shelter.WindCoverage * 100);
            UiText.Wrapped($"Shelter: {insulation}% insulation, {wind}% wind block");
        }

        // Forage
        var forage = location.GetFeature<ForageFeature>();
        if (forage != null && (group == null || group == HudActionGroup.Resources))
        {
            hasFeatures = true;
            var resources = forage.GetAvailableResourceTypes();
            if (resources.Count > 0)
            {
                string resourceList = string.Join(", ", resources.Take(3));
                UiText.Text($"Forage: {resourceList}");
            }
            else
            {
                UiText.Disabled("Forage: depleted");
            }

            // Show exploration progress
            double explorationPct = location.GetExplorationPct();
            if (explorationPct >= 1.0)
            {
                UiText.Colored(new Vector4(0.5f, 0.8f, 0.5f, 1f), "  Fully explored");
            }
            else
            {
                int pctDisplay = (int)(explorationPct * 100);
                UiText.Colored(new Vector4(0.6f, 0.7f, 0.8f, 1f), $"  {pctDisplay}% explored");
            }
        }

        // Game (animals)
        var territory = location.GetFeature<SmallGameFeature>();
        if (territory != null && (group == null || group == HudActionGroup.Resources))
        {
            hasFeatures = true;
            UiText.Text($"Game: {territory.GetDescription()}");
        }

        // Water
        var water = location.GetFeature<WaterFeature>();
        if (water != null && (group == null || group == HudActionGroup.Resources))
        {
            hasFeatures = true;
            UiText.Text("Water source");
        }

        // Traps
        var traps = location.GetFeature<SnareLineFeature>();
        if (traps != null && traps.SnareCount > 0 && (group == null || group == HudActionGroup.Resources))
        {
            hasFeatures = true;
            if (traps.HasCatchWaiting)
            {
                UiText.Colored(new Vector4(0.4f, 0.9f, 0.4f, 1f), $"Traps: {traps.CatchCount} catch ready!");
            }
            else
            {
                UiText.Text($"Traps: {traps.SnareCount} active");
            }
        }

        // Curing rack
        var rack = location.GetFeature<CuringRackFeature>();
        if (rack != null && rack.ItemCount > 0 && (group == null || group == HudActionGroup.Storage))
        {
            hasFeatures = true;
            if (rack.HasReadyItems)
            {
                UiText.Colored(new Vector4(0.4f, 0.9f, 0.4f, 1f), $"Curing rack: items ready!");
            }
            else
            {
                UiText.Text($"Curing rack: {rack.ItemCount} curing");
            }
        }

        // Carcass
        var carcass = location.GetFeature<CarcassFeature>();
        if (carcass != null && (group == null || group == HudActionGroup.Storage))
        {
            hasFeatures = true;
            string decay = carcass.GetDecayDescription();
            UiText.Text($"Carcass: {carcass.AnimalName} ({decay})");
        }

        // Cache/Storage
        var cache = location.GetFeature<CacheFeature>();
        if (cache != null && (group == null || group == HudActionGroup.Storage))
        {
            hasFeatures = true;
            double weight = cache.Storage.CurrentWeightKg;
            if (weight > 0)
            {
                UiText.Text($"Cache: {weight:F1}kg stored");
            }
            else
            {
                UiText.Text("Cache: empty");
            }
        }

        // Bedding
        var bedding = location.GetFeature<BeddingFeature>();
        if (bedding != null && (group == null || group == HudActionGroup.Shelter))
        {
            hasFeatures = true;
            UiText.Text($"Bedding: {bedding.Quality} quality");
        }

        if (!hasFeatures && group == null)
        {
            UiText.Disabled("No notable features");
        }
    }

    /// <summary>
    /// What has come through here, as the ground reports it. Count and age both matter:
    /// one set of prints an hour old and a dozen sets from two days back are different
    /// situations, and the player should be able to tell them apart before deciding
    /// whether to follow.
    /// </summary>
    private static void RenderTracks(GameContext ctx, (int x, int y) tile)
    {
        if (ctx.Map == null) return;

        var position = new GridPosition(tile.x, tile.y);
        var tracks = ctx.Map.Tracks.At(position);
        if (tracks.Count == 0) return;

        ImGui.Spacing();

        foreach (var (track, freshness) in tracks)
        {
            int count = ctx.Map.Tracks.TrafficOf(position, track.Maker);
            if (count <= 0) continue;

            string what = track.Maker switch
            {
                TrackMaker.Human => "Footprints",
                TrackMaker.Paw => "Paw prints",
                TrackMaker.Hoof => "Hoof prints",
                _ => "Tracks"
            };

            // Fresher sign reads brighter, the same way it draws on the map.
            var color = freshness switch
            {
                > 0.75 => new Vector4(0.85f, 0.85f, 0.80f, 1f),
                > 0.45 => new Vector4(0.70f, 0.70f, 0.66f, 1f),
                _ => new Vector4(0.55f, 0.55f, 0.52f, 1f)
            };

            string age = freshness switch
            {
                > 0.75 => "fresh",
                > 0.45 => "recent",
                > 0.15 => "old",
                _ => "nearly gone"
            };

            string tally = count > 1 ? $" x{count}" : "";
            string direction = DirectionSummary(track, freshness);
            UiText.Colored(color, $"{what}{tally} - {age}, {direction}");

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                UiText.Text("Direction is based on net movement");
                UiText.Text($"North/South traffic: {Readable(track.NorthSouthTraffic, freshness)}");
                UiText.Text($"East/West traffic: {Readable(track.EastWestTraffic, freshness)}");
                ImGui.EndTooltip();
            }
        }
    }

    private static string DirectionSummary(Track track, double freshness)
    {
        double northSouth = track.NorthSouthTraffic * freshness;
        double eastWest = track.EastWestTraffic * freshness;
        if (northSouth <= 0 && eastWest <= 0)
            return $"heading {track.DominantHeading.ToString().ToLower()}";

        bool northSouthDominant = northSouth >= eastWest * 1.5;
        bool eastWestDominant = eastWest >= northSouth * 1.5;
        if (!northSouthDominant && !eastWestDominant)
            return "mixed";

        double axisTraffic = northSouthDominant ? northSouth : eastWest;
        double net = northSouthDominant ? Math.Abs(track.DirectionY * freshness) : Math.Abs(track.DirectionX * freshness);
        double opposing = Math.Max(0, axisTraffic - net) / 2;
        double leading = (axisTraffic + net) / 2;

        if (opposing <= 0 || leading >= opposing * 1.5)
            return $"mostly {track.DominantHeading.ToString().ToLower()}";

        return northSouthDominant ? "N/S traffic" : "E/W traffic";
    }

    private static int Readable(double traffic, double freshness)
    {
        double readable = traffic * freshness;
        return readable <= 0 ? 0 : Math.Max(1, (int)Math.Round(readable));
    }

    private static void RenderNPCs(GameContext ctx, Location location, bool isPlayerHere)
    {
        if (location == null) return;

        var npcsHere = ctx.NPCs.Where(n => n.CurrentLocation == location).ToList();
        if (npcsHere.Count == 0) return;

        ImGui.Spacing();

        // Show detailed info if player is at this tile, otherwise basic
        if (isPlayerHere)
        {
            foreach (var npc in npcsHere)
            {
                RenderNPCDetailed(npc);
            }

            // Hint to open full overlay
            ImGui.Spacing();
            UiText.Disabled("Press N to inspect");
        }
        else
        {
            foreach (var npc in npcsHere)
            {
                string action = npc.CurrentAction?.Name ?? "Idle";
                UiText.Text($"{npc.Name}: {action}");
            }
        }
    }

    private static void RenderNPCDetailed(Actors.NPC npc)
    {
        // Name + action + need
        string action = npc.CurrentAction?.Name ?? "Idle";
        string needText = npc.CurrentNeed.HasValue ? $" ({npc.CurrentNeed})" : "";
        UiText.Colored(new Vector4(0.9f, 0.85f, 0.7f, 1f), npc.Name);
        ImGui.SameLine();
        UiText.Disabled($"- {action}{needText}");

        // Warning icons for critical states
        var warnings = new List<string>();

        if (npc.Body.WarmPct < 0.3)
            warnings.Add("freezing");
        if (npc.Body.FullPct < 0.1)
            warnings.Add("starving");
        if (npc.Body.HydratedPct < 0.2)
            warnings.Add("dehydrated");
        if (npc.Body.EnergyPct < 0.15)
            warnings.Add("exhausted");

        // Check for injuries
        var effects = npc.EffectRegistry.GetAll().ToList();
        if (effects.Any(e => e.EffectKind == "Bleeding"))
            warnings.Add("bleeding");

        if (warnings.Count > 0)
        {
            UiText.Colored(new Vector4(1f, 0.4f, 0.4f, 1f), $"  ! {string.Join(", ", warnings)}");
        }
    }

}
