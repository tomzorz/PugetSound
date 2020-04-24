using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PugetSound.Logic
{
    public enum UserEventType
    {
        JoinedRoom,
        LeftRoom,
        BecameListener,
        BecameDj
    }

    public class UserEvent : IRoomEvent
    {
        public UserEvent(string userName, string friendlyName, UserEventType type)
        {
            Uri = $"spotify:user:{userName}";
            At = DateTimeOffset.Now;

            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (type)
            {
                case UserEventType.JoinedRoom:
                    Title = $"{friendlyName} ({userName}) joined";
                    break;
                case UserEventType.LeftRoom:
                    Title = $"{friendlyName} ({userName}) left";
                    break;
                case UserEventType.BecameListener:
                    Title = $"{friendlyName} ({userName}) became a listener";
                    break;
                case UserEventType.BecameDj:
                    Title = $"{friendlyName} ({userName}) became a DJ";
                    break;
            }
        }

        public DateTimeOffset At { get; }

        public string Title { get; }

        public string Uri { get; }
    }
}
