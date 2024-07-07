namespace text_survival.Actors
{
    public class NpcSpawner
    {
        private List<Npc> Npcs { get; set; }

        public NpcSpawner()
        {
            Npcs = [];
        }

        public void Add(Npc npc)
        {
            Npcs.Add(npc);
        }

        public void Remove(Npc npc)
        {
            Npcs.Remove(npc);
        }

        public Npc? GenerateRandomNpc()
        {
            if (Npcs.Count == 0)
            {
                return new Npc("Ghost");
            }
            Npc? npc = Utils.GetRandomFromList(Npcs);
            return npc?.Clone() ?? null;
        }

        public bool IsEmpty()
        {
            return Npcs.Count == 0;
        }

        public int Count()
        {
            return Npcs.Count;
        }
    }
}