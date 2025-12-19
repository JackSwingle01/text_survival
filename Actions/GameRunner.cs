using text_survival.Actors.Player;
using text_survival.Actors.NPCs;
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Combat;
using text_survival.Crafting;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.Actions.Expeditions;
using text_survival.UI;

namespace text_survival.Actions;

public class Choice<T>(string? prompt = null)
{
    public string? Prompt = prompt;
    private readonly Dictionary<string, T> options = [];
    public void AddOption(string label, T item)
    {
        options[label] = item;
    }
    public T GetPlayerChoice()
    {
        if (options.Count == 0)
        {
            throw new InvalidOperationException("No Choices Available");
        }
        string choice = Input.Select(Prompt ?? "Choose:", options.Keys);
        return options[choice];
    }
}

public partial class GameRunner(GameContext ctx)
{
    private readonly GameContext ctx = ctx;

    public void Run()
    {
        while (ctx.player.IsAlive)
        {
            GameDisplay.Render(ctx);
            CheckFireWarning();
            RunCampMenu();
            ctx.Update(1);
        }

        // Player died from survival conditions - show death message
        GameDisplay.AddDanger("Your vision fades to black as you collapse...");
        GameDisplay.AddDanger("You have died.");
        GameDisplay.Render(ctx, addSeparator: false);
        Input.WaitForKey();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MAIN MENU
    // ═══════════════════════════════════════════════════════════════════════════

    private void RunCampMenu()
    {
        var choice = new Choice<Action>();

        if (CanRestByFire())
            choice.AddOption("Wait", Wait);

        if (HasActiveFire())
            choice.AddOption("Tend fire", TendFire);

        if (CanStartFire())
            choice.AddOption("Start fire", StartFire);

        // Work around camp - foraging, exploring without leaving
        if (HasCampWork())
            choice.AddOption(GetCampWorkLabel(), WorkAroundCamp);

        // Crafting - make tools from available materials
        if (ctx.Inventory.HasCraftingMaterials)
            choice.AddOption("Crafting", RunCrafting);

        // Leave camp - travel to other locations (only show if destinations exist)
        if (ctx.Camp.Location.Connections.Count > 0)
            choice.AddOption("Leave camp", LeaveCamp);

        if (HasItems())
            choice.AddOption("Inventory", RunInventoryMenu);

        if (ctx.player.Body.IsTired)
            choice.AddOption("Sleep", Sleep);

        choice.GetPlayerChoice().Invoke();
    }

    private bool HasCampWork()
    {
        var campLocation = ctx.Camp.Location;

        if (campLocation.HasFeature<ForageFeature>())
            return true;

        if (ctx.Zone.HasUnrevealedLocations())
            return true;

        return false;
    }

    private string GetCampWorkLabel()
    {
        var options = new List<string>();

        if (ctx.Camp.Location.HasFeature<ForageFeature>())
            options.Add("Forage");

        if (ctx.Zone.HasUnrevealedLocations())
            options.Add("Scout");

        return options.Count > 0
            ? $"Work near camp ({string.Join(", ", options)})"
            : "Work near camp";
    }

    private void WorkAroundCamp()
    {
        var campWorkRunner = new CampWorkRunner(ctx);
        campWorkRunner.Run();
    }

    private void RunCrafting()
    {
        var craftingRunner = new CraftingRunner(ctx);
        craftingRunner.Run();
    }

    private void LeaveCamp()
    {
        var expeditionRunner = new ExpeditionRunner(ctx);
        expeditionRunner.Run();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FIRE MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    private bool HasActiveFire()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null) return false;

        // Show "Tend fire" if there's an active fire AND we have fuel to add
        return (fire.IsActive || fire.HasEmbers) && ctx.Inventory.HasFuel;
    }

