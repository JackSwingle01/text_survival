
using text_survival.Bodies;
using text_survival.Events;
using text_survival.IO;

namespace text_survival.Survival;


public class HungerModule(Body owner)
{
	private const double MAX_CALORIES = 2000.0; // Maximum calories stored before fat conversion
	private bool IsStarving => CurrentCalories <= 0;
	private bool wasStarving;
	private double CurrentCalories { get; set; } = MAX_CALORIES / 2;
	private readonly Body owner = owner;

	public void Update(double metabolicRate)
	{
		// todo, actually update with activity level
		// todo have this account for temp too

		double calories = metabolicRate / 24 / 60;  //* timePassed.TotalHours;

		CurrentCalories -= calories;

		if (IsStarving)
		{
			double excessCalories = -CurrentCalories;
			CurrentCalories = 0;
			EventBus.Publish(new StarvingEvent(owner, excessCalories, isNew: !wasStarving));
		}
		else if (wasStarving)
		{
			EventBus.Publish(new StoppedStarvingEvent(owner));
		}
		wasStarving = IsStarving;
	}

	public void AddCalories(double calories)
	{
		CurrentCalories += calories;

		if (CurrentCalories > MAX_CALORIES)
		{
			double excessCalories = CurrentCalories - MAX_CALORIES;
			EventBus.Publish(new CalorieSurplusEvent(owner, excessCalories));
			CurrentCalories = MAX_CALORIES;
		}
	}

	public void Describe()
	{
		double percent = (int)(CurrentCalories / MAX_CALORIES * 100);
		Output.WriteLine("Calorie Store: ", percent, "%");
	}
}
