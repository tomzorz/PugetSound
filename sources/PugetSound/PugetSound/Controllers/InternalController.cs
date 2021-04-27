using System;
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
using SpotifyAPI.Web.Enums;

namespace PugetSound.Controllers
{
    [Authorize(AuthenticationSchemes = "Spotify")]
    public class InternalController : Controller
    {
        private readonly RoomService _roomService;
        private readonly ILogger<InternalController> _logger;
        private readonly SpotifyAccessService _spotifyAccessService;

        public InternalController(RoomService roomService, ILogger<InternalController> logger, SpotifyAccessService spotifyAccessService)
        {
            _roomService = roomService;
            _logger = logger;
            _spotifyAccessService = spotifyAccessService;
        }


        public async Task<IActionResult> Index(string join = null)
        {
            var username = HttpContext.User.Claims.GetSpotifyUsername();

            var api = await _spotifyAccessService.TryGetMemberApi(username);

            // guarantee playlist
            var profile = await api.GetPrivateProfileAsync();

            var queuePlaylist = "";
            var queuePlaylistUrl = "";
            var playlistMessage = "";

            var offset = 0;
            const int paginationLimit = 50;
            var playlists = await api.GetUserPlaylistsAsync(profile.Id, paginationLimit, offset);
            // ReSharper disable once UselessBinaryOperation (this seems to be a resharper issue)
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

                playlists = await api.GetUserPlaylistsAsync(profile.Id, paginationLimit, offset);
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
        public async Task<IActionResult> Room(IndexModel room)
        {
            var username = HttpContext.User.Claims.GetSpotifyUsername();

            // turn off shuffle and repeat, best effort...
            try
            {
                var api = await _spotifyAccessService.TryGetMemberApi(username);

                var devices = await api.GetDevicesAsync();

                devices.ThrowOnError(nameof(api.GetDevices));

                if (!devices.Devices.Any()) throw new Exception("No devices available to set shuffle/repeat on!");

                var device = devices.Devices.FirstOrDefault(x => x.IsActive) ?? devices.Devices.First();

                var repeatResult = await api.SetRepeatModeAsync(RepeatState.Off, device.Id);
                repeatResult.ThrowOnError(nameof(api.SetRepeatModeAsync));

                var shuffleResult = await api.SetShuffleAsync(false, device.Id);
                shuffleResult.ThrowOnError(nameof(api.SetShuffleAsync));

                _logger.Log(LogLevel.Information, "Turned off shuffle and repeat for {Username}", username);
            }
            catch (Exception e)
            {
                // oh well
                Debug.WriteLine(e);
                _logger.Log(LogLevel.Warning, "Failed to turn off shuffle and/or repeat for {Username} upon entering room because {@Exception}", username, e);
            }

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

        public async Task<IActionResult> LeaveRoom(RoomModel model)
        {
            var username = HttpContext.User.Claims.GetSpotifyUsername();

            var hasRoom = _roomService.TryGetRoomForUsername(model.UserName, out var room);

            if (username == model.UserName && hasRoom)
            {
                room.MemberLeave(room.Members.FirstOrDefault(x => x.UserName == username));
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Admin()
        {
            return View(new AdminModel());
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult AdminRoomCleanup(AdminModel model)
        {
            var username = HttpContext.User.Claims.GetSpotifyUsername();
            var adminUsername = Environment.GetEnvironmentVariable("PugetSoundAdminUser");

            if (username != adminUsername)
            {
                _logger.Log(LogLevel.Warning, "[ADMIN] {Username} tried to clean up {Room} but wasn't the {AdminUserName}", username, model.RoomName, adminUsername);
                return View("Admin", new AdminModel
                {
                    Result = "Failed to clean up room, you don't have admin rights!"
                });
            }

            var success = _roomService.TryForceCleanupRoom(model.RoomName);

            if (!success)
            {
                _logger.Log(LogLevel.Information, "[ADMIN] {Username} tried to clean up {Room} but the operation failed.", username, model.RoomName);
                return View("Admin", new AdminModel
                {
                    Result = $"Failed to clean up {model.RoomName}"
                });
            }

            _logger.Log(LogLevel.Information, "[ADMIN] {Username} cleaned up {Room}", username, model.RoomName);
            return View("Admin", new AdminModel
            {
                Result = $"Successfully cleaned up {model.RoomName}"
            });
        }

        public IActionResult AdminKickUser(AdminModel model)
        {
            var username = HttpContext.User.Claims.GetSpotifyUsername();
            var adminUsername = Environment.GetEnvironmentVariable("PugetSoundAdminUser");

            if (username != adminUsername)
            {
                _logger.Log(LogLevel.Warning,
                    "[ADMIN] {Username} tried to kick user {KickUsername} from {Room} but wasn't the {AdminUserName}",
                    username, model.UserName, model.RoomName, adminUsername);
                return View("Admin", new AdminModel
                {
                    Result = "Failed to kick user from room, you don't have admin rights!"
                });
            }

            var success = _roomService.TryKickUserFromRoom(model.RoomName, model.UserName);

            if (!success)
            {
                _logger.Log(LogLevel.Information, "[ADMIN] {Username} tried to kick {KickUsername} from {Room} but the operation failed.", username, model.UserName, model.RoomName);
                return View("Admin", new AdminModel
                {
                    Result = $"Failed to kick {model.UserName} from {model.RoomName}"
                });
            }

            _logger.Log(LogLevel.Information, "[ADMIN] {Username} kicked {KickUsername} from {Room}", username,
                model.UserName, model.RoomName);
            return View("Admin", new AdminModel
            {
                Result = $"Successfully kicked {model.UserName} from {model.RoomName}"
            });
        }
    }
}