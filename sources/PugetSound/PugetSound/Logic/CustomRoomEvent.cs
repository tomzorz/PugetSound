using System;

namespace PugetSound.Logic
{
    public class CustomRoomEvent : IRoomEvent
    {
        public CustomRoomEvent(string title)
        {
            Title = title;
            At = DateTimeOffset.Now;
        }

        public DateTimeOffset At { get; }

        public string Title { get; }

        public string Uri { get; }
    }
}
