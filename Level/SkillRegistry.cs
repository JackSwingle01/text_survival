using text_survival.IO;

namespace text_survival.Level
{
    public class SkillRegistry
    {
        public Skill Fighting { get; private set; }
        public Skill Endurance { get; private set; }
        public Skill Reflexes { get; private set; }
        public Skill Defense { get; private set; }
        public Skill Hunting { get; private set; }
        public Skill Toolmaking { get; private set; }
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
            Toolmaking = new Skill("Toolmaking");
            Foraging = new Skill("Foraging");
            Firecraft = new Skill("Firecraft");
            Mending = new Skill("Mending");
            Healing = new Skill("Healing");
            Magic = new Skill("Shamanism");
        }

        public void Describe()
        {
            Output.WriteLine("\nSkills:");

            var allSkills = new[] { Fighting, Endurance, Reflexes, Defense, Hunting, Toolmaking, Foraging, Firecraft, Mending, Healing, Magic };

            foreach (var skill in allSkills)
            {
                if (skill.Level > 0)
                {
                    Output.WriteLine($"{skill.Name}: {skill.Level} ({skill.Xp}/{skill.LevelUpThreshold})");
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
                "Toolmaking" => Toolmaking,
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