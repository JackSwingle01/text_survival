

namespace text_survival.Level
{
    public class Skill
    {
        private int _xp;
        public int Level { get; private set; }
        public SkillType Type { get; set; }

        public Skill(SkillType type)
        {
            Type = type;
            _xp = 0;
            Level = 0;
        }

        private int LevelUpThreshold => (Level + 1) * 10;

        public void GainExperience(int xp)
        {
            _xp += xp;

            if (_xp < LevelUpThreshold) return;
            // else level up
            _xp -= LevelUpThreshold;
            LevelUp();
        }

        public void LevelUp()
        {
            Level++;
            EventAggregator.Publish(new SkillLevelUpEvent(this));
            Utils.WriteLine("You leveled up ", this, " to level ", Level, "!");
        }

        public override string ToString()
        {
            return Type.ToString();
        }

        public void Write()
        {
            Utils.Write(this, ": ", Level, " (", _xp, "/", LevelUpThreshold, ")");
        }



    }
}
