using text_survival.Actors.Player;
using text_survival.Core;
using text_survival.Environments;
using text_survival.Environments.Features;

namespace text_survival.Actions.Expeditions;

public class ExpeditionRunner
{
    public class SegmentResult(int timeElapsed)
    {
        public int TimeElapsed { get; set; } = timeElapsed;
    }

    private readonly int SEGMENT_TIME_MINUTES = 5;
    public SegmentResult RunExpedtionSegment(Expedition expedition, Player player)
    {
        int t = 0;
        while (t < SEGMENT_TIME_MINUTES)
        {
            World.Update(1);
            expedition.IncrementTime(1);
            t++;

            // check for events, encounters, etc.
            // TODO

            if (expedition.IsPhaseComplete())
            {
                break;
            }
        }

        if (expedition.CurrentPhase == ExpeditionPhase.Working)
        {
            DoWork(expedition, t);
        }
        AddSegmentUpdate(expedition, t);
        if (expedition.IsPhaseComplete())
        {
            HandlePhaseCompletion(expedition);
        }
        return new SegmentResult(t);
    }

    private void HandlePhaseCompletion(Expedition exp)
    {
        exp.AdvancePhase();
        if (exp.IsComplete)
        {
            exp.AddLog("You have returned from your expedition.");
        }
        else
        {
            exp.AddLog($"You are now {exp.GetPhaseDisplayName().ToLower()}.");
        }
    }

    private static void DoWork(Expedition exp, int minutes)
    {
        if (exp.Type == ExpeditionType.Forage)
        {
            DoForageWork(exp, minutes);
        }
    }
    private static void DoForageWork(Expedition exp, int minutes)
    {
        Location location = exp.endLocation;
        var feature = location.GetFeature<ForageFeature>();
        if (feature is null) throw new InvalidOperationException("Can't forage here.");
        var items = feature.Forage(minutes / 60);
        exp.LootCollected.AddRange(items);
        if (items.Count > 0)
        {
            var groupedItems = items
                .GroupBy(item => item.Name)
                .Select(group => $"{group.Key} ({group.Count()})")
                .ToList();

            string timeText = minutes == 60 ? "1 hour" : $"{minutes} minutes";
            exp.AddLog($"You spent {timeText} searching and found: {string.Join(", ", groupedItems)}");
        }
        else
        {
            string timeText = minutes == 60 ? "1 hour" : $"{minutes} minutes";
            exp.AddLog($"You spent {timeText} searching but found nothing.");
        }
    }

    private static void AddSegmentUpdate(Expedition expedition, int minutesPassed)
    {
        string action = expedition.CurrentPhase switch
        {
            ExpeditionPhase.TravelingOut => "walk",
            ExpeditionPhase.Working => "work",
            ExpeditionPhase.TravelingBack => "walk",
            _ => "unknown"
        };
        expedition.AddLog($"You {action} for {minutesPassed} minutes.");
    }

    public static string GetFireMarginMessage(double marginMinutes)
    {
        if (double.IsNegativeInfinity(marginMinutes)) return "You have no fire.";
        if (marginMinutes < 0) return "The fire probably won't last until you return. Make sure you know what you're doing.";
        if (marginMinutes < 15) return "The fire should barely last until you're back. But it will be tight.";
        if (marginMinutes < 30) return "You should have enough time to get back to the fire as long as you don't have any delays.";
        if (marginMinutes < 60) return "You have a decent fire, it should last until you get back with a good margin.";
        return "You have plenty of time left on the fire. You should be good to go.";
    }


}