﻿using text_survival.Actors;
using text_survival.Magic;

namespace text_survival.Items
{
    public class Gear : Item, IEquippable
    {
        private List<Buff> EquipBuffs { get; }
        public List<Buff> GetEquipBuffs() => EquipBuffs;
        public void AddEquipBuff(Buff buff) => EquipBuffs.Add(buff);
        public void RemoveEquipBuff(Buff buff) => EquipBuffs.Remove(buff);

        public Gear(string name, double weight = 1) : base(name, weight)
        {
            EquipBuffs = new List<Buff>();
            UseEffect = (player) =>
            {
                player.Equip(this);
            };
        }
    }

}

