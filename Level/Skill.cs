

namespace text_survival.Level
{
    public class Skill
    {
        private int _points;
        private int _level;
        public SkillType Type { get; set; }

        public Skill(SkillType type)
        {
            Type = type;
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
            Utils.WriteLine("You leveled up ", this, " to level ", _level, "!");
        }

        public override string ToString()
        {
            return Type.ToString();
        }

        public void Write()
        {
            Utils.Write(this, ": ", _level, " (", _points, "/", LevelUpThreshold, ")");
        }



    }
}
