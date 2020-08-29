using System.Collections.Generic;
using PugetSound.Logic;

namespace PugetSound.Models
{
    public class RoomHistoryModel
    {
        public IReadOnlyCollection<IRoomEvent> RoomEvents { get; set; }

        public string RoomName { get; set; }
    }
}
