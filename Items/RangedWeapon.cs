namespace text_survival.Items
{
    /// <summary>
    /// Represents a ranged weapon (bow) that requires ammunition.
    /// Extends Weapon with range and ammunition type properties.
    /// </summary>
    public class RangedWeapon : Weapon
    {
        /// <summary>
        /// Optimal effective range in meters (30-50m for bows).
        /// </summary>
        public double EffectiveRange { get; set; }

        /// <summary>
        /// Maximum range in meters (70m for bows).
        /// </summary>
        public double MaxRange { get; set; }

        /// <summary>
        /// Type of ammunition this weapon requires (e.g., "Arrow").
        /// </summary>
        public string AmmunitionType { get; set; }

        /// <summary>
        /// Base accuracy multiplier for ranged attacks (before distance/skill modifiers).
        /// </summary>
        public double BaseAccuracy { get; set; }

        public RangedWeapon(
            WeaponType type,
            WeaponMaterial material,
            string ammunitionType,
            double effectiveRange,
            double maxRange,
            string name = "",
            int craftsmanship = 50)
            : base(type, material, name, craftsmanship)
        {
            AmmunitionType = ammunitionType;
            EffectiveRange = effectiveRange;
            MaxRange = maxRange;
            BaseAccuracy = Accuracy; // Store the weapon's base accuracy
        }

        /// <summary>
        /// Creates a Simple Bow (basic hunting bow).
        /// </summary>
        public static RangedWeapon CreateSimpleBow(int craftsmanship = 50)
        {
            var bow = new RangedWeapon(
                WeaponType.Bow,
                WeaponMaterial.Wood,
                ammunitionType: "Arrow",
                effectiveRange: 40.0,  // 30-50m optimal
                maxRange: 70.0,        // Beyond 70m = very low accuracy
                name: "",
                craftsmanship: craftsmanship
            );

            // Override base stats for bow
            bow.Damage = 12;  // Higher damage than melee (lethal shots possible)
            bow.Accuracy = 0.7; // Base 70% accuracy at optimal range
            bow.BlockChance = 0.0; // Can't block with a bow
            bow.Weight = 1.0;
            bow.BaseAccuracy = 0.7;

            // Set proper name based on craftsmanship
            string qualityDesc = craftsmanship switch
            {
                < 20 => "Primitive",
                < 40 => "Rough",
                < 60 => "Simple",
                < 80 => "Sturdy",
                < 95 => "Master Crafted",
                _ => "Flawless"
            };
            bow.Name = $"{qualityDesc} Wooden Bow";

            return bow;
        }

        public override string ToString()
        {
            return $"{Name} (Range: {EffectiveRange:F0}m, Ammo: {AmmunitionType})";
        }
    }
}
