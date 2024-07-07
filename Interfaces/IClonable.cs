using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_survival.Actors;

namespace text_survival.Interfaces
{
    public interface IClonable<T>
    {
        public delegate T CloneDelegate();
        public CloneDelegate Clone { get; set; }
    }
}
