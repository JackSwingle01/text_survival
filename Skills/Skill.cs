using text_survival.IO;
using text_survival.UI;

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
            GameDisplay.AddNarrative($"You leveled up {this} to level {Level}!");
        }

    }
}
