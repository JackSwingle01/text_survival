using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using static text_survival.Environments.Grid.TerrainTypeExtensions;

namespace text_survival.Desktop.UI;

public class TilePopup
{
    // Currently selected tile (if popup is visible)
    private (int x, int y)? _selectedTile;
    private Location? _selectedLocation;
    private int? _travelTimeMinutes;
    private bool _isAdjacent;
    private bool _isPassable;
    private bool _isPlayerHere;

    // Screen position for popup (near the clicked tile)
    private Vector2 _popupPosition;

    public bool IsOpen => _selectedTile.HasValue;

    public (int x, int y)? SelectedTile => _selectedTile;

    public void Show(GameContext ctx, int x, int y, Vector2 screenPosition)
    {
        var map = ctx.Map;
        if (map == null) return;

        _selectedTile = (x, y);
        _selectedLocation = map.GetLocationAt(x, y);

        var currentPos = map.CurrentPosition;
        var targetPos = new GridPosition(x, y);

        _isPlayerHere = currentPos.X == x && currentPos.Y == y;
        _isAdjacent = currentPos.IsAdjacentTo(targetPos);
        _isPassable = _selectedLocation?.IsPassable ?? false;

        // Calculate travel time if adjacent and passable
        if (_isAdjacent && _isPassable && !_isPlayerHere && _selectedLocation != null)
        {
            _travelTimeMinutes = CalculateTravelTime(ctx, _selectedLocation);
        }
        else
        {
            _travelTimeMinutes = null;
        }

        // Position popup to the right of the tile, vertically centered
        _popupPosition = new Vector2(screenPosition.X + 110, screenPosition.Y);
    }

    public void Hide()
    {
        _selectedTile = null;
        _selectedLocation = null;
    }

    public string? Render(GameContext ctx, float deltaTime)
    {
        if (!IsOpen || _selectedLocation == null) return null;

        string? result = null;

        // Position the popup
        ImGui.SetNextWindowPos(_popupPosition, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(220, 0), ImGuiCond.Always);

        ImGuiWindowFlags flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                                  ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings;

        if (ImGui.Begin("##TilePopup", flags))
        {
            // Location name
            ImGui.TextColored(new Vector4(0.9f, 0.85f, 0.7f, 1f), _selectedLocation.Name);

            // Terrain type (if different from name)
            if (_selectedLocation.Name != _selectedLocation.Terrain.ToString())
            {
                ImGui.TextDisabled(_selectedLocation.Terrain.ToString());
            }

            ImGui.Separator();

            // Render feature details
            RenderFeatures(ctx);

            // Render NPCs if any
            RenderNPCs(ctx);

            ImGui.Separator();

            // Go button (only if adjacent, passable, and not current tile)
            if (_isAdjacent && _isPassable && !_isPlayerHere)
            {
                string buttonLabel = _travelTimeMinutes.HasValue
                    ? $"Go ({_travelTimeMinutes.Value} min)"
                    : "Go";

                if (ImGui.Button(buttonLabel, new Vector2(-1, 30)))
                {
                    result = "go";
                }
            }
            else if (_isPlayerHere)
            {
                ImGui.TextDisabled("You are here");
            }
            else if (!_isAdjacent)
            {
                ImGui.TextDisabled("Too far to travel");
            }
            else if (!_isPassable)
            {
                ImGui.TextDisabled("Impassable terrain");
            }
        }
        ImGui.End();

        return result;
    }

