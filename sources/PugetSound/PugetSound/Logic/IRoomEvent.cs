using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PugetSound.Logic
{
    public interface IRoomEvent
    {
        DateTimeOffset At { get; }

        string Title { get; }

        string Uri { get; }
    }
}
