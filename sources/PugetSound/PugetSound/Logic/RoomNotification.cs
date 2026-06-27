using System;
using System.Text.Json.Serialization;

namespace PugetSound.Logic
{
    public class RoomNotification : EventArgs
    {
        public string Message { get; set; }

        public RoomNotificationCategory Category { get; set; }

        [JsonIgnore]
        public string TargetId { get; set; }
    }

    public enum RoomNotificationCategory
    {
        Information,
        Success,
        Warning,
        Error
    }
}
