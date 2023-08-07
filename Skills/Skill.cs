using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival.Skills
{
    public class Skill
    {
        private int _points;
        private int _level;
        public string Name { get; set; }

        public Skill(string name) {
            Name = name;
            _points = 0;
            _level = 0;
        }

        private int LevelUpThreshold => (_level + 1) * 10;

        public void AddPoints(int points)
        {
            _points += points;

            if (_points < LevelUpThreshold) return;
            // else level up
            _points -= LevelUpThreshold;
            LevelUp();
        }

        public void LevelUp()
        {
            _level++;
            EventAggregator.Publish(new SkillLevelUpEvent(this));
            Utils.WriteLine("You leveled up " + Name + " to level " + _level + "!");
        }


    }
}
