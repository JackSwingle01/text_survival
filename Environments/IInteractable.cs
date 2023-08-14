using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival.Environments
{
    public interface IInteractable
    {
        string Name { get; }
        public void Interact(Player player);
    }
}
