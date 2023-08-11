

namespace text_survival.Level
{
    public class Skill
    {
        public int Xp;
        public int Level { get; private set; }
        public SkillType Type { get; set; }
        public int LevelUpThreshold => (Level) * 10;

        public Skill(SkillType type)
        {
            Type = type;
            Xp = 0;
            Level = 0;
        }


        public void GainExperience(int xp)
        {
            Xp += xp;

            if (Xp < LevelUpThreshold) return;
            // else level up
            Xp -= LevelUpThreshold;
            LevelUp();
        }

        public void LevelUp()
        {
            Level++;
            EventHandler.Publish(new SkillLevelUpEvent(this));
            Utils.WriteLine("You leveled up ", this, " to level ", Level, "!");
        }

        public override string ToString()
        {
            return Type.ToString();
        }

        public void Write()
        {
            Utils.Write(this, ": ", Level, " (", Xp, "/", LevelUpThreshold, ")");
        }



    }
}
