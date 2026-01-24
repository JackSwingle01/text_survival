using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Handles curing rack management actions.
/// Static handler that takes GameContext and mutates state directly.
/// </summary>
public static class CuringRackHandler
{
    public static void UseCuringRack(GameContext ctx)
    {
        var rack = ctx.Camp.GetFeature<CuringRackFeature>();
        var inv = ctx.Inventory;

        while (true)
        {
            GameDisplay.AddNarrative(ctx, rack!.GetDescription());
            GameDisplay.Render(ctx, statusText: "Checking rack.");

            var options = new List<string>();
            var actions = new Dictionary<string, Action>();

            // Collect finished items
            if (rack.HasReadyItems)
            {
                string collectOpt = "Collect finished items";
                options.Add(collectOpt);
                actions[collectOpt] = () =>
                {
                    var loot = new Inventory();
                    int collected = rack.CollectFinished(loot);
                    GameDisplay.AddSuccess(ctx, $"You collected {collected} item(s) from the rack.");
                    InventoryCapacityHelper.CombineAndReport(ctx, loot);
                };
            }

            // Add items to rack (if space available)
            if (rack.HasSpace)
            {
                // Scraped hide -> Cured hide
                if (inv.Count(Resource.ScrapedHide) > 0)
                {
                    double w = inv.Peek(Resource.ScrapedHide);
                    string opt = $"Hang scraped hide ({w:F1}kg) - 2 days to cure";
                    options.Add(opt);
                    actions[opt] = () =>
                    {
                        double weight = inv.Pop(Resource.ScrapedHide);
                        rack.AddItem(CurableItemType.ScrapedHide, weight);
                        GameDisplay.AddSuccess(ctx, "You hang the hide on the rack to cure.");
                    };
                }

                // Raw meat -> Dried meat
                if (inv.Count(Resource.RawMeat) > 0)
                {
                    double w = inv.Peek(Resource.RawMeat);
                    string opt = $"Hang raw meat ({w:F1}kg) - 1 day to dry";
                    options.Add(opt);
                    actions[opt] = () =>
                    {
                        double weight = inv.Pop(Resource.RawMeat);
                        rack.AddItem(CurableItemType.RawMeat, weight);
                        GameDisplay.AddSuccess(ctx, "You hang the meat on the rack to dry.");
                    };
                }

                // Raw fish -> Dried fish
                if (inv.Count(Resource.RawFish) > 0)
                {
                    double w = inv.Peek(Resource.RawFish);
                    string opt = $"Hang raw fish ({w:F1}kg) - 16 hours to dry";
                    options.Add(opt);
                    actions[opt] = () =>
                    {
                        double weight = inv.Pop(Resource.RawFish);
                        rack.AddItem(CurableItemType.RawFish, weight);
                        GameDisplay.AddSuccess(ctx, "You hang the fish on the rack to dry.");
                    };
                }

                // Berries -> Dried berries
                if (inv.Count(Resource.Berries) > 0)
                {
                    double w = inv.Peek(Resource.Berries);
                    string opt = $"Spread berries ({w:F2}kg) - 12 hours to dry";
                    options.Add(opt);
                    actions[opt] = () =>
                    {
                        double weight = inv.Pop(Resource.Berries);
                        rack.AddItem(CurableItemType.Berries, weight);
                        GameDisplay.AddSuccess(ctx, "You spread the berries on the rack to dry.");
                    };
                }
            }
            else if (!rack.HasReadyItems)
            {
                GameDisplay.AddNarrative(ctx, "The rack is full.");
            }

            options.Add("Done");

            if (options.Count == 1)
            {
                // Nothing to do
                break;
            }

            string choice = Input.Select(ctx, "Curing rack:", options);

            if (choice == "Done")
                break;

            actions[choice]();
            ctx.Update(2, ActivityType.Crafting); // Brief time to add/collect
        }
    }
}
