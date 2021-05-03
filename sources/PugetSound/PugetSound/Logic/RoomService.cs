using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PugetSound.Auth;
using PugetSound.Data.Services;
using PugetSound.Helpers;
using PugetSound.Hubs;

namespace PugetSound.Logic
{
    public class RoomService
    {
        private readonly IHubContext<RoomHub, IRoomHubInterface> _roomHubContext;
        private readonly ILogger<RoomService> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly SpotifyAccessService _spotifyAccessService;
        private readonly UserScoreService _userScoreService;
        private readonly StatisticsService _statisticsService;
        private readonly DevicePersistenceService _devicePersistenceService;

        public RoomService(IHubContext<RoomHub, IRoomHubInterface> roomHubContext, ILogger<RoomService> logger, ILoggerFactory loggerFactory, SpotifyAccessService spotifyAccessService,
            UserScoreService userScoreService, StatisticsService statisticsService, DevicePersistenceService devicePersistenceService)
        {
            _roomHubContext = roomHubContext;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _spotifyAccessService = spotifyAccessService;
            _userScoreService = userScoreService;
            _statisticsService = statisticsService;
            _devicePersistenceService = devicePersistenceService;
            _rooms = new Dictionary<string, PartyRoom>();
            _memberRoomCache = new Dictionary<string, PartyRoom>();
        }

        private readonly Dictionary<string, PartyRoom> _rooms;
        private readonly Dictionary<string, PartyRoom> _memberRoomCache;

        public IReadOnlyCollection<IRoomEvent> TryGetRoomEvents(string roomId)
        {
            return _rooms.ContainsKey(roomId) ? _rooms[roomId].RoomEvents : new List<IRoomEvent>
            {
                new CustomRoomEvent("You tried to see the room history for a room that doesn't exist. Womp-womp.")
            };
        }

        public PartyRoom EnsureRoom(string roomId)
        {
            if (_rooms.ContainsKey(roomId)) return _rooms[roomId];

            var roomLogger = _loggerFactory.CreateLogger<PartyRoom>();
            var room = new PartyRoom(roomId, roomLogger, _spotifyAccessService, _userScoreService, _statisticsService, _devicePersistenceService);
            _statisticsService.IncrementRoomCount();
            room.OnRoomMembersChanged += Room_OnRoomMembersChanged;
            room.OnRoomNotification += Room_OnRoomNotification;
            room.OnRoomCurrentReactionsChanged += Room_OnRoomCurrentReactionsChanged;
            _rooms[roomId] = room;

            _logger.Log(LogLevel.Information, "Created {Room}", roomId);

            return _rooms[roomId];
        }

        private async void Room_OnRoomCurrentReactionsChanged(object sender, RoomCurrentReactions e)
        {
            // get room
            var room = (PartyRoom)sender;

            // update web clients
            await _roomHubContext.Clients.Group(room.RoomId).UpdateReactionTotals(e.ReactionTotals.ToClientReactionTotals());
        }

        private async void Room_OnRoomNotification(object sender, RoomNotification e)
        {
            // get room
            var room = (PartyRoom)sender;

            // update web clients
            if (string.IsNullOrWhiteSpace(e.TargetId))
            {
                await _roomHubContext.Clients.Group(room.RoomId).ShowNotification(e.Category.Stringify(), e.Message);
            }
            else
            {
                await _roomHubContext.Clients.Client(e.TargetId).ShowNotification(e.Category.Stringify(), e.Message);
            }
        }

        private async void Room_OnRoomMembersChanged(object sender, string e)
        {
            // get room
            var room = (PartyRoom) sender;

            // update cache
            foreach (var roomMember in room.Members)
            {
                _memberRoomCache[roomMember.UserName] = room;
            }

            if (!string.IsNullOrWhiteSpace(e))
            {
                if (room.Members.All(x => x.UserName != e))
                {
                    // user just left room, remove from cache
                    _memberRoomCache.Remove(e);

                    _logger.Log(LogLevel.Information, "{Username} left {Room}", e, room.RoomId);
                }
                else
                {
                    _logger.Log(LogLevel.Information, "{Username} joined {Room}", e, room.RoomId);
                    _logger.Log(LogLevel.Information, "{Username} became listener in {Room}", e, room.RoomId);
                }
            }

            // update web clients
            var (listeners, djs) = await room.Members.UpdateAndSplitMembers(_userScoreService);
            await _roomHubContext.Clients.Group(room.RoomId).ListenersChanged(listeners);
            await _roomHubContext.Clients.Group(room.RoomId).DjsChanged(djs);
        }

        public bool TryGetRoomForUsername(string username, out PartyRoom room)
        {
            var s = _memberRoomCache.TryGetValue(username, out var roomActual);
            room = s ? roomActual : null;
            return s;
        }

