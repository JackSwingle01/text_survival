using text_survival.Effects;

namespace text_survival.Survival;


public class SurvivalProcessorResult(SurvivalData data)
{
	public SurvivalData Data { get; set; } = data;
	public List<Effect> Effects { get; set; } = [];
	public List<string> Messages { get; set; } = [];
}

