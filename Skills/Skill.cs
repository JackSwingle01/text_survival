

using text_survival.IO;

namespace text_survival.Skills
{
    public class Skill(string name)
    {
        public int Xp = 0;
        public int Level { get; private set; } = 0;
        public string Name { get; set; } = name;
        public int LevelUpThreshold => (Level) * 10;

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
