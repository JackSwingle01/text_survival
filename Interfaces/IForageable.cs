using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_survival.Environments;

namespace text_survival.Interfaces
{
    internal interface IForageable
    {
        ForageModule ForageModule { get; }
    }
}
