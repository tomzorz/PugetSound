using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
