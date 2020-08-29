using System;

namespace PugetSound.Logic
{
    public interface IRoomEvent
    {
        DateTimeOffset At { get; }

        string Title { get; }

        string Uri { get; }
    }
}
