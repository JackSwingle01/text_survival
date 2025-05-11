namespace text_survival.Actors
{
    public class NpcSpawner
    {
        private List<Func<Npc>> _factories { get; set; }

        public NpcSpawner()
        {
            _factories = [];
        }

        public NpcSpawner(List<Func<Npc>> factories)
        {
            _factories = factories;
        }

        public void Add(Func<Npc> factory)
        {
            _factories.Add(factory);
        }

        public Npc? GenerateRandomNpc()
        {
            if (_factories.Count == 0)
            {
                return new Npc("Ghost");
            }
            var fac = Utils.GetRandomFromList(_factories);
            return fac();
        }
    }
}