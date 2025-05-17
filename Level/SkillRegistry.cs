
using text_survival.IO;

namespace text_survival.Level
{
    public class SkillRegistry
    {
        private readonly Dictionary<string, Skill> skills;

        public SkillRegistry(bool fullSkills = true)
        {
            skills = [];
            if (fullSkills)
            {
                skills.Add("Fighting", new Skill("Fighting"));
                skills.Add("Endurance", new Skill("Endurance"));
                skills.Add("Agility", new Skill("Reflexes"));
                skills.Add("Defense", new Skill("Defense"));
                
                skills.Add("Hunting", new Skill("Hunting"));
                skills.Add("Toolmaking", new Skill("Toolmaking"));
                skills.Add("Foraging", new Skill("Foraging"));
                skills.Add("Firecraft", new Skill("Firecraft"));
                skills.Add("Mending", new Skill("Mending"));
                skills.Add("Healing", new Skill("Healing"));
                skills.Add("Shamanism", new Skill("Shamanism"));
            }
            else
            {
                skills.Add("Melee", new Skill("Fighting"));
                skills.Add("Endurance", new Skill("Endurance"));
                skills.Add("Agility", new Skill("Reflexes"));
                skills.Add("Defense", new Skill("Defense"));
            }
        }

        public void AddExperience(string skillName, int xp)
        {
            if (skills.ContainsKey(skillName))
                skills[skillName].GainExperience(xp);
        }

        public int GetLevel(string skillName) => skills.TryGetValue(skillName, out Skill? value) ? value.Level : 1;

        public Skill? GetSkill(string skillName) => skills.TryGetValue(skillName, out Skill? value) ? value : null;

        public void Describe()
        {
            Output.WriteLine("Skills:");
            foreach (var skill in skills.Values)
            {
                Output.WriteLine($"{skill.Name}: {skill.Level} ({skill.Xp}/{skill.LevelUpThreshold})");
            }
        }
    }
}