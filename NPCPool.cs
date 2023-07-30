namespace text_survival
{
    public class NPCPool
    {
        private List<NPC> NPCs { get; set; }
        private List<NPC> Graveyard { get; set; }
        public NPCPool()
        {
            NPCs = new List<NPC>();
            Graveyard = new List<NPC>();
        }
        public NPCPool(List<NPC> npcs)
        {
            NPCs = npcs;
            Graveyard = new List<NPC>();
        }
        public void Add(NPC npc)
        {
            NPCs.Add(npc);
        }
        public void Add(List<NPC> npcs)
        {
            NPCs.AddRange(npcs);
        }
        public void Remove(NPC npc)
        {
            NPCs.Remove(npc);
        }
        public void RemoveAt(int index)
        {
            NPCs.RemoveAt(index);
        }
        public NPC GetNPC(int index)
        {
            return NPCs[index];
        }
        public NPC? GetNPC(string name)
        {
            foreach (NPC npc in NPCs)
            {
                if (npc.Name == name)
                {
                    return npc;
                }
            }
            return null;
        }

        public void Print()
        {
            foreach (NPC npc in NPCs)
            {
                Utils.Write(npc.ToString());
            }
        }
        public void Print(int index)
        {
            Utils.Write(NPCs[index].ToString());
        }
        public NPC GetRandomNPC()
        {
            if (NPCs.Count == 0)
            {
                Refresh();
                return new NPC("Ghost", 1, 1, 1, 1);
            }
            int index = Utils.Rand(0,NPCs.Count-1);
            return NPCs[index];
        }
        public List<NPC>.Enumerator GetEnumerator()
        {
            return NPCs.GetEnumerator();
        }
        public void Refresh()
        {
            foreach (NPC npc in Graveyard)
            {
                NPCs.Add(npc);
            }
            Graveyard.Clear();
            foreach (NPC npc in NPCs)
            {
                npc.Health = npc.MaxHealth;
            }
        }
        public void Kill(NPC npc)
        {
            Graveyard.Add(npc);
            NPCs.Remove(npc);
        }
    }

}
