using text_survival.Actors;
using text_survival.Environments;

namespace text_survival.Combat;

public class CombatOrchestrator
{
    
    public List<CombatScenario> scenarios = [];

    public void Update()
    {
        foreach(var fight in scenarios)
        {
            fight.ProcessAITurns();
            if (fight.IsOver)
            {
                scenarios.Remove(fight);
            }
        }
    } 

    public void InitiateCombat(List<Actor> combatants, Location location)
    {
        
    }

}