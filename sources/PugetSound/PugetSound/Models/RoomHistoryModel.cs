using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Humanizer.Configuration;
using Humanizer.DateTimeHumanizeStrategy;
using PugetSound.Logic;

namespace PugetSound.Models
{
    public class RoomHistoryModel
    {
        public IReadOnlyCollection<IRoomEvent> RoomEvents { get; set; }

        public string RoomName { get; set; }
    }
}
