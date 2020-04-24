using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PugetSound.Auth;
using PugetSound.Logic;
using PugetSound.Models;
using SpotifyAPI.Web;

namespace PugetSound.Controllers
{
    [Authorize(AuthenticationSchemes = "Spotify")]
    public class HomeController : Controller
    {
        private readonly RoomService _roomService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(RoomService roomService, ILogger<HomeController> _logger)
        {
            _roomService = roomService;
            this._logger = _logger;
        }

        private async Task<SpotifyWebAPI> GetApiForUser()
        {
            var accessToken = await HttpContext.GetTokenAsync(Constants.Spotify, "access_token");

            var api = new SpotifyWebAPI
            {
                AccessToken = accessToken,
                TokenType = "Bearer"
            };

            return api;
        }

        public async Task<IActionResult> Index()
        {
            var api = await GetApiForUser();

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

            while (playlists.HasNextPage())
            {
                var playlist = playlists.Items.FirstOrDefault(x => !x.Collaborative && !x.Public && x.Name == Constants.QueuePlaylistName);

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
            }

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

            _logger.Log(LogLevel.Information, "{Page} loaded for {Username}", "Index", username);

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
                RoomName = alreadyInRoom ? prevRoom.RoomId : ""
            });
        }

        [HttpGet]
        public IActionResult Room()
        {
            var username = HttpContext.User.Claims.GetSpotifyUsername();

            _logger.Log(LogLevel.Information, "{Page} loaded for {Username} with redirect from GET-Room", "Index", username);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Room(IndexModel room)
        {
            var api = await GetApiForUser();

            var username = HttpContext.User.Claims.GetSpotifyUsername();

            // prev room?
            var alreadyInRoom = _roomService.TryGetRoomForUsername(username, out var prevRoom);
            if (alreadyInRoom && room.RoomName != prevRoom.RoomId)
            {
                // if it doesn't match leave the prev one
                prevRoom.MemberLeave(prevRoom.Members.First(x => x.UserName == username));
            }

            var party = _roomService.EnsureRoom(room.RoomName);

            _logger.Log(LogLevel.Information, "{Page} loaded for {Username}", "Room", username);

            var member = new RoomMember(username, HttpContext.User.Claims.GetSpotifyFriendlyName(), room.PlaylistId, api);
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