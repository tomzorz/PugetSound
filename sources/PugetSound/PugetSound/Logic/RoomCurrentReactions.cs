using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PugetSound.Logic
{
    public class RoomCurrentReactions : EventArgs
    {
        public Dictionary<Reaction, int> ReactionTotals { get; set; }
    }
}
