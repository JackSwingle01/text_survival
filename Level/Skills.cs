using text_survival.IO;

namespace text_survival.Level
{
    public enum SkillType
    {
        Blade,
        Blunt,
        Unarmed,
        Block,
        HeavyArmor,
        LightArmor,
        Athletics,
        Dodge,
        Destruction,
        Restoration,
    }
    public class Skills
    {

        public List<Skill> All { get; set; }
        public Skill Blade { get; set; }
        public Skill Blunt { get; set; }
        public Skill Unarmed { get; set; }
        public Skill Block { get; set; }
        public Skill HeavyArmor { get; set; }
        public Skill LightArmor { get; set; }
        public Skill Athletics { get; set; }
        public Skill Dodge { get; set; }
        public Skill Destruction { get; set; }
        public Skill Restoration { get; set; }


        // todo: add Armorer, Sneak, Security, Acrobatics, Marksman, Mercantile, Speech, Alchemy, Conjuration, Destruction, Illusion, Mysticism, Restoration, Alteration, Enchant

        public Skills()
        {
            All = new List<Skill>();
            Blade = new Skill(SkillType.Blade);
            Blunt = new Skill(SkillType.Blunt);
            Unarmed = new Skill(SkillType.Unarmed);
            Block = new Skill(SkillType.Block);
            HeavyArmor = new Skill(SkillType.HeavyArmor);
            LightArmor = new Skill(SkillType.LightArmor);
            Athletics = new Skill(SkillType.Athletics);
            Dodge = new Skill(SkillType.Dodge);
            Destruction = new Skill(SkillType.Destruction);
            Restoration = new Skill(SkillType.Restoration);

            All.Add(Blade);
            All.Add(Blunt);
            All.Add(Unarmed);
            All.Add(Block);
            All.Add(HeavyArmor);
            All.Add(LightArmor);
            All.Add(Athletics);
            All.Add(Dodge);
            All.Add(Destruction);
            All.Add(Restoration);

            EventHandler.Subscribe<GainExperienceEvent>(OnGainedExperience);
        }

        private void OnGainedExperience(GainExperienceEvent e)
        {
            switch (e.Type)
            {
                case SkillType.Blade:
                    Blade.GainExperience(e.Experience);
                    break;
                case SkillType.Blunt:
                    Blunt.GainExperience(e.Experience);
                    break;
                case SkillType.Unarmed:
                    Unarmed.GainExperience(e.Experience);
                    break;
                case SkillType.Block:
                    Block.GainExperience(e.Experience);
                    break;
                case SkillType.HeavyArmor:
                    HeavyArmor.GainExperience(e.Experience);
                    break;
                case SkillType.LightArmor:
                    LightArmor.GainExperience(e.Experience);
                    break;
                case SkillType.Athletics:
                    Athletics.GainExperience(e.Experience);
                    break;
                case SkillType.Dodge:
                    Dodge.GainExperience(e.Experience);
                    break;
                case SkillType.Destruction:
                    Destruction.GainExperience(e.Experience);
                    break;
                case SkillType.Restoration:
                    Restoration.GainExperience(e.Experience);
                    break;
                default:
                    Output.WriteWarning("Invalid experience gain type");
                    break;
            }
        }

    }
}
