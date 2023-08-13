using text_survival.Magic;

namespace text_survival.Items
{

    public class Weapon : Item, IEquippable
    {
        //public WeaponClass WeaponClass { get; set; }
        public WeaponClass WeaponClass { get; set; }
        public WeaponMaterial WeaponMaterial { get; set; }
        public WeaponType WeaponType { get; set; }
        public double Damage { get; set; }
        public double Accuracy { get; set; }
        public double BlockChance { get; set; }
        public Buff Buff { get; set; }

        public Weapon(WeaponType type, WeaponMaterial weaponMaterial, string name = "", int quality = 50) : base(name, quality: quality)
        {
            UseEffect = (player) =>
            {
                player.Equip(this);
            };
            SetBaseStats(type);
            ApplyMaterialModifier(weaponMaterial);
            ApplyQualityModifier();
            WeaponClass = GetDamageTypeFromWeaponType(type);
            if (Name == "")
                Name = $"{GetQualityEnumFromQuality(Quality)} {weaponMaterial} {type}";
            WeaponType = type;
            WeaponMaterial = weaponMaterial;
            Buff = new Buff(name, -1);
        }

        private void ApplyQualityModifier()
        {
            Damage *= (double)(Quality * 2) / 100;
            BlockChance *= (double)(Quality * 2) / 100;
        }
        private void ApplyMaterialModifier(WeaponMaterial weaponMaterial)
        {
            switch (weaponMaterial)
            {
                case WeaponMaterial.Wooden:
                    Damage *= .1F;
                    BlockChance *= .7;
                    //Speed *= 1.2F;
                    Weight *= .5F;
                    break;
                case WeaponMaterial.Stone:
                    Damage *= .7F;
                    BlockChance *= .6;
                    //Speed *= .1F;
                    Weight *= 1.5F;
                    break;
                case WeaponMaterial.Bronze:
                    Damage *= .9F;
                    BlockChance *= .9;
                    //Speed *= 1.1F;
                    Weight *= .9F;
                    break;
                case WeaponMaterial.Iron:
                    Damage *= 1F;
                    BlockChance *= 1;
                    //Defense *= 1F;
                    //Speed *= .7F;
                    Weight *= 1.4F;
                    break;
                case WeaponMaterial.Steel:
                    Damage *= 1F;
                    BlockChance *= 1;
                    //Defense *= 1F;
                    //Speed *= 1F;
                    Weight *= 1F;
                    break;
                case WeaponMaterial.Silver:
                    Damage *= 1.2F;
                    BlockChance *= 1.1;
                    //Defense *= 1.1F;
                    //Speed *= 1F;
                    Weight *= 1.1F;
                    break;
                case WeaponMaterial.Golden:
                    Damage *= 1.5F;
                    BlockChance *= .5;
                    //Defense *= .5F;
                    //Speed *= .5F;
                    Weight *= 1.5F;
                    break;
                case WeaponMaterial.Other:
                default:
                    break;
            }
        }
        private void SetBaseStats(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Sword:
                    Damage = 12;
                    BlockChance = .1;
                    Accuracy = 1;
                    //Defense = 4;
                    //Speed = 1;
                    Weight = 2;
                    break;
                case WeaponType.Axe:
                    Damage = 18;
                    BlockChance = .04;
                    Accuracy = .8;
                    //Defense = 0;
                    //Speed = -3;
                    Weight = 4;
                    break;
                case WeaponType.Spear:
                    Damage = 8;
                    BlockChance = .08;
                    Accuracy = 1.2;
                    //Defense = 8;
                    //Speed = -1;
                    Weight = 1;
                    break;
                case WeaponType.Mace:
                    Damage = 14;
                    BlockChance = .04;
                    Accuracy = .7;
                    //Defense = 4;
                    //Speed = -1;
                    Weight = 2;
                    break;
                case WeaponType.Hammer:
                    Damage = 20;
                    BlockChance = .1;
                    Accuracy = .5;
                    //Defense = 2;
                    //Speed = -4;
                    Weight = 5;
                    break;
                case WeaponType.Dagger:
                    Damage = 6;
                    BlockChance = .01;
                    Accuracy = 1.5;
                    //Defense = 0;
                    //Speed = 6;
                    Weight = .5F;
                    break;
                case WeaponType.Staff:
                    Damage = 4;
                    BlockChance = .14;
                    Accuracy = 1.2;
                    //Defense = 14;
                    //Speed = 2;
                    Weight = 2;
                    break;
                case WeaponType.Unarmed:
                    Damage = 2;
                    BlockChance = .2;
                    Accuracy = 1.5;
                    //Defense = 0;
                    //Speed = 3;
                    Weight = 0;
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

        private static WeaponClass GetDamageTypeFromWeaponType(WeaponType type)
        {
            return type switch
            {
                WeaponType.Sword => WeaponClass.Blade,
                WeaponType.Axe => WeaponClass.Blade,
                WeaponType.Spear => WeaponClass.Blade,
                WeaponType.Mace => WeaponClass.Blunt,
                WeaponType.Hammer => WeaponClass.Blunt,
                WeaponType.Dagger => WeaponClass.Blade,
                WeaponType.Staff => WeaponClass.Blunt,
                WeaponType.Unarmed => WeaponClass.Unarmed,
                _ => WeaponClass.Blunt,
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

        public void OnEquip(Player player)
        {
            Buff.ApplyEffect(player);
        }

        public void OnUnequip(Player player)
        {
            Buff.RemoveEffect(player);
        }
    }
}
