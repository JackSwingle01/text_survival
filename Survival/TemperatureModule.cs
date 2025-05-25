using text_survival.Bodies;
using text_survival.Events;
using text_survival.IO;

namespace text_survival.Survival;

public class TemperatureModule(Body owner)
{
    private readonly Body owner = owner;
    private double BodyTemperature => owner.BodyTemperature;
    private bool IsWarming;
    private TemperatureEnum TemperatureEffect = TemperatureEnum.Warm;

    private const double BaseBodyTemperature = 98.6F;
    private const double SevereHypothermiaThreshold = 89.6; // °F
    private const double HypothermiaThreshold = 95.0;  // °F
    private const double HyperthermiaThreshold = 99.5; // °F  

    private enum TemperatureEnum
    {
        Warm,
        Cool,
        Cold,
        Freezing,
        Hot,
    }

    public void Update(double environmentalTemp, double equipmentInsulation)
    {
        double naturalInsulation = Math.Clamp(owner.CalculateColdResistance(), 0, 1); // 0-1
        double totalInsulation = Math.Clamp(naturalInsulation + equipmentInsulation, 0, 0.95);

        double skinTemp = BodyTemperature - 8.4;
        double tempDifferential = environmentalTemp - skinTemp;
        double insulatedDiff = tempDifferential * (1 - totalInsulation);
        double tempDiffMagnitude = Math.Abs(insulatedDiff);
        double baseRate = 1.0 / 120.0;
        double exponentialFactor = 1.0 + (tempDiffMagnitude / 40.0);
        double rate = baseRate * exponentialFactor;

        // double surfaceAreaFactor = Math.Pow(body.Weight / 70.0, -0.2);

        double tempChange = insulatedDiff * rate;
        owner.BodyTemperature += tempChange;

        IsWarming = tempChange > 0;

        UpdateTemperatureEffect();
    }


    private void UpdateTemperatureEffect()
    {
        TemperatureEnum oldTemperature = TemperatureEffect;

        // Normal body temperature, no effects
        if (BodyTemperature >= BaseBodyTemperature && BodyTemperature <= HyperthermiaThreshold)
        {
            TemperatureEffect = TemperatureEnum.Warm;
        }
        else if (BodyTemperature >= HypothermiaThreshold && BodyTemperature < BaseBodyTemperature)
        {
            TemperatureEffect = TemperatureEnum.Cool;
        }

        else if (BodyTemperature > SevereHypothermiaThreshold && BodyTemperature <= HypothermiaThreshold)
        {
            TemperatureEffect = TemperatureEnum.Cold;
            EventBus.Publish(new BodyColdEvent(owner, oldTemperature != TemperatureEffect));
        }
        else if (BodyTemperature < SevereHypothermiaThreshold)
        {
            TemperatureEffect = TemperatureEnum.Freezing;
            EventBus.Publish(new BodyColdEvent(owner, oldTemperature != TemperatureEffect));
        }
        else if (BodyTemperature > HyperthermiaThreshold)
        {
            TemperatureEffect = TemperatureEnum.Hot;
            EventBus.Publish(new BodyHotEvent(owner, oldTemperature != TemperatureEffect));
        }
    }


    public void Describe()
    {
        string tempChange = IsWarming ? "Warming up" : "Getting colder";
        Output.WriteLine("Body Temperature: ", BodyTemperature, "°F (", TemperatureEffect, "), ", tempChange);
    }
}
