using text_survival.Actors.NPCs;
using text_survival.Combat;
using text_survival.IO;

namespace text_survival.Actions;

public class ActionBuilder
{
    private string _name = "";
    private readonly List<Func<GameContext, bool>> _isAvailableRequirements = [];
    private readonly List<Action<GameContext>> _onExecuteActions = [];
    private Func<GameContext, List<IGameAction>>? _getNextActions;
    private string? _userPrompt;
    private int _timeInMinutes = 1; // Default: all actions take 1 minute unless specified

    public ActionBuilder Named(string name)
    {
        _name = name;
        return this;
    }

    public ActionBuilder When(Func<GameContext, bool> condition)
    {
        _isAvailableRequirements.Add(condition);
        return this;
    }

    public ActionBuilder Do(Action<GameContext> action)
    {
        _onExecuteActions.Add(action);
        return this;
    }

    public ActionBuilder ThenShow(Func<GameContext, List<IGameAction>> nextActions)
    {
        _getNextActions = nextActions;
        return this;
    }

    public ActionBuilder ThenReturn()
    {
        _getNextActions = _ => [];
        return this;
    }

    public ActionBuilder WithPrompt(string prompt)
    {
        _userPrompt = prompt;
        return this;
    }

    public void SetTimeInMinutes(int minutes)
    {
        _timeInMinutes = minutes;
    }

    public IGameAction Build()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            throw new InvalidOperationException("Action name is required");
        }

        Action<GameContext>? combinedAction = null;
        if (_onExecuteActions.Count > 0)
        {
            combinedAction = ctx => _onExecuteActions.ForEach(x => x(ctx));
        }

        // combines all conditional requirements with AND. i.e returns true iff all are true
        Func<GameContext, bool>? combinedRequirements = null;
        if (_isAvailableRequirements.Count > 0)
        {
            combinedRequirements = ctx => _isAvailableRequirements.All(x => x(ctx));
        }

        return new DynamicAction(
            name: _name,
            isAvailable: combinedRequirements,
            onExecute: combinedAction,
            getNextActions: _getNextActions,
            userPrompt: _userPrompt,
            timeInMinutes: _timeInMinutes
        );
    }
}

public static class ActionBuilderExtensions
{
    public static ActionBuilder CreateAction(string name) => new ActionBuilder().Named(name);

    public static ActionBuilder OnlyIfHasItems(this ActionBuilder b)
    {
        return b.When(ctx => ctx.player.inventoryManager.Items.Count > 0);
    }

    public static ActionBuilder ShowMessage(this ActionBuilder b, string message)
    {
        return b.Do(_ => Output.WriteLine(message));
    }

    public static ActionBuilder ThenOpenInventory(this ActionBuilder b)
    {
        return b.ThenShow(ctx => [ActionFactory.Inventory.OpenInventory()]);
    }

    public static ActionBuilder ThenLookAround(this ActionBuilder b)
    {
        return b.ThenShow(ctx => [ActionFactory.Describe.LookAround(ctx.CurrentLocation)]);
    }

    public static ActionBuilder AndGainExperience(this ActionBuilder b, string skillName, int xp = 1)
    {
        return b.Do(ctx => ctx.player.Skills.GetSkill(skillName).GainExperience(xp));
    }

    public static ActionBuilder TakesMinutes(this ActionBuilder b, int minutes)
    {
        b.SetTimeInMinutes(minutes);
        return b;
    }

    public static ActionBuilder OnlyIfCanBypassHostiles(this ActionBuilder b)
    {
        return b.When(ctx =>
        {
            Npc? npc = CombatUtils.GetFastestHostileNpc(ctx.CurrentLocation);
            return npc == null || CombatUtils.SpeedCheck(ctx.player, npc);
        });
    }
    public static ActionBuilder OnlyIfBlockedByHostiles(this ActionBuilder b)
    {
        return b.When(ctx =>
        {
            Npc? npc = CombatUtils.GetFastestHostileNpc(ctx.CurrentLocation);
            return npc != null && !CombatUtils.SpeedCheck(ctx.player, npc);
        });
    }

    public static ActionBuilder WaitForUserInputToContinue(this ActionBuilder b)
    {
        return b.Do(_ =>
        {
            Output.WriteLine("Press any key to continue...");
            Input.ReadKey(true);
        });
    }
}