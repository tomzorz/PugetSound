using System;

namespace PugetSound.Logic
{
    [Flags]
    public enum Reaction
    {
        None = 0,
        Rock = 1,
        Flame = 2,
        Clap = 4,
        Heart = 8
    }
}
