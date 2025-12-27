using text_survival.Actions.Handlers;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.UI;
using text_survival.Web;
using text_survival.Web.Dto;

namespace text_survival.Actions.Expeditions;

/// <summary>
/// Handles grid-based tile-to-tile movement.
/// Player clicks adjacent tiles to move one at a time.
/// </summary>
public class GridTravelRunner(GameContext ctx)
{
    private readonly GameContext _ctx = ctx;
    private bool PlayerDied => !_ctx.player.IsAlive;

    /// <summary>
    /// Main grid travel loop. Renders grid and waits for player input.
    /// Returns when player chooses to stop (arrives at a location with activities)
    /// or dies.
    /// </summary>
    public void DoGridTravel()
    {
        // Check for pending travel target from map click at camp
        if (_ctx.PendingTravelTarget.HasValue)
        {
            var (targetX, targetY) = _ctx.PendingTravelTarget.Value;
            _ctx.PendingTravelTarget = null; // Clear pending target

            var targetTile = _ctx.Grid![targetX, targetY];
            if (targetTile != null)
            {
                bool success = TryMoveToTile(targetTile);
                if (!success && PlayerDied) return;

                // After successful move to a named location, offer activities
                if (success && _ctx.CurrentTile!.HasNamedLocation)
                {
                    // Discovery text on first visit
                    if (!_ctx.CurrentTile.NamedLocation!.Explored)
                    {
                        DiscoverLocation(_ctx.CurrentTile);
                    }

                    // Check if player wants to stay and do activities
                    if (HasActivities(_ctx.CurrentTile))
                    {
                        return; // Exit travel loop - player can now do activities
                    }
                }
            }
        }

        while (!PlayerDied)
        {
            // Render grid and wait for input
            var response = WebIO.RenderGridAndWaitForInput(_ctx, GetStatusText());

            // Check response type
            if (response.Type == "move" && response.TargetX.HasValue && response.TargetY.HasValue)
            {
                var targetTile = _ctx.Grid![response.TargetX.Value, response.TargetY.Value];
                if (targetTile != null)
                {
                    bool success = TryMoveToTile(targetTile);
                    if (!success && PlayerDied) return;

                    // After successful move to a named location, offer activities
                    if (success && _ctx.CurrentTile!.HasNamedLocation)
                    {
                        // Discovery text on first visit
                        if (!_ctx.CurrentTile.NamedLocation!.Explored)
                        {
                            DiscoverLocation(_ctx.CurrentTile);
                        }

                        // Check if player wants to stay and do activities
                        if (HasActivities(_ctx.CurrentTile))
                        {
                            return; // Exit travel loop - player can now do activities
                        }
                    }
                }
            }
            else if (response.Type == "menu" || response.ChoiceIndex == 0)
            {
                // Player wants to access menu (e.g., at camp or named location)
                return;
            }
        }
    }

    /// <summary>
    /// Attempt to move to an adjacent tile.
    /// Returns true if successful, false if failed or player died.
    /// </summary>
    public bool TryMoveToTile(Tile targetTile)
    {
        // Validate the move
        string? error = GridTravelProcessor.ValidateMove(_ctx.CurrentTile!, targetTile, _ctx.Grid!);
        if (error != null)
        {
            GameDisplay.AddWarning(_ctx, error);
            return false;
        }

        // Calculate travel time
        int travelTime = GridTravelProcessor.GetTravelTimeMinutes(
            targetTile, _ctx.player, _ctx.Weather, _ctx.Inventory);

        // Check for hazardous terrain
        bool isHazardous = GridTravelProcessor.IsHazardousTerrain(targetTile);
        bool quickTravel = true;

        if (isHazardous)
        {
            int carefulTime = GridTravelProcessor.GetCarefulTravelTimeMinutes(
                targetTile, _ctx.player, _ctx.Weather, _ctx.Inventory);
            double injuryRisk = GridTravelProcessor.GetInjuryRisk(
                targetTile, _ctx.player, _ctx.Weather);
            string hazardDesc = GridTravelProcessor.GetHazardDescription(targetTile, _ctx.Weather);

            quickTravel = WebIO.PromptHazardChoice(
                _ctx, targetTile, hazardDesc,
                travelTime, carefulTime, injuryRisk * 100);

            if (!quickTravel)
            {
                travelTime = carefulTime;
            }
        }

        // Execute the movement
        bool died = RunTravelWithProgress(travelTime);
        if (died) return false;

        // Check for injury if quick travel through hazard
        if (isHazardous && quickTravel)
        {
            double injuryRisk = GridTravelProcessor.GetInjuryRisk(targetTile, _ctx.player, _ctx.Weather);
            if (injuryRisk > 0 && Utils.RandDouble(0, 1) < injuryRisk)
            {
                ApplyTravelInjury(targetTile);
            }
        }

        // Move to the new tile
        MoveToTile(targetTile);

        return true;
    }

