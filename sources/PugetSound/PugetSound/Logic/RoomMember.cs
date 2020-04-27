using System.Text.Json.Serialization;
using SpotifyAPI.Web;

namespace PugetSound.Logic
{
    public class RoomMember
    {
        public bool IsDj { get; set; }

        public string UserName { get; }

        public string FriendlyName { get; }

        public string PlaylistId { get; }

        public bool VotedSkipSong { get; set; }

        public int DjOrderNumber { get; set; }

        public RoomMember(string username, string friendlyName, string playlistId)
        {
            UserName = username;
            FriendlyName = friendlyName;
            PlaylistId = playlistId;
        }
    }
}
