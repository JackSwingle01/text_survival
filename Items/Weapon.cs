namespace text_survival.Items
{
    public enum WeaponType
    {
        // Crafted weapons
        Spear,          // Long distance hunting/combat
        Club,           // Heavy blunt force
        HandAxe,        // Cutting tool and weapon
        Knife,          // Sharp tool made of flint or similar
        SharpStone,     // Primitive cutting tool

        // Natural weapons
        Unarmed,        // Human fists
        Claws,          // Bear, large feline
        Fangs,          // Wolf, snake
        Horns           // Mammoth tusks, deer antlers
    }

    public enum WeaponClass
    {
        Blade,          // Cutting damage
        Blunt,          // Impact damage
        Pierce,         // Stabbing damage
        Claw,           // Tearing damage
        Unarmed         // Basic damage
    }

    public enum WeaponMaterial
    {
        Wood,           // Sticks, branches
        Stone,          // Basic stone
        Bone,           // Animal bones
        Antler,         // Deer/elk antlers
        Flint,          // Knapped flint
        Obsidian,       // Volcanic glass
        Organic,        // Natural animal weapons
        Other           // Miscellaneous
    }
    public class Weapon : Gear
    {
        public WeaponClass Class { get; set; }
        public WeaponMaterial Material { get; set; }
        public WeaponType Type { get; set; }
        public double Damage { get; set; }
        public double Accuracy { get; set; }
        public double BlockChance { get; set; }
        public double Craftsmanship { get; set; }

        public Weapon(WeaponType type, WeaponMaterial material, string name = "", int craftsmanship = 50)
            : base(name, quality: craftsmanship)
        {
            Craftsmanship = craftsmanship;
            SetBaseStats(type);
            ApplyMaterialModifier(material);
            ApplyCraftsmanshipModifier();
            Class = GetDamageTypeFromWeaponType(type);

            if (string.IsNullOrWhiteSpace(name))
                Name = $"{GetCraftsmanshipDescription(Craftsmanship)} {GetMaterialDescription(material)} {GetWeaponTypeDescription(type)}";

            Type = type;
            Material = material;
            EquipEffects = [];
        }

        private void ApplyCraftsmanshipModifier()
        {
            // The better crafted the weapon, the more effective it is
            Damage *= (Craftsmanship * 1.5) / 100;
            BlockChance *= Craftsmanship / 100;
        }

        private void ApplyMaterialModifier(WeaponMaterial material)
        {
            switch (material)
            {
                case WeaponMaterial.Wood:
                    Damage *= 0.6;
                    BlockChance *= 0.7;
                    Weight *= 0.5;
                    break;
                case WeaponMaterial.Stone:
                    Damage *= 1.0;
                    BlockChance *= 0.6;
                    Weight *= 1.2;
                    break;
                case WeaponMaterial.Bone:
                    Damage *= 0.8;
                    BlockChance *= 0.8;
                    Weight *= 0.7;
                    break;
                case WeaponMaterial.Antler:
                    Damage *= 0.9;
                    BlockChance *= 0.7;
                    Weight *= 0.8;
                    break;
                case WeaponMaterial.Flint:
                    Damage *= 1.2;
                    BlockChance *= 0.5;
                    Weight *= 1.0;
                    break;
                case WeaponMaterial.Obsidian:
                    Damage *= 1.4;
                    BlockChance *= 0.4;
                    Weight *= 0.9;
                    break;
                case WeaponMaterial.Organic:
                case WeaponMaterial.Other:
                default:
                    // No modifiers for natural/organic materials
                    break;
            }
        }

        private void SetBaseStats(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Spear:
                    Damage = 8;
                    BlockChance = 0.12;
                    Accuracy = 1.2;
                    Weight = 1.5;
                    break;
                case WeaponType.Club:
                    Damage = 10;
                    BlockChance = 0.08;
                    Accuracy = 0.9;
                    Weight = 2.0;
                    break;
                case WeaponType.HandAxe:
                    Damage = 12;
                    BlockChance = 0.05;
                    Accuracy = 0.8;
                    Weight = 1.8;
                    break;
                case WeaponType.Knife:
                    Damage = 6;
                    BlockChance = 0.02;
                    Accuracy = 1.4;
                    Weight = 0.5;
                    break;
                case WeaponType.SharpStone:
                    Damage = 4;
                    BlockChance = 0.01;
                    Accuracy = 1.1;
                    Weight = 0.3;
                    break;
                case WeaponType.Unarmed:
                    Damage = 2;
                    BlockChance = 0.01;
                    Accuracy = 1.5;
                    Weight = 0;
                    break;
                default:
                    Damage = 2;
                    BlockChance = 0.01;
                    Accuracy = 1.0;
                    Weight = 0.5;
                    break;
            }
        }

        public static Weapon GenerateRandomWeapon()
        {
            // Filter out unarmed and natural weapons when generating random weapons
            var validTypes = Enum.GetValues(typeof(WeaponType))
                .Cast<WeaponType>()
                .Where(t => t != WeaponType.Unarmed &&
                            t != WeaponType.Claws &&
                            t != WeaponType.Fangs &&
                            t != WeaponType.Horns)
                .ToArray();

            // Filter out organic and other from random generation
            var validMaterials = Enum.GetValues(typeof(WeaponMaterial))
                .Cast<WeaponMaterial>()
                .Where(m => m != WeaponMaterial.Organic && m != WeaponMaterial.Other)
                .ToArray();

            WeaponMaterial material = validMaterials[Utils.RandInt(0, validMaterials.Length - 1)];
            WeaponType type = validTypes[Utils.RandInt(0, validTypes.Length - 1)];

            int craftsmanship = Utils.RandInt(30, 80); // Primitive technology has limited upper quality

            return new Weapon(type, material, craftsmanship: craftsmanship);
        }

        private static WeaponClass GetDamageTypeFromWeaponType(WeaponType type)
        {
            return type switch
            {
                WeaponType.Knife => WeaponClass.Blade,
                WeaponType.SharpStone => WeaponClass.Blade,
                WeaponType.HandAxe => WeaponClass.Blade,
                WeaponType.Spear => WeaponClass.Pierce,
                WeaponType.Club => WeaponClass.Blunt,
                WeaponType.Unarmed => WeaponClass.Unarmed,
                WeaponType.Claws => WeaponClass.Claw,
                WeaponType.Fangs => WeaponClass.Pierce,
                WeaponType.Horns => WeaponClass.Pierce,
                _ => WeaponClass.Blunt,
            };
        }

        private string GetCraftsmanshipDescription(double craftsmanship)
        {
            return craftsmanship switch
            {
                0 => "Broken",
                < 20 => "Primitive",
                < 40 => "Rough",
                < 60 => "Simple",
                < 80 => "Sturdy",
                < 95 => "Master Crafted",
                <= 100 => "Flawless",
                _ => "Strange"
            };
        }

        private string GetMaterialDescription(WeaponMaterial material)
        {
            return material switch
            {
                WeaponMaterial.Wood => "Wooden",
                WeaponMaterial.Stone => "Stone",
                WeaponMaterial.Bone => "Bone",
                WeaponMaterial.Antler => "Antler",
                WeaponMaterial.Flint => "Flint",
                WeaponMaterial.Obsidian => "Obsidian",
                WeaponMaterial.Organic => "",
                WeaponMaterial.Other => "",
                _ => ""
            };
        }

        private string GetWeaponTypeDescription(WeaponType type)
        {
            return type switch
            {
                WeaponType.Spear => "Spear",
                WeaponType.Club => "Club",
                WeaponType.HandAxe => "Hand Axe",
                WeaponType.Knife => "Knife",
                WeaponType.SharpStone => "Sharp Stone",
                WeaponType.Unarmed => "Fists",
                WeaponType.Claws => "Claws",
                WeaponType.Fangs => "Fangs",
                WeaponType.Horns => "Horns",
                _ => "Tool"
            };
        }
    }
}