    private void CheckFireWarning()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null || (!fire.IsActive && !fire.HasEmbers))
            return;

        // Don't warn when fire is growing
        string phase = fire.GetFirePhase();
        if (phase == "Igniting" || phase == "Building")
            return;

        int minutes = (int)(fire.HoursRemaining * 60);

        if (minutes <= 5)
            GameDisplay.AddDanger($"Your fire will die in {minutes} minutes!");
        else if (minutes <= 15)
            GameDisplay.AddWarning($"Fire burning low - {minutes} minutes remaining.");
    }

    private bool CanStartFire()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();

        bool noFire = fire == null;
        bool coldFire = fire != null && !fire.IsActive && !fire.HasEmbers;

        if (!noFire && !coldFire) return false;

        // Need a fire tool and materials
        bool hasTool = ctx.Inventory.Tools.Any(t =>
            t.Type == ToolType.FireStriker ||
            t.Type == ToolType.HandDrill ||
            t.Type == ToolType.BowDrill);
        return hasTool && ctx.Inventory.CanStartFire;
    }

    private bool CanRestByFire()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        return fire != null && fire.IsActive;
    }

    private void TendFire()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>()!;
        var inv = ctx.Inventory;

        while (true)
        {
            // Build fuel options - only show options that can actually be added
            var fuelChoices = new List<string>();
            var fuelMap = new Dictionary<string, (string name, FuelType type, Func<double> takeFunc)>();

            if (inv.Logs.Count > 0)
            {
                if (fire.CanAddFuel(FuelType.Softwood))
                {
                    string label = $"Add log ({inv.Logs.Count} @ {inv.Logs.Sum():F1}kg)";
                    fuelChoices.Add(label);
                    fuelMap[label] = ("log", FuelType.Softwood, inv.TakeSmallestLog);
                }
                else
                {
                    // Show greyed-out option explaining why logs can't be added yet
                    string disabledLabel = "[dim]Add log (fire too small)[/]";
                    fuelChoices.Add(disabledLabel);
                    fuelMap[disabledLabel] = ("disabled", FuelType.Softwood, () => 0);
                }
            }

            if (inv.Sticks.Count > 0 && fire.CanAddFuel(FuelType.Kindling))
            {
                string label = $"Add stick ({inv.Sticks.Count} @ {inv.Sticks.Sum():F1}kg)";
                fuelChoices.Add(label);
                fuelMap[label] = ("stick", FuelType.Kindling, inv.TakeSmallestStick);
            }

            if (inv.Tinder.Count > 0 && fire.CanAddFuel(FuelType.Tinder))
            {
                string label = $"Add tinder ({inv.Tinder.Count} @ {inv.Tinder.Sum():F2}kg)";
                fuelChoices.Add(label);
                fuelMap[label] = ("tinder", FuelType.Tinder, inv.TakeTinder);
            }

            if (fuelChoices.Count == 0)
            {
                // Check if we have fuel but fire is too cold
                bool hasFuelButTooCold = (inv.Logs.Count > 0 || inv.Sticks.Count > 0) && inv.Tinder.Count == 0;
                if (hasFuelButTooCold)
                    GameDisplay.AddWarning("The fire is too cold. You need tinder to build it up first.");
                else
                    GameDisplay.AddNarrative("You have no fuel to add.");
                return;
            }

            fuelChoices.Add("Done");

            GameDisplay.Render(ctx, addSeparator: false);
            string choice = Input.Select("Add fuel:", fuelChoices);

            if (choice == "Done")
                return;

            var (name, fuelType, takeFunc) = fuelMap[choice];

            // Handle disabled option (fire too small for logs)
            if (name == "disabled")
            {
                GameDisplay.AddWarning("The fire needs to be bigger before you can add logs. Add more kindling first.");
                continue;
            }

            bool hadEmbers = fire.HasEmbers;

            // Take fuel from inventory and add to fire
            double mass = takeFunc();
            fire.AddFuel(mass, fuelType);

            GameDisplay.AddNarrative($"You add a {name} ({mass:F2}kg) to the fire.");

            if (hadEmbers && fire.IsActive)
                GameDisplay.AddNarrative("The embers ignite the fuel! The fire springs back to life.");

            ctx.Update(1);
        }
    }

    private void StartFire()
    {
        var inv = ctx.Inventory;
        var existingFire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        bool relightingFire = existingFire != null;

        if (relightingFire)
            GameDisplay.AddNarrative("You prepare to relight the fire.");
        else
            GameDisplay.AddNarrative("You prepare to start a fire.");

        // Get fire-making tools from aggregate inventory
        var fireTools = inv.Tools.Where(t =>
            t.Type == ToolType.FireStriker ||
            t.Type == ToolType.HandDrill ||
            t.Type == ToolType.BowDrill).ToList();

        if (fireTools.Count == 0)
        {
            GameDisplay.AddWarning("You don't have any fire-making tools!");
            return;
        }

        bool hasTinder = inv.Tinder.Count > 0;
        bool hasKindling = inv.Sticks.Count > 0;

        if (!hasTinder)
        {
            GameDisplay.AddWarning("You don't have any tinder to start a fire!");
            return;
        }

        if (!hasKindling)
        {
            GameDisplay.AddWarning("You don't have any kindling to start a fire!");
            return;
        }

        GameDisplay.AddNarrative($"Materials: {inv.Tinder.Count} tinder, {inv.Sticks.Count} kindling");

        // Build tool options with success chances
        var toolChoices = new List<string>();
        var toolMap = new Dictionary<string, (Tool tool, double chance)>();

        foreach (var tool in fireTools)
        {
            double baseChance = GetFireToolBaseChance(tool);
            var skill = ctx.player.Skills.GetSkill("Firecraft");
            double successChance = baseChance + (skill.Level * 0.1);
            successChance = Math.Clamp(successChance, 0.05, 0.95);

            string label = $"{tool.Name} - {successChance:P0} success chance";
            toolChoices.Add(label);
            toolMap[label] = (tool, successChance);
        }

        toolChoices.Add("Cancel");

        GameDisplay.Render(ctx, addSeparator: false);
        string choice = Input.Select("Choose fire-making tool:", toolChoices);

        if (choice == "Cancel")
        {
            GameDisplay.AddNarrative("You decide not to start a fire right now.");
            return;
        }

        var (selectedTool, finalChance) = toolMap[choice];

        while (true)
        {
            GameDisplay.AddNarrative($"You work with the {selectedTool.Name}...");
            ctx.Update(15);

            bool success = Utils.DetermineSuccess(finalChance);

            // Always consume tinder on attempt
            double tinderUsed = inv.TakeTinder();

            if (success)
            {
                // Also consume a stick for kindling
                double kindlingUsed = inv.TakeSmallestStick();

                var playerSkill = ctx.player.Skills.GetSkill("Firecraft");

                if (relightingFire)
                {
                    GameDisplay.AddSuccess($"Success! You relight the fire! ({finalChance:P0} chance)");
                    existingFire!.AddFuel(tinderUsed, FuelType.Tinder);
                    existingFire.AddFuel(kindlingUsed, FuelType.Kindling);
                    existingFire.IgniteFuel(FuelType.Tinder, tinderUsed);
                }
                else
                {
                    GameDisplay.AddSuccess($"Success! You start a fire! ({finalChance:P0} chance)");
                    var newFire = new HeatSourceFeature();
                    newFire.AddFuel(tinderUsed, FuelType.Tinder);
                    newFire.AddFuel(kindlingUsed, FuelType.Kindling);
                    newFire.IgniteFuel(FuelType.Tinder, tinderUsed);
                    ctx.CurrentLocation.Features.Add(newFire);
                }

                playerSkill.GainExperience(3);
                break;
            }
            else
            {
                GameDisplay.AddWarning($"You failed to start the fire. The tinder was wasted. ({finalChance:P0} chance)");
                ctx.player.Skills.GetSkill("Firecraft").GainExperience(1);

                // Check if retry is possible
                if (inv.Tinder.Count > 0 && inv.Sticks.Count > 0)
                {
                    GameDisplay.Render(ctx, addSeparator: false);
                    if (Input.Confirm($"Try again with {selectedTool.Name}?"))
                        continue;
                }
                break;
            }
        }
    }

    private static double GetFireToolBaseChance(Tool tool)
    {
        return tool.Name switch
        {
            "Hand Drill" => 0.30,
            "Bow Drill" => 0.50,
            "Fire Striker" or "Flint and Steel" => 0.90,
            _ => 0.50  // Default for generic fire strikers
        };
    }

    private void Sleep()
    {
        int hours = Input.ReadInt("How many hours would you like to sleep?");

        if (hours < 1 || hours > 24)
        {
            GameDisplay.AddWarning("You can only sleep for 1-24 hours at a time.");
            return;
        }

        int minutes = hours * 60;
        ctx.player.Body.Rest(minutes);
        ctx.Update(minutes);
    }

    private void Wait()
    {
        // GameDisplay.AddNarrative("You wait, watching your fire...");
        ctx.Update(1, 1.0, 2.0);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // INVENTORY
    // ═══════════════════════════════════════════════════════════════════════════

    private bool HasItems()
    {
        var inv = ctx.Inventory;
        return inv.HasFuel || inv.HasFood || inv.HasWater || inv.Tools.Count > 0;
    }

    private void RunInventoryMenu()
    {
        GameDisplay.RenderInventoryScreen(ctx);
        Input.WaitForKey("Press any key to return...");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // COMBAT
    // ═══════════════════════════════════════════════════════════════════════════

    private void StartCombat(Npc enemy)
    {
        if (!enemy.IsAlive) return;

        GameDisplay.AddNarrative("!");
        Thread.Sleep(500);
        GameDisplay.AddNarrative(CombatNarrator.DescribeCombatStart(ctx.player, enemy));

        ctx.player.IsEngaged = true;
        enemy.IsEngaged = true;

        bool enemyFirstStrike = enemy.Speed > ctx.player.Speed;

        if (enemyFirstStrike)
        {
            GameDisplay.AddNarrative($"The {enemy.Name} moves with surprising speed!");
            Thread.Sleep(500);
            EnemyCombatTurn(enemy);
        }
        else
        {
            GameDisplay.AddNarrative("You're quick to react, giving you the initiative!");
            Thread.Sleep(500);
            PlayerCombatTurn(enemy);
        }
    }

    private void PlayerCombatTurn(Npc enemy)
    {
        if (!ctx.player.IsAlive || !enemy.IsAlive || !ctx.player.IsEngaged)
        {
            EndCombat(enemy);
            return;
        }

        GameDisplay.AddNarrative("─────────────────────────────────────");
        DisplayCombatStatus(enemy);

        var choice = new Choice<Action>();
        choice.AddOption($"Attack {enemy.Name}", () => AttackEnemy(enemy));

        if (ctx.player.Skills.Fighting.Level > 1)
            choice.AddOption($"Targeted Attack {enemy.Name}", () => TargetedAttackEnemy(enemy));

        if (ctx.player.Speed > 0.25)
            choice.AddOption("Flee", () => AttemptFlee(enemy));

        choice.GetPlayerChoice().Invoke();
    }

    private void EnemyCombatTurn(Npc enemy)
    {
        if (!ctx.player.IsAlive || !enemy.IsAlive || !enemy.IsEngaged)
        {
            EndCombat(enemy);
            return;
        }

        Thread.Sleep(500);
        enemy.Attack(ctx.player);

        if (!ctx.player.IsAlive || !enemy.IsAlive)
            EndCombat(enemy);
        else
            PlayerCombatTurn(enemy);
    }

    private void AttackEnemy(Npc enemy)
    {
        var weapon = ctx.Inventory.Weapon?.ToWeapon();
        ctx.player.Attack(enemy, weapon);

        if (!enemy.IsAlive)
            EndCombat(enemy);
        else
            EnemyCombatTurn(enemy);
    }

    private void TargetedAttackEnemy(Npc enemy)
    {
        int fightingSkill = ctx.player.Skills.Fighting.Level;
        var targetPart = SelectTargetPart(enemy, fightingSkill);

        if (targetPart != null)
        {
            var weapon = ctx.Inventory.Weapon?.ToWeapon();
            ctx.player.Attack(enemy, weapon, targetPart.Name);

            if (!enemy.IsAlive)
                EndCombat(enemy);
            else
                EnemyCombatTurn(enemy);
        }
        else
        {
            PlayerCombatTurn(enemy);
        }
    }

    private BodyRegion? SelectTargetPart(Actor enemy, int depth)
    {
        if (depth <= 0)
        {
            GameDisplay.AddWarning("You don't have enough skill to target an attack");
            return null;
        }

        GameDisplay.AddNarrative($"Where do you want to target your attack on the {enemy.Name}?");

        List<BodyRegion> allParts = [];
        foreach (var part in enemy.Body.Parts)
        {
            if (depth > 0)
                allParts.Add(part);
        }

        return Input.SelectOrCancel("Select target:", allParts);
    }

    private void AttemptFlee(Npc enemy)
    {
        if (CombatUtils.SpeedCheck(ctx.player, enemy))
        {
            GameDisplay.AddNarrative("You got away!");
            enemy.IsEngaged = false;
            ctx.player.IsEngaged = false;
            ctx.player.Skills.Reflexes.GainExperience(2);
        }
        else
        {
            GameDisplay.AddNarrative($"You weren't fast enough to get away from {enemy.Name}!");
            ctx.player.Skills.Reflexes.GainExperience(1);
            EnemyCombatTurn(enemy);
        }
    }

    private void EndCombat(Npc enemy)
    {
        ctx.player.IsEngaged = false;
        enemy.IsEngaged = false;

        if (!ctx.player.IsAlive)
        {
            GameDisplay.AddDanger("Your vision fades to black as you collapse... You have died!");
            Environment.Exit(0);
        }
        else if (!enemy.IsAlive)
        {
            string[] victoryMessages = [
                $"The {enemy.Name} collapses, defeated!",
                $"You stand victorious over the fallen {enemy.Name}!",
                $"With a final blow, you bring down the {enemy.Name}!"
            ];
            GameDisplay.AddNarrative(victoryMessages[Utils.RandInt(0, victoryMessages.Length - 1)]);

            int xpGain = CalculateExperienceGain(enemy);
            GameDisplay.AddNarrative($"You've gained {xpGain} fighting experience!");
            ctx.player.Skills.Fighting.GainExperience(xpGain);
        }
    }

    private static int CalculateExperienceGain(Npc enemy)
    {
        int baseXP = 5;
        double sizeMultiplier = Math.Clamp(enemy.Body.WeightKG / 50, 0.5, 3.0);
        double weaponMultiplier = Math.Clamp(enemy.ActiveWeapon.Damage / 8, 0.5, 2.0);
        return (int)(baseXP * sizeMultiplier * weaponMultiplier);
    }

    private void DisplayCombatStatus(Npc enemy)
    {
        double playerVitality = ctx.player.Vitality;
        string playerStatus = $"You: {Math.Round(playerVitality * 100, 0)}% Vitality";
        AddHealthMessage(playerStatus, playerVitality);

        double enemyVitality = enemy.Vitality;
        string enemyStatus = $"{enemy.Name}: {Math.Round(enemyVitality * 100, 0)}% Vitality";
        AddHealthMessage(enemyStatus, enemyVitality);
    }

    private static void AddHealthMessage(string message, double healthPercentage)
    {
        if (healthPercentage < 0.2)
            GameDisplay.AddDanger(message);
        else if (healthPercentage < 0.5)
            GameDisplay.AddWarning(message);
        else
            GameDisplay.AddSuccess(message);
    }
}