namespace text_survival.Level
{
    public enum SkillType
    {
        Blade,
        Blunt,
        HandToHand,
        Block,
        HeavyArmor,
        LightArmor,
        Athletics,
        Dodge
    }
    public class Skills
    {

        public List<Skill> All { get; set; }
        public Skill Blade { get; set; }
        public Skill Blunt { get; set; }
        public Skill HandToHand { get; set; }
        public Skill Block { get; set; }
        public Skill HeavyArmor { get; set; }
        public Skill LightArmor { get; set; }
        public Skill Athletics { get; set; }
        public Skill Dodge { get; set; }

        // todo: add Armorer, Sneak, Security, Acrobatics, Marksman, Mercantile, Speechcraft, HandToHand, Alchemy, Conjuration, Destruction, Illusion, Mysticism, Restoration, Alteration, Enchant

        public Skills()
        {
            All = new List<Skill>();
            Blade = new Skill(SkillType.Blade);
            Blunt = new Skill(SkillType.Blunt);
            HandToHand = new Skill(SkillType.HandToHand);
            Block = new Skill(SkillType.Block);
            HeavyArmor = new Skill(SkillType.HeavyArmor);
            LightArmor = new Skill(SkillType.LightArmor);
            Athletics = new Skill(SkillType.Athletics);
            Dodge = new Skill(SkillType.Dodge);
            All.Add(Blade);
            All.Add(Blunt);
            All.Add(HandToHand);
            All.Add(Block);
            All.Add(HeavyArmor);
            All.Add(LightArmor);
            All.Add(Athletics);
            All.Add(Dodge);

            EventAggregator.Subscribe<GainExperienceEvent>(OnGainedExperience);
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
                case SkillType.HandToHand:
                    HandToHand.GainExperience(e.Experience);
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

                default:
                    Utils.WriteWarning("Invalid experience gain type");
                    break;
            }
        }

    }
}
