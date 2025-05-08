using System.Collections.Generic;
using System.Linq;
using text_survival.IO;

namespace text_survival.Actors
{
    public class Body
    {
        private readonly BodyPart rootPart;
        private double bodyFat;
        private double muscle;
        private double weight;
        private readonly double baseWeight;

        public Body(BodyPart rootPart, double overallWeight, double fatPercent, double musclePercent)
        {
            if (fatPercent + musclePercent > 100)
                throw new ArgumentException("Fat and muscle percentages cannot exceed 100% combined.");
            if (overallWeight < 0 || fatPercent < 0 || musclePercent < 0)
                throw new ArgumentException("Weight and percentages cannot be negative.");

            this.rootPart = rootPart;
            bodyFat = overallWeight * (fatPercent / 100);
            muscle = overallWeight * (musclePercent / 100);
            baseWeight = overallWeight - bodyFat - muscle;
            UpdateWeight();
        }

        public double BodyFat
        {
            get => bodyFat;
            set
            {
                bodyFat = Math.Max(value, 0);
                UpdateWeight();
            }
        }

        public double Muscle
        {
            get => muscle;
            set
            {
                muscle = Math.Max(value, 0);
                UpdateWeight();
            }
        }

        public double BodyFatPercentage => Weight > 0 ? (BodyFat / Weight) * 100 : 0;
        public double MusclePercentage => Weight > 0 ? (Muscle / Weight) * 100 : 0;
        public double Weight => weight;

        private void UpdateWeight()
        {
            weight = baseWeight + bodyFat + muscle;
        }

        public double GetStat(string stat)
        {
            return stat switch
            {
                "Strength" => GetCapacity("Manipulation") * (MusclePercentage / 100),
                "Speed" => GetCapacity("Moving") * (1 - BodyFatPercentage / 100) * (baseWeight / Weight),
                "Vitality" => (GetCapacity("Breathing") + GetCapacity("BloodPumping") + GetCapacity("Digestion")) / 3 * (MusclePercentage / 100 + BodyFatPercentage / 200),
                "Perception" => (GetCapacity("Sight") + GetCapacity("Hearing")) / 2,
                _ => 0
            };
        }

        public double GetCapacity(string capacity)
        {
            var parts = GetAllParts();
            return capacity switch
            {
                "Moving" => CalculateMinCapacity(parts, ["Leg", "Spine", "Pelvis"], "Moving"),
                "Manipulation" => CalculateMinCapacity(parts, ["Arm", "Hand", "Clavicle"], "Manipulation"),
                "Breathing" => CalculateMinCapacity(parts, ["Lung", "Ribcage", "Sternum"], "Breathing"),
                "Consciousness" => CalculateSinglePartCapacity(parts, "Brain", "Consciousness"),
                "Sight" => CalculateAverageCapacity(parts, ["Eye"], "Sight"),
                "Hearing" => CalculateAverageCapacity(parts, ["Ear"], "Hearing"),
                "BloodPumping" => CalculateSinglePartCapacity(parts, "Heart", "BloodPumping"),
                "Digestion" => CalculateMinCapacity(parts, ["Stomach", "Liver"], "Digestion"),
                "BloodFiltration" => CalculateAverageCapacity(parts, ["Kidney"], "BloodFiltration"),
                "Eating" => CalculateMinCapacity(parts, ["Mouth", "Jaw"], "Eating"),
                "Talking" => CalculateMinCapacity(parts, ["Mouth", "Jaw", "Tongue"], "Talking"),
                _ => 1.0
            };
        }

        private double CalculateMinCapacity(List<BodyPart> parts, string[] partNames, string capacity)
        {
            var relevantParts = parts.Where(p => partNames.Any(name => p.Name.Contains(name)));
            return relevantParts.Any()
                ? relevantParts.Min(p => p.Capacities.GetValueOrDefault(capacity, 1.0) * p.Health / p.MaxHealth)
                : 1.0;
        }

