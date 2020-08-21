using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PugetSound.Auth;
using PugetSound.Logic;

namespace PugetSound.Hubs
{
    public class RoomHub : Hub<IRoomHubInterface>
    {
        private readonly RoomService _roomService;
        private readonly ILogger<RoomHub> _logger;

        public RoomHub(RoomService roomService, ILogger<RoomHub> _logger)
        {
            _roomService = roomService;
            this._logger = _logger;
        }

        public override async Task OnConnectedAsync()
        {
            var username = Context.User.Claims.GetSpotifyUsername();

            var hasRoom = _roomService.TryGetRoomForUsername(username, out var room);

            if (!hasRoom) return;

            await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            return Task.CompletedTask;
        }

        public async Task Hello(long clientTime)
        {
            var username = Context.User.Claims.GetSpotifyUsername();

            var hasRoom = _roomService.TryGetRoomForUsername(username, out var room);

            if (!hasRoom) return;

            room.Members.First(x => x.UserName == username).ConnectionId = Context.ConnectionId;

            _logger.Log(LogLevel.Information, "{User} saying hello", username);

            var listeners = room.Members.Where(x => !x.IsDj).ToList();
            var djs = room.Members.Where(x => x.IsDj).OrderBy(y => y.DjOrderNumber).ToList();

            await Clients.Caller.ListenersChanged(listeners);
            await Clients.Caller.DjsChanged(djs);

            await Clients.Caller.ApplyClientTimeDifference(DateTimeOffset.Now.ToUnixTimeMilliseconds() - clientTime);

            if (room.CurrentRoomState.IsPlayingSong) await Clients.Caller.SongChanged(room.CurrentRoomState);
        }

        public Task LeaveRoom()
        {
            var username = Context.User.Claims.GetSpotifyUsername();

            var hasRoom = _roomService.TryGetRoomForUsername(username, out var room);

            if (!hasRoom) return Task.CompletedTask;

            room.MemberLeave(room.Members.FirstOrDefault(x => x.UserName == username));

            return Task.CompletedTask;
        }

        public Task BecomeDj()
        {
            var username = Context.User.Claims.GetSpotifyUsername();

            var hasRoom = _roomService.TryGetRoomForUsername(username, out var room);

            if (!hasRoom) return Task.CompletedTask;

            _logger.Log(LogLevel.Information, "{Username} became DJ in {Room}", username, room.RoomId);

            room.ToggleDj(room.Members.First(x => x.UserName == username), true);

            return Task.CompletedTask;
        }

        public Task BecomeListener()
        {
            var username = Context.User.Claims.GetSpotifyUsername();

            var hasRoom = _roomService.TryGetRoomForUsername(username, out var room);

            if (!hasRoom) return Task.CompletedTask;

            _logger.Log(LogLevel.Information, "{Username} became listener in {Room}", username, room.RoomId);

            room.ToggleDj(room.Members.First(x => x.UserName == username), false);

            return Task.CompletedTask;
        }

        public Task VoteSkipSong()
        {
            var username = Context.User.Claims.GetSpotifyUsername();

            var hasRoom = _roomService.TryGetRoomForUsername(username, out var room);

            if (!hasRoom) return Task.CompletedTask;

            _logger.Log(LogLevel.Information, "{Username} voted to skip song in {Room}", username, room.RoomId);

            room.VoteSkipSong(room.Members.First(x => x.UserName == username));

            return Task.CompletedTask;
        }

        public async Task AddToLiked()
        {
            var username = Context.User.Claims.GetSpotifyUsername();

            var hasRoom = _roomService.TryGetRoomForUsername(username, out var room);

            if (!hasRoom) return;

            _logger.Log(LogLevel.Information, "{Username} in {Room} added the current song to their liked songs", username, room.RoomId);

            await room.AddToLiked(room.Members.First(x => x.UserName == username));
        }
    }
}
