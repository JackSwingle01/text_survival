namespace text_survival.Items
{
    public enum WeaponType
    {
        Sword,
        Axe,
        Spear,
        Mace,
        Hammer,
        Dagger,
        Staff,
    }
    //public enum WeaponClass
    //{
    //    Light,
    //    Heavy,
    //}

    public enum DamageType
    {
        Blunt,
        Slashing,
        Piercing,
    }

    public enum Material
    {
        Wooden,
        Stone,
        Bronze,
        Iron,
        Steel,
        Silver,
        Golden
    }

    public enum QualityEnum
    {
        Unknown,
        Trash,
        Broken,
        Worn,
        Poor,
        Fair,
        Average,
        Decent,
        Good,
        Fine,
        Excellent,
        Perfect
    }

    public class Weapon : EquipableItem
    {
        //public WeaponClass WeaponClass { get; set; }
        public DamageType DamageType { get; set; }
        public Material Material { get; set; }
        public WeaponType WeaponType { get; set; }
        public int Damage { get; set; }

        
        public Weapon(WeaponType type, Material material) : base("default")
        {
            SetBaseStats(type);
            ApplyMaterialModifier(material);
            ApplyQualityModifier();
            DamageType = GetDamageTypeFromWeaponType(type);
            Name = $"{GetQualityEnumFromQuality(Quality)} {material} {type}";
            WeaponType = type;
            Material = material;
            EquipSpot = EquipSpots.Weapon;
        }
        
        private void ApplyQualityModifier()
        {
            Strength *= (((float)Quality) / 100F);
            Defense *= (((float)Quality) / 100F);
            Speed *= (((float)Quality) / 100F);
        }
        private void ApplyMaterialModifier(Material material)
        {
            switch (material)
            {
                case Material.Wooden:
                    Strength *= .1F;
                    Defense *= .7F;
                    Speed *= 1.2F;
                    Weight *= .5F;
                    break;
                case Material.Stone:
                    Strength *= .7F;
                    Defense *= .6F;
                    Speed *= .1F;
                    Weight *= 1.5F;
                    break;
                case Material.Bronze:
                    Strength *= .9F;
                    Defense *= .9F;
                    Speed *= 1.1F;
                    Weight *= .9F;
                    break;
                case Material.Iron:
                    Strength *= 1F;
                    Defense *= 1F;
                    Speed *= .7F;
                    Weight *= 1.4F;
                    break;
                case Material.Steel:
                    Strength *= 1F;
                    Defense *= 1F;
                    Speed *= 1F;
                    Weight *= 1F;
                    break;
                case Material.Silver:
                    Strength *= 1.2F;
                    Defense *= 1.1F;
                    Speed *= 1F;
                    Weight *= 1.1F;
                    break;
                case Material.Golden:
                    Strength *= 1.5F;
                    Defense *= .5F;
                    Speed *= .5F;
                    Weight *= 1.5F;
                    break;
                default:
                    break;
            }
        }
        private void SetBaseStats(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Sword:
                    Strength = 12;
                    Defense = 4;
                    Speed = 1;
                    break;
                case WeaponType.Axe:
                    Strength = 18;
                    Defense = 0;
                    Speed = -3;
                    break;
                case WeaponType.Spear:
                    Strength = 8;
                    Defense = 8;
                    Speed = -1;
                    break;
                case WeaponType.Mace:
                    Strength = 14;
                    Defense = 4;
                    Speed = -1;
                    break;
                case WeaponType.Hammer:
                    Strength = 20;
                    Defense = 2;
                    Speed = -4;
                    break;
                case WeaponType.Dagger:
                    Strength = 6;
                    Defense = 0;
                    Speed = 6;
                    break;
                case WeaponType.Staff:
                    Strength = 4;
                    Defense = 14;
                    Speed = 2;
                    break;
                default:
                    break;
            }
        }
        public static Weapon GenerateRandomWeapon()
        {
            Material material = Utils.GetRandomEnum<Material>();
            WeaponType weaponType = Utils.GetRandomEnum<WeaponType>();
            Weapon weapon = new Weapon(weaponType, material);
            return weapon;
        }

        private DamageType GetDamageTypeFromWeaponType(WeaponType type)
        {
            return type switch
            {
                WeaponType.Sword => DamageType.Slashing,
                WeaponType.Axe => DamageType.Slashing,
                WeaponType.Spear => DamageType.Piercing,
                WeaponType.Mace => DamageType.Blunt,
                WeaponType.Hammer => DamageType.Blunt,
                WeaponType.Dagger => DamageType.Piercing,
                WeaponType.Staff => DamageType.Blunt,
                _ => DamageType.Blunt,
            };
        }

        public static QualityEnum GetQualityEnumFromQuality(int quality)
        {
            return quality switch
            {
                0 => QualityEnum.Trash,
                < 10 => QualityEnum.Broken,
                < 20 => QualityEnum.Worn,
                < 30 => QualityEnum.Poor,
                < 40 => QualityEnum.Fair,
                < 60 => QualityEnum.Average,
                < 70 => QualityEnum.Decent,
                < 80 => QualityEnum.Good,
                < 90 => QualityEnum.Fine,
                < 99 => QualityEnum.Excellent,
                100 => QualityEnum.Perfect,
                _ => QualityEnum.Unknown
            };
        }
    }
}
