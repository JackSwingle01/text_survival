using text_survival.IO;
using text_survival.UI;

namespace text_survival.Skills
{
    public class SkillRegistry
    {
        public Skill Fighting { get; private set; }
        public Skill Endurance { get; private set; }
        public Skill Reflexes { get; private set; }
        public Skill Defense { get; private set; }
        public Skill Hunting { get; private set; }
        public Skill Crafting { get; private set; }
        public Skill Foraging { get; private set; }
        public Skill Firecraft { get; private set; }
        public Skill Mending { get; private set; }
        public Skill Healing { get; private set; }
        public Skill Magic { get; private set; }

        public SkillRegistry()
        {
            Fighting = new Skill("Fighting");
            Endurance = new Skill("Endurance");
            Reflexes = new Skill("Reflexes");
            Defense = new Skill("Defense");
            Hunting = new Skill("Hunting");
            Crafting = new Skill("Crafting");
            Foraging = new Skill("Foraging");
            Firecraft = new Skill("Firecraft");
            Mending = new Skill("Mending");
            Healing = new Skill("Healing");
            Magic = new Skill("Shamanism");
        }

        public void Describe()
        {
            GameDisplay.AddNarrative("\nSkills:");

            var allSkills = new[] { Fighting, Endurance, Reflexes, Defense, Hunting, Crafting, Foraging, Firecraft, Mending, Healing, Magic };

            foreach (var skill in allSkills)
            {
                if (skill.Level > 0)
                {
                    GameDisplay.AddNarrative($"{skill.Name}: {skill.Level} ({skill.Xp}/{skill.LevelUpThreshold})");
                }
            }
        }

        public Skill GetSkill(string skillName)
        {
            return skillName switch
            {
                "Fighting" => Fighting,
                "Endurance" => Endurance,
                "Reflexes" => Reflexes,
                "Defense" => Defense,
                "Hunting" => Hunting,
                "Crafting" => Crafting,
                "Foraging" => Foraging,
                "Firecraft" => Firecraft,
                "Mending" => Mending,
                "Healing" => Healing,
                "Magic" => Magic,
                _ => throw new ArgumentException($"Skill {skillName} does not exist.")
            };
        }
    }
}