using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PugetSound.Auth;
using PugetSound.Hubs;

namespace PugetSound.Logic
{
    public class RoomService
    {
        private readonly IHubContext<RoomHub, IRoomHubInterface> _roomHubContext;
        private readonly ILogger<RoomService> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly SpotifyAccessService _spotifyAccessService;

        public RoomService(IHubContext<RoomHub, IRoomHubInterface> roomHubContext, ILogger<RoomService> logger, ILoggerFactory loggerFactory, SpotifyAccessService spotifyAccessService)
        {
            _roomHubContext = roomHubContext;
            this._logger = logger;
            _loggerFactory = loggerFactory;
            _spotifyAccessService = spotifyAccessService;
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

            var roomLogger = _loggerFactory.CreateLogger(typeof(PartyRoom));
            var room = new PartyRoom(roomId, roomLogger, _spotifyAccessService);
            room.OnRoomMembersChanged += Room_OnRoomMembersChanged;
            _rooms[roomId] = room;

            _logger.Log(LogLevel.Information, "Created {Room}", roomId);

            return _rooms[roomId];
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
            var listeners = room.Members.Where(x => !x.IsDj).ToList();
            var djs = room.Members.Where(x => x.IsDj).ToList();

            await _roomHubContext.Clients.Group(room.RoomId).ListenersChanged(listeners);
            await _roomHubContext.Clients.Group(room.RoomId).DjsChanged(djs);
        }

        public bool TryGetRoomForUsername(string username, out PartyRoom room)
        {
            var s = _memberRoomCache.TryGetValue(username, out var roomActual);
            room = s ? roomActual : null;
            return s;
        }

        public async Task ProcessRoomsAsync()
        {
            foreach (var partyRoom in _rooms)
            {
                var roomState = await partyRoom.Value.TryPlayNext();

                if (!roomState.IsPlayingSong) continue;

                _logger.Log(LogLevel.Information, "{Room} playing song {SongTitle} by {SongArtist}, queued by {Username}",
                    partyRoom.Value.RoomId, roomState.CurrentSongTitle, roomState.CurrentSongArtist, roomState.CurrentDjUsername);

                await _roomHubContext.Clients.Group(partyRoom.Key).SongChanged(roomState);
            }
        }
    }
}