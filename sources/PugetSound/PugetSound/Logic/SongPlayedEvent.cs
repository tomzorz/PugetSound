using System;

namespace PugetSound.Logic
{
    public class SongPlayedEvent : IRoomEvent
    {
        public SongPlayedEvent(string username, string friendlyName, string title, string id, string songUri)
        {
            Title = $"{friendlyName} ({username}) played {title}";
            Id = id;
            Uri = songUri;
            At = DateTimeOffset.Now;
        }

        public DateTimeOffset At { get; }

        public string Title { get; }

        public string Id { get; }

        public string Uri { get; }
    }
}
