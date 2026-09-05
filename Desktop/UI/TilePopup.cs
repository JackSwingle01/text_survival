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
    private CrossingPreview? _preview;
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

        // Preview the crossing if adjacent and passable - the same numbers TravelRunner
        // will actually use, so this can never promise a time or risk it doesn't deliver.
        if (_isAdjacent && _isPassable && !_isPlayerHere && _selectedLocation != null && ctx.CurrentLocation != null)
        {
            _preview = TravelProcessor.PreviewCrossing(
                ctx.CurrentLocation, _selectedLocation, ctx.player, ctx.Weather, ctx.Inventory, map);
        }
        else
        {
            _preview = null;
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

        // Position the popup (20% of screen width)
        int screenWidth = Raylib_cs.Raylib.GetScreenWidth();
        float popupWidth = screenWidth * 0.20f;
        ImGui.SetNextWindowPos(_popupPosition, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(popupWidth, 0), ImGuiCond.Always);

        ImGuiWindowFlags flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                                  ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings;

        if (ImGui.Begin("##TilePopup", flags))
        {
            // Location name
            UiText.Colored(new Vector4(0.9f, 0.85f, 0.7f, 1f), _selectedLocation.Name);

            // Terrain type (if different from name)
            if (_selectedLocation.Name != _selectedLocation.Terrain.ToString())
            {
                UiText.Disabled(_selectedLocation.Terrain.ToString());
            }

            ImGui.Separator();

            // Render feature details
            RenderFeatures(ctx);

            // Sign on the ground - not a feature of the place, but of what passed through
            RenderTracks(ctx);

            // Render NPCs if any
            RenderNPCs(ctx);

            ImGui.Separator();

            // Go button(s) (only if adjacent, passable, and not current tile). Hazardous
            // terrain gets two paces up front instead of a follow-up prompt after "Go".
            if (_isAdjacent && _isPassable && !_isPlayerHere && _preview.HasValue && _preview.Value.IsHazardous)
            {
                var preview = _preview.Value;
                int riskPercent = (int)(preview.RiskLevel * 100);

                UiText.Colored(new Vector4(0.9f, 0.6f, 0.3f, 1f), "Hazardous terrain");

                if (ImGui.Button($"Go careful ({preview.CarefulMinutes} min)", new Vector2(-1, 30)))
                {
                    result = "go_careful";
                }
                if (ImGui.Button($"Go quick ({preview.QuickMinutes} min, {riskPercent}% risk)", new Vector2(-1, 30)))
                {
                    result = "go_quick";
                }
            }
            else if (_isAdjacent && _isPassable && !_isPlayerHere)
            {
                string buttonLabel = _preview.HasValue
                    ? $"Go ({_preview.Value.QuickMinutes} min)"
                    : "Go";

                if (ImGui.Button(buttonLabel, new Vector2(-1, 30)))
                {
                    result = "go";
                }
            }
            else if (_isPlayerHere)
            {
                UiText.Disabled("You are here");
            }
            else if (!_isAdjacent)
            {
                UiText.Disabled("Too far to travel");
            }
            else if (!_isPassable)
            {
                UiText.Disabled("Impassable terrain");
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
                UiText.Colored(color, $"Fire: {phase} ({FormatTime(minutes)})");
            }
            else if (fire.HasEmbers)
            {
                int minutes = (int)(fire.EmberTimeRemaining * 60);
                UiText.Colored(new Vector4(0.8f, 0.4f, 0.2f, 1f), $"Embers ({FormatTime(minutes)})");
            }
        }

        // Shelter
        var shelter = _selectedLocation.GetFeature<ShelterFeature>();
        if (shelter != null)
        {
            hasFeatures = true;
            int insulation = (int)Math.Round(shelter.TemperatureInsulation * 100);
            int wind = (int)Math.Round(shelter.WindCoverage * 100);
            UiText.Text($"Shelter: {insulation}% insulation, {wind}% wind block");
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
                UiText.Text($"Forage: {resourceList}");
            }
            else
            {
                UiText.Disabled("Forage: depleted");
            }

            // Show exploration progress
            double explorationPct = _selectedLocation.GetExplorationPct();
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
        var territory = _selectedLocation.GetFeature<SmallGameFeature>();
        if (territory != null)
        {
            hasFeatures = true;
            UiText.Text($"Game: {territory.GetDescription()}");
        }

        // Water
        var water = _selectedLocation.GetFeature<WaterFeature>();
        if (water != null)
        {
            hasFeatures = true;
            UiText.Text("Water source");
        }

        // Traps
        var traps = _selectedLocation.GetFeature<SnareLineFeature>();
        if (traps != null && traps.SnareCount > 0)
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
        var rack = _selectedLocation.GetFeature<CuringRackFeature>();
        if (rack != null && rack.ItemCount > 0)
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
        var carcass = _selectedLocation.GetFeature<CarcassFeature>();
        if (carcass != null)
        {
            hasFeatures = true;
            string decay = carcass.GetDecayDescription();
            UiText.Text($"Carcass: {carcass.AnimalName} ({decay})");
        }

        // Cache/Storage
        var cache = _selectedLocation.GetFeature<CacheFeature>();
        if (cache != null)
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
        var bedding = _selectedLocation.GetFeature<BeddingFeature>();
        if (bedding != null)
        {
            hasFeatures = true;
            UiText.Text($"Bedding: {bedding.Quality} quality");
        }

        if (!hasFeatures)
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
    private void RenderTracks(GameContext ctx)
    {
        if (_selectedTile == null || ctx.Map == null) return;

        var position = new GridPosition(_selectedTile.Value.x, _selectedTile.Value.y);
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
            UiText.Colored(color, $"{what}{tally} - {age}, heading {track.Heading.ToString().ToLower()}");
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
