using text_survival.Effects;
using text_survival.Magic;

namespace text_survival.Items
{
    public class Armor : Gear
    {
        public EquipSpots EquipSpot { get; set; }
        public double Rating { get; set; }
        public ArmorClass Type { get; set; }

        public Armor(string name, double rating, EquipSpots equipSpot, double insulation = 0, ArmorClass type = ArmorClass.Light) : base(name)
        {
            EquipEffects = [];
            Rating = rating;
            EquipSpot = equipSpot;
            Insulation = insulation;
            Type = type;
        }


    }
}