    /// <summary>
    /// Move player to a tile and update visibility.
    /// </summary>
    private void MoveToTile(Tile tile)
    {
        _ctx.CurrentTile = tile;
        tile.MarkExplored();

        // Update fog of war
        int sightRange = _ctx.Grid!.GetSightRange(tile);
        _ctx.Grid.UpdateVisibility(tile.Position, sightRange);

        // Update CurrentLocation for compatibility with existing systems
        if (tile.HasNamedLocation)
        {
            _ctx.CurrentLocation = tile.NamedLocation!;
        }
    }

    /// <summary>
    /// Handle first-time discovery of a named location.
    /// </summary>
    private void DiscoverLocation(Tile tile)
    {
        var location = tile.NamedLocation!;
        location.MarkExplored();

        string discoveryMessage = $"You discover: {location.Name}";
        if (!string.IsNullOrEmpty(location.Tags))
            discoveryMessage += $" {location.Tags}";

        GameDisplay.AddNarrative(_ctx, discoveryMessage);

        if (!string.IsNullOrEmpty(location.DiscoveryText))
        {
            GameDisplay.AddNarrative(_ctx, location.DiscoveryText);
        }
    }

    /// <summary>
    /// Check if a tile has activities the player can do.
    /// </summary>
    private bool HasActivities(Tile tile)
    {
        if (!tile.HasNamedLocation) return false;

        // Camp always has activities
        if (tile.NamedLocation == _ctx.Camp) return true;

        // Check for workable features
        return tile.NamedLocation!.HasWorkOptions(_ctx);
    }

    /// <summary>
    /// Run travel time with event checking.
    /// Returns true if player died.
    /// </summary>
    private bool RunTravelWithProgress(int timeMinutes)
    {
        WebIO.RenderGrid(_ctx, $"Traveling...");

        // Update game state for travel duration
        _ctx.Update(timeMinutes, ActivityType.Traveling);

        if (PlayerDied) return true;

        // Handle any triggered events - check EventOccurredLastUpdate flag
        if (_ctx.EventOccurredLastUpdate)
        {
            // Event was handled by Update(), check if we need to stop
            if (PlayerDied) return true;
        }

        return false;
    }

    /// <summary>
    /// Apply injury from quick travel through hazardous terrain.
    /// </summary>
    private void ApplyTravelInjury(Tile tile)
    {
        // If tile has a named location, use its properties for injury
        if (tile.HasNamedLocation)
        {
            TravelHandler.ApplyTravelInjury(_ctx, tile.NamedLocation!);
        }
        else
        {
            // For generic terrain tiles, apply a simple injury based on terrain type
            string hazardType = tile.Terrain switch
            {
                TerrainType.Water => "slip on the ice",
                TerrainType.Marsh => "stumble in the frozen marsh",
                TerrainType.Rock => "lose your footing on loose rocks",
                TerrainType.Hills => "slip on the steep terrain",
                _ => "stumble on the hazardous ground"
            };

            GameDisplay.AddDanger(_ctx, $"You {hazardType} and hurt yourself.");

            // Apply minor blunt damage to a random leg
            _ctx.player.Body.Damage(new Bodies.DamageInfo(
                amount: 3,
                type: Bodies.DamageType.Blunt,
                target: Bodies.BodyTarget.AnyLeg
            ));
        }
    }

    /// <summary>
    /// Get status text for grid display.
    /// </summary>
    private string GetStatusText()
    {
        var tile = _ctx.CurrentTile!;

        if (tile.HasNamedLocation)
        {
            if (tile.NamedLocation == _ctx.Camp)
                return "At Camp - Click adjacent tile to move";
            return $"At {tile.NamedLocation!.Name} - Click adjacent tile to move";
        }

        return $"{tile.Terrain} - Click adjacent tile to move";
    }
}
