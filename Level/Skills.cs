namespace text_survival.Level
{
    public enum SkillType
    {
        Strength,
        Defense,
        Speed
    }
    public class Skills
    {

        public List<Skill> All { get; set; }
        public Skill Strength { get; set; }
        public Skill Defense { get; set; }
        public Skill Speed { get; set; }

        public Skills()
        {
            All = new List<Skill>();
            Strength = new Skill(SkillType.Strength);
            Defense = new Skill(SkillType.Defense);
            Speed = new Skill(SkillType.Speed);
            EventAggregator.Subscribe<GainExperienceEvent>(OnGainedExperience);
        }

        private void OnGainedExperience(GainExperienceEvent e)
        {
            switch (e.Type)
            {
                case SkillType.Strength:
                    Strength.AddPoints(e.Experience);
                    break;
                case SkillType.Defense:
                    Defense.AddPoints(e.Experience);
                    break;
                case SkillType.Speed:
                    Speed.AddPoints(e.Experience);
                    break;
                default:
                    Utils.WriteWarning("Invalid experience gain type");
                    break;
            }
            Utils.WriteLine("gained xp!");
        }

    }
}
