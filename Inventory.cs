﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_survival.Items;

namespace text_survival
{
    public class Inventory : Container
    {
        public Inventory(string name="Backpack", int weightCap = 10) : base (name, weightCap)
        {
        }

        public override void Open(Player player)
        {
            while (true)
            {
                Utils.WriteLine(this, " (", GetWeight(), "/", MaxWeight,"):");
                int index = Utils.GetSelectionFromList(Items, true) - 1;
                if (index == -1) return;
                Item item = GetItem(index);
                Utils.WriteLine("What would you like to do with ", item);
                int choice = Utils.GetSelectionFromList(new List<string>() { "Use", "Inspect", "Drop" }, true);
                switch (choice)
                {
                    case 0:
                        continue;
                    case 1:
                        item.Use(player);
                        break;
                    case 2:
                        Examine.ExamineItem(item);
                        break;
                    case 3:
                        this.Remove(item);
                        player.CurrentArea.Items.Add(item);
                        break;
                }
            }

        }
    }
}