        private double CalculateAverageCapacity(List<BodyPart> parts, string[] partNames, string capacity)
        {
            var relevantParts = parts.Where(p => partNames.Any(name => p.Name.Contains(name)));
            return relevantParts.Any()
                ? relevantParts.Average(p => p.Capacities.GetValueOrDefault(capacity, 1.0) * p.Health / p.MaxHealth)
                : 1.0;
        }

        private double CalculateSinglePartCapacity(List<BodyPart> parts, string partName, string capacity)
        {
            var part = parts.FirstOrDefault(p => p.Name == partName);
            return part != null
                ? part.Capacities.GetValueOrDefault(capacity, 1.0) * part.Health / part.MaxHealth
                : 1.0;
        }

        public void Damage(double amount)
        {
            rootPart.Damage(amount);
        }

        public void Heal(double amount)
        {
            rootPart.Heal(amount);
        }

        public bool IsAlive => rootPart.Health > 0;

        private List<BodyPart> GetAllParts()
        {
            var parts = new List<BodyPart> { rootPart };
            foreach (var part in rootPart.Parts)
            {
                parts.AddRange(GetSubParts(part));
            }
            return parts;
        }

        private List<BodyPart> GetSubParts(BodyPart part)
        {
            var parts = new List<BodyPart> { part };
            foreach (var subPart in part.Parts)
            {
                parts.AddRange(GetSubParts(subPart));
            }
            return parts;
        }

        public void Describe()
        {
            Output.WriteLine("Body Stats:");
            Output.WriteLine($"Strength: {Math.Round(GetStat("Strength"), 1)}");
            Output.WriteLine($"Speed: {Math.Round(GetStat("Speed"), 1)}");
            Output.WriteLine($"Vitality: {Math.Round(GetStat("Vitality"), 1)}");
            Output.WriteLine($"Perception: {Math.Round(GetStat("Perception"), 1)}");
            Output.WriteLine($"Body Fat: {Math.Round(BodyFat, 1)} kg ({Math.Round(BodyFatPercentage, 1)}%)");
            Output.WriteLine($"Muscle: {Math.Round(Muscle, 1)} kg ({Math.Round(MusclePercentage, 1)}%)");
            Output.WriteLine($"Weight: {Math.Round(Weight, 1)} kg");
            Output.WriteLine("Capacities:");
            Output.WriteLine($"Moving: {Math.Round(GetCapacity("Moving") * 100, 1)}%");
            Output.WriteLine($"Manipulation: {Math.Round(GetCapacity("Manipulation") * 100, 1)}%");
            Output.WriteLine($"Breathing: {Math.Round(GetCapacity("Breathing") * 100, 1)}%");
            Output.WriteLine($"Consciousness: {Math.Round(GetCapacity("Consciousness") * 100, 1)}%");
            Output.WriteLine($"Sight: {Math.Round(GetCapacity("Sight") * 100, 1)}%");
            Output.WriteLine($"Hearing: {Math.Round(GetCapacity("Hearing") * 100, 1)}%");
            Output.WriteLine($"BloodPumping: {Math.Round(GetCapacity("BloodPumping") * 100, 1)}%");
            Output.WriteLine($"Digestion: {Math.Round(GetCapacity("Digestion") * 100, 1)}%");
            Output.WriteLine($"BloodFiltration: {Math.Round(GetCapacity("BloodFiltration") * 100, 1)}%");
            Output.WriteLine($"Eating: {Math.Round(GetCapacity("Eating") * 100, 1)}%");
            Output.WriteLine($"Talking: {Math.Round(GetCapacity("Talking") * 100, 1)}%");
            Output.WriteLine("Body Parts:");
            DescribePart(rootPart, 0);
        }

        private void DescribePart(BodyPart part, int indent)
        {
            string indentStr = new string(' ', indent * 2);
            Output.WriteLine($"{indentStr}{part.Name}: Health={Math.Round(part.Health, 1)}/{part.MaxHealth}");
            foreach (var subPart in part.Parts)
            {
                DescribePart(subPart, indent + 1);
            }
        }
    }
}