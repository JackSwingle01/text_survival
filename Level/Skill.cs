

using text_survival.IO;

namespace text_survival.Level
{
    public class Skill
    {
        public int Xp;
        public int Level { get; private set; }
        public string Name { get; set; }
        public int LevelUpThreshold => (Level) * 10;

        public Skill(string name)
        {
            Name = name;
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
            Output.WriteLine("You leveled up ", this, " to level ", Level, "!");
        }

        public override string ToString() => Name;

        public void Describe()
        {
            Output.Write(this, ": ", Level, " (", Xp, "/", LevelUpThreshold, ")");
        }

    }
}
