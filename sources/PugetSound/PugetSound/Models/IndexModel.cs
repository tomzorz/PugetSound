using SpotifyAPI.Web.Models;

namespace PugetSound.Models
{
    public class IndexModel
    {
        public string UserName { get; set; }

        public string ProfileLink { get; set; }

        public string FriendlyName { get; set; }

        public string PlaylistMessage { get; set; }

        public string PlaylistLink { get; set; }

        public string RoomName { get; set; }

        public string PlaylistId { get; set; }

        public bool IsAlreadyInRoom { get; set; }
    }
}