    private void RenderFeatures(GameContext ctx)
    {
        if (_selectedLocation == null) return;

        bool hasFeatures = false;

        // Fire status
        var fire = _selectedLocation.GetFeature<HeatSourceFeature>();
        if (fire != null)
        {
            hasFeatures = true;
            if (fire.IsActive)
            {
                string phase = fire.GetFirePhase();
                int minutes = (int)(fire.BurningHoursRemaining * 60);
                Vector4 color = minutes <= 5
                    ? new Vector4(1f, 0.3f, 0.3f, 1f)
                    : minutes <= 15
                        ? new Vector4(1f, 0.7f, 0.3f, 1f)
                        : new Vector4(1f, 0.6f, 0.2f, 1f);
                ImGui.TextColored(color, $"Fire: {phase} ({FormatTime(minutes)})");
            }
            else if (fire.HasEmbers)
            {
                int minutes = (int)(fire.EmberTimeRemaining * 60);
                ImGui.TextColored(new Vector4(0.8f, 0.4f, 0.2f, 1f), $"Embers ({FormatTime(minutes)})");
            }
        }

        // Shelter
        var shelter = _selectedLocation.GetFeature<ShelterFeature>();
        if (shelter != null)
        {
            hasFeatures = true;
            int insulation = (int)Math.Round(shelter.TemperatureInsulation * 100);
            int wind = (int)Math.Round(shelter.WindCoverage * 100);
            ImGui.Text($"Shelter: {insulation}%% insulation, {wind}%% wind block");
        }

        // Forage
        var forage = _selectedLocation.GetFeature<ForageFeature>();
        if (forage != null)
        {
            hasFeatures = true;
            var resources = forage.GetAvailableResourceTypes();
            if (resources.Count > 0)
            {
                string resourceList = string.Join(", ", resources.Take(3));
                ImGui.Text($"Forage: {resourceList}");
            }
            else
            {
                ImGui.TextDisabled("Forage: depleted");
            }

            // Show exploration progress
            double explorationPct = _selectedLocation.GetExplorationPct();
            if (explorationPct >= 1.0)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.8f, 0.5f, 1f), "  Fully explored");
            }
            else
            {
                int pctDisplay = (int)(explorationPct * 100);
                ImGui.TextColored(new Vector4(0.6f, 0.7f, 0.8f, 1f), $"  {pctDisplay}%% explored");
            }
        }

        // Game (animals)
        var territory = _selectedLocation.GetFeature<AnimalTerritoryFeature>();
        if (territory != null)
        {
            hasFeatures = true;
            ImGui.Text($"Game: {territory.GetDescription()}");
        }

        // Water
        var water = _selectedLocation.GetFeature<WaterFeature>();
        if (water != null)
        {
            hasFeatures = true;
            ImGui.Text("Water source");
        }

        // Traps
        var traps = _selectedLocation.GetFeature<SnareLineFeature>();
        if (traps != null && traps.SnareCount > 0)
        {
            hasFeatures = true;
            if (traps.HasCatchWaiting)
            {
                ImGui.TextColored(new Vector4(0.4f, 0.9f, 0.4f, 1f), $"Traps: {traps.CatchCount} catch ready!");
            }
            else
            {
                ImGui.Text($"Traps: {traps.SnareCount} active");
            }
        }

        // Curing rack
        var rack = _selectedLocation.GetFeature<CuringRackFeature>();
        if (rack != null && rack.ItemCount > 0)
        {
            hasFeatures = true;
            if (rack.HasReadyItems)
            {
                ImGui.TextColored(new Vector4(0.4f, 0.9f, 0.4f, 1f), $"Curing rack: items ready!");
            }
            else
            {
                ImGui.Text($"Curing rack: {rack.ItemCount} curing");
            }
        }

        // Carcass
        var carcass = _selectedLocation.GetFeature<CarcassFeature>();
        if (carcass != null)
        {
            hasFeatures = true;
            string decay = carcass.GetDecayDescription();
            ImGui.Text($"Carcass: {carcass.AnimalName} ({decay})");
        }

        // Cache/Storage
        var cache = _selectedLocation.GetFeature<CacheFeature>();
        if (cache != null)
        {
            hasFeatures = true;
            double weight = cache.Storage.CurrentWeightKg;
            if (weight > 0)
            {
                ImGui.Text($"Cache: {weight:F1}kg stored");
            }
            else
            {
                ImGui.Text("Cache: empty");
            }
        }

        // Bedding
        var bedding = _selectedLocation.GetFeature<BeddingFeature>();
        if (bedding != null)
        {
            hasFeatures = true;
            ImGui.Text($"Bedding: {bedding.Quality} quality");
        }

        if (!hasFeatures)
        {
            ImGui.TextDisabled("No notable features");
        }
    }

    private void RenderNPCs(GameContext ctx)
    {
        if (_selectedLocation == null) return;

        var npcsHere = ctx.NPCs.Where(n => n.CurrentLocation == _selectedLocation).ToList();
        if (npcsHere.Count == 0) return;

        ImGui.Spacing();

        // Show detailed info if player is at this tile, otherwise basic
        if (_isPlayerHere)
        {
            foreach (var npc in npcsHere)
            {
                RenderNPCDetailed(npc);
            }

            // Hint to open full overlay
            ImGui.Spacing();
            ImGui.TextDisabled("Press N to inspect");
        }
        else
        {
            foreach (var npc in npcsHere)
            {
                string action = npc.CurrentAction?.Name ?? "Idle";
                ImGui.Text($"{npc.Name}: {action}");
            }
        }
    }

    private static void RenderNPCDetailed(Actors.NPC npc)
    {
        // Name + action + need
        string action = npc.CurrentAction?.Name ?? "Idle";
        string needText = npc.CurrentNeed.HasValue ? $" ({npc.CurrentNeed})" : "";
        ImGui.TextColored(new Vector4(0.9f, 0.85f, 0.7f, 1f), npc.Name);
        ImGui.SameLine();
        ImGui.TextDisabled($"- {action}{needText}");

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
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), $"  ! {string.Join(", ", warnings)}");
        }
    }

    private static int CalculateTravelTime(GameContext ctx, Location destination)
    {
        // Base travel time depends on terrain and player movement capacity
        var capacities = ctx.player.GetCapacities();
        double movingCapacity = Math.Max(0.1, capacities.Moving);

        // Use terrain's base traversal time
        int baseMinutes = destination.Terrain.BaseTraversalMinutes();

        // Adjust for movement capacity
        return (int)(baseMinutes / movingCapacity);
    }

    private static string FormatTime(int minutes)
    {
        if (minutes >= 60)
        {
            int hours = minutes / 60;
            int mins = minutes % 60;
            return mins > 0 ? $"{hours}h {mins}m" : $"{hours}h";
        }
        return $"{minutes}m";
    }
}
