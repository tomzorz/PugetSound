using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PugetSound.Auth;
using PugetSound.Logic;
using PugetSound.Models;

namespace PugetSound.Controllers
{
    [Authorize(AuthenticationSchemes = "Spotify")]
    public class InternalController : Controller
    {
        private readonly RoomService _roomService;
        private readonly ILogger<InternalController> _logger;
        private readonly SpotifyAccessService _spotifyAccessService;

        public InternalController(RoomService roomService, ILogger<InternalController> _logger, SpotifyAccessService spotifyAccessService)
        {
            _roomService = roomService;
            this._logger = _logger;
            _spotifyAccessService = spotifyAccessService;
        }


        public async Task<IActionResult> Index(string join = null)
        {
            var api = await _spotifyAccessService.TryGetMemberApi(HttpContext.User.Claims.GetSpotifyUsername());

            var username = HttpContext.User.Claims.GetSpotifyUsername();

            // guarantee playlist
            var profile = await api.GetPrivateProfileAsync();

            var queuePlaylist = "";
            var queuePlaylistUrl = "";
            var playlistMessage = "";

            var offset = 0;
            const int paginationLimit = 50;
            var playlists = api.GetUserPlaylists(profile.Id, paginationLimit, offset);
            offset += paginationLimit;

            do
            {
                var playlist = playlists.Items.FirstOrDefault(x =>
                    !x.Collaborative && !x.Public && x.Name == Constants.QueuePlaylistName);

                // found it
                if (playlist != null)
                {
                    queuePlaylist = playlist.Id;
                    queuePlaylistUrl = playlist.Uri;
                    playlistMessage = "Found existing queue playlist: ";
                    break;
                }

                playlists = api.GetUserPlaylists(profile.Id, paginationLimit, offset);
                offset += paginationLimit;
            } while (playlists.HasNextPage());

            // if we didn't find the playlist create it
            if (string.IsNullOrWhiteSpace(queuePlaylist))
            {
                var newPlaylist = await api.CreatePlaylistAsync(profile.Id, Constants.QueuePlaylistName, false, false,
                    "PugetSound Queue playlist - add songs here you want to play");
                queuePlaylist = newPlaylist.Id;
                queuePlaylistUrl = newPlaylist.Uri;
                playlistMessage = "Created queue playlist: ";

                _logger.Log(LogLevel.Information, "Created queue playlist for {Username}", username);
            }
            else
            {
                _logger.Log(LogLevel.Information, "Found queue playlist for {Username}", username);
            }

            var friendlyName = HttpContext.User.Claims.GetSpotifyFriendlyName();

            var alreadyInRoom = _roomService.TryGetRoomForUsername(username, out var prevRoom);

            // log

            _logger.Log(LogLevel.Information, "Welcoming {FriendlyName} as {Username}", friendlyName, username);

            if (alreadyInRoom) _logger.Log(LogLevel.Information, "Pre-filled {Room} for {Username}", prevRoom.RoomId, username);

            // return login page

            return View(new IndexModel
            {
                UserName = username,
                FriendlyName = friendlyName,
                ProfileLink = profile.Uri,
                PlaylistLink = queuePlaylistUrl,
                PlaylistMessage = playlistMessage,
                PlaylistId = queuePlaylist,
                IsAlreadyInRoom = alreadyInRoom,
                RoomName = alreadyInRoom ? prevRoom.RoomId : join ?? ""
            });
        }

        [HttpGet]
        public IActionResult Room()
        {
            return RedirectToAction(nameof(Index));
        }

        [Route("internal/roomhistory/{room}")]
        public IActionResult RoomHistory(string room)
        {
            var roomEvents = _roomService.TryGetRoomEvents(room);

            return View(nameof(RoomHistory), new RoomHistoryModel
            {
                RoomEvents = roomEvents,
                RoomName = room
            });
        }

        private const string NaughtyRoomName = "for-naughty-people";

        [HttpPost]
        public IActionResult Room(IndexModel room)
        {
            var username = HttpContext.User.Claims.GetSpotifyUsername();

            // prev room?
            var alreadyInRoom = _roomService.TryGetRoomForUsername(username, out var prevRoom);
            if (alreadyInRoom && room.RoomName != prevRoom.RoomId)
            {
                // if it doesn't match leave the prev one
                prevRoom.MemberLeave(prevRoom.Members.First(x => x.UserName == username));
            }

            // sanitize room name
            var rgx = new Regex("[^a-zA-Z-]");
            if (string.IsNullOrWhiteSpace(room.RoomName)) room.RoomName = NaughtyRoomName;
            var sanitizedRoomName = rgx.Replace(room.RoomName.Replace(" ", "-"), string.Empty);
            if (string.IsNullOrWhiteSpace(sanitizedRoomName) || sanitizedRoomName.Length < 3)
            {
                // hehe
                sanitizedRoomName = NaughtyRoomName;
            }
            room.RoomName = sanitizedRoomName;

            var party = _roomService.EnsureRoom(room.RoomName);

            _logger.Log(LogLevel.Information, "{Page} loaded for {Username}", "Room", username);

            var member = new RoomMember(username, HttpContext.User.Claims.GetSpotifyFriendlyName(), room.PlaylistId);
            party.MemberJoin(member);

            return View(new RoomModel
            {
                RoomName = room.RoomName,
                UserName = username,
                QueuePlaylistLink = $"spotify:playlist:{room.PlaylistId}"
            });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}