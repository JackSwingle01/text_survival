namespace text_survival.Items
{

    public class Weapon : EquipableItem
    {
        //public WeaponClass WeaponClass { get; set; }
        public DamageType DamageType { get; set; }
        public WeaponMaterial WeaponMaterial { get; set; }
        public WeaponType WeaponType { get; set; }
        public int Damage { get; set; }


        public Weapon(WeaponType type, WeaponMaterial weaponMaterial) : base("default")
        {
            SetBaseStats(type);
            ApplyMaterialModifier(weaponMaterial);
            ApplyQualityModifier();
            DamageType = GetDamageTypeFromWeaponType(type);
            Name = $"{GetQualityEnumFromQuality(Quality)} {weaponMaterial} {type}";
            WeaponType = type;
            WeaponMaterial = weaponMaterial;
            EquipSpot = EquipSpots.Weapon;
        }

        private void ApplyQualityModifier()
        {
            Strength *= (((float)Quality) / 100F);
            Defense *= (((float)Quality) / 100F);
            Speed *= (((float)Quality) / 100F);
        }
        private void ApplyMaterialModifier(WeaponMaterial weaponMaterial)
        {
            switch (weaponMaterial)
            {
                case WeaponMaterial.Wooden:
                    Strength *= .1F;
                    Defense *= .7F;
                    Speed *= 1.2F;
                    Weight *= .5F;
                    break;
                case WeaponMaterial.Stone:
                    Strength *= .7F;
                    Defense *= .6F;
                    Speed *= .1F;
                    Weight *= 1.5F;
                    break;
                case WeaponMaterial.Bronze:
                    Strength *= .9F;
                    Defense *= .9F;
                    Speed *= 1.1F;
                    Weight *= .9F;
                    break;
                case WeaponMaterial.Iron:
                    Strength *= 1F;
                    Defense *= 1F;
                    Speed *= .7F;
                    Weight *= 1.4F;
                    break;
                case WeaponMaterial.Steel:
                    Strength *= 1F;
                    Defense *= 1F;
                    Speed *= 1F;
                    Weight *= 1F;
                    break;
                case WeaponMaterial.Silver:
                    Strength *= 1.2F;
                    Defense *= 1.1F;
                    Speed *= 1F;
                    Weight *= 1.1F;
                    break;
                case WeaponMaterial.Golden:
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
                    Weight = 2;
                    break;
                case WeaponType.Axe:
                    Strength = 18;
                    Defense = 0;
                    Speed = -3;
                    Weight = 4;
                    break;
                case WeaponType.Spear:
                    Strength = 8;
                    Defense = 8;
                    Speed = -1;
                    Weight = 1;
                    break;
                case WeaponType.Mace:
                    Strength = 14;
                    Defense = 4;
                    Speed = -1;
                    Weight = 2;
                    break;
                case WeaponType.Hammer:
                    Strength = 20;
                    Defense = 2;
                    Speed = -4;
                    Weight = 5;
                    break;
                case WeaponType.Dagger:
                    Strength = 6;
                    Defense = 0;
                    Speed = 6;
                    Weight = .5F;
                    break;
                case WeaponType.Staff:
                    Strength = 4;
                    Defense = 14;
                    Speed = 2;
                    Weight = 2;
                    break;
                default:
                    break;
            }
        }
        public static Weapon GenerateRandomWeapon()
        {
            WeaponMaterial weaponMaterial = Utils.GetRandomEnum<WeaponMaterial>();
            WeaponType weaponType = Utils.GetRandomEnum<WeaponType>();
            Weapon weapon = new Weapon(weaponType, weaponMaterial);
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