        public bool TryForceCleanupRoom(string roomName)
        {
            var roomExists = _rooms.TryGetValue(roomName, out var room);

            if (!roomExists) return false;

            // temp capture list so we don't modify it while looping
            var tmpMemberList = room.Members.ToList();

            foreach (var roomMember in tmpMemberList)
            {
                room.MemberLeave(roomMember);
            }

            _rooms.Remove(roomName);

            _statisticsService.DecrementRoomCount();

            return true;
        }

        public bool TryKickUserFromRoom(string roomName, string userName)
        {
            var roomExists = _rooms.TryGetValue(roomName, out var room);

            if (!roomExists) return false;

            var roomMember = room.Members.FirstOrDefault(x => x.UserName == userName);

            if (roomMember == null) return false;

            room.MemberLeave(roomMember);

            return true;
        }

        public async Task ProcessRoomsAsync()
        {
            var roomsForCleanup = new List<PartyRoom>();

            foreach (var partyRoom in _rooms)
            {
                // no song played in the last 2 hours, schedule room for removal
                if (partyRoom.Value.TimeSinceLastSongPlayed + TimeSpan.FromHours(2) < DateTimeOffset.Now)
                {
                    roomsForCleanup.Add(partyRoom.Value);
                    continue;
                }

                // remove users with 10+ play failures
                var problematicMembers = partyRoom.Value.Members.Where(x => x.PlayFailureCount > 10).ToList();
                foreach (var problematicMember in problematicMembers)
                {
                    try
                    {
                        partyRoom.Value.MemberLeave(problematicMember);

                        await _roomHubContext.Clients.Client(problematicMember.ConnectionId).ForcedRoomLeave();

                        _logger.Log(LogLevel.Information, "Kicked {Username} for exceeding maximum playback failure count.", problematicMember.UserName);
                    }
                    catch (Exception e)
                    {
                        _logger.Log(LogLevel.Warning, "Error during removing problematic member {Username} because {@Exception}", problematicMember.UserName, e);
                    }
                }

                try
                {
                    // do retry tasks
                    await partyRoom.Value.DoRetryTasks();

                    // handle room state / play next when applicable
                    var roomState = await partyRoom.Value.TryPlayNext();

                    if (!roomState.IsPlayingSong) continue;

                    _logger.Log(LogLevel.Information, "{Room} playing song {SongTitle} by {SongArtist}, queued by {Username}",
                        partyRoom.Value.RoomId, roomState.CurrentSongTitle, roomState.CurrentSongArtist, roomState.CurrentDjUsername);

                    await _roomHubContext.Clients.Group(partyRoom.Key).Chat(Constants.RoomChatUsername, $"Now playing {roomState.CurrentSongTitle} by {roomState.CurrentSongArtist}");

                    await _roomHubContext.Clients.Group(partyRoom.Key).SongChanged(roomState);
                }
                catch (Exception e)
                {
                    _logger.Log(LogLevel.Warning, "Error during processing {Room} because {@Exception}", partyRoom.Value.RoomId, e);
                }
            }

            // clean up old rooms
            foreach (var room in roomsForCleanup)
            {
                // remove members
                var members = room.Members.ToList();
                foreach (var roomMember in members)
                {
                    try
                    {
                        room.MemberLeave(roomMember);

                        await _roomHubContext.Clients.Client(roomMember.ConnectionId).ForcedRoomLeave();

                        _logger.Log(LogLevel.Information, "Kicked {Username} as their room is closing down.", roomMember.UserName);
                    }
                    catch (Exception e)
                    {
                        _logger.Log(LogLevel.Warning, "Error during removing member {Username} because {@Exception}", roomMember.UserName, e);
                    }
                }

                // ensure stats consistency (shouldn't really happen but eh)
                var rc = room.Members.Count;
                if (rc > 0)
                {
                    for (int i = 0; i < rc; i++)
                    {
                        _statisticsService.DecrementUserCount();
                    }
                }

                // remove room
                room.OnRoomMembersChanged -= Room_OnRoomMembersChanged;
                room.OnRoomNotification -= Room_OnRoomNotification;
                room.OnRoomCurrentReactionsChanged -= Room_OnRoomCurrentReactionsChanged;
                _rooms.Remove(room.RoomId);
                _statisticsService.DecrementRoomCount();
                _logger.Log(LogLevel.Information, "Cleaned up silent room {Room}", room.RoomId);
            }
        }

        public List<(string roomid, List<(string username, string friendlyname)>)> GenerateAdminSummary()
            => _rooms
                .Select(
                    partyRoom
                        => (partyRoom.Value.RoomId,
                            partyRoom.Value.Members
                                .Select(x => (x.UserName, x.FriendlyName))
                                .ToList()))
                .ToList();
    }
}