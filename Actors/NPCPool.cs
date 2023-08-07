namespace text_survival.Actors
{
    public class NpcPool
    {
        private List<Func<Npc>> Npcs { get; set; }

        public NpcPool()
        {
            Npcs = new List<Func<Npc>>();
        }

        public void Add(Func<Npc> npcFactoryMethod)
        {
            Npcs.Add(npcFactoryMethod);
        }

        public void Remove(Func<Npc> npcFactoryMethod)
        {
            Npcs.Remove(npcFactoryMethod);
        }

        public Npc GenerateRandomNpc()
        {
            if (Npcs.Count == 0)
            {
                return new Npc("Ghost", 1, 1, 1, 1);
            }
            int index = Utils.Rand(0, Npcs.Count - 1);
            return Npcs[index].Invoke();
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