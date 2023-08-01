namespace text_survival.Actors
{
    public class NpcPool
    {
        private List<Func<Npc>> NPCs { get; set; }

        public NpcPool()
        {
            NPCs = new List<Func<Npc>>();
        }

        public void Add(Func<Npc> npcFactoryMethod)
        {
            NPCs.Add(npcFactoryMethod);
        }

        public void Remove(Func<Npc> npcFactoryMethod)
        {
            NPCs.Remove(npcFactoryMethod);
        }

        public Npc GetRandomNpc()
        {
            if (NPCs.Count == 0)
            {
                return new Npc("Ghost", 1, 1, 1, 1);
            }
            int index = Utils.Rand(0, NPCs.Count - 1);
            return NPCs[index].Invoke();
        }

        public bool IsEmpty()
        {
            return NPCs.Count == 0;
        }

        public int Count()
        {
            return NPCs.Count;
        }
    }
}