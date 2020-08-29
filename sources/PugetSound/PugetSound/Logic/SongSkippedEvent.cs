using System;

namespace PugetSound.Logic
{
    public class SongSkippedEvent : IRoomEvent
    {
        public SongSkippedEvent()
        {
            At = DateTimeOffset.Now;
        }

        public DateTimeOffset At { get; }

        public string Title => "Song skipped";

        public string Uri => null;
    }
}
