using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PugetSound.Logic;

namespace PugetSound.Helpers
{
    public static class EnumHelpers
    {
        public static Dictionary<string, int> ToClientReactionTotals(this IDictionary<Reaction, int> dict) =>
            dict.ToDictionary(x => x.Key.ToString().ToLowerInvariant(), y => y.Value);
    }
}
