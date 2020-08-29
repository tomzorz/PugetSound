using System;
using System.Collections.Generic;

namespace PugetSound.Logic
{
    public class RoomCurrentReactions : EventArgs
    {
        public Dictionary<Reaction, int> ReactionTotals { get; set; }
    }
}
