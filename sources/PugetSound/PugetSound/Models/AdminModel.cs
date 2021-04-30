using System.Collections.Generic;

namespace PugetSound.Models
{
    public class AdminModel
    {
        public List<(string roomid, List<(string username, string friendlyname)>)> RoomsAndMembers { get; set; }

        public string RoomName { get; set; }

        public string UserName { get; set; }

        public string Result { get; set; }
    }
}
