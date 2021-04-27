using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PugetSound.Auth;
using PugetSound.Data.Services;
using PugetSound.Helpers;
using PugetSound.Logic;

namespace PugetSound.Hubs
{
    public class RoomHub : Hub<IRoomHubInterface>
    {
        private readonly RoomService _roomService;
        private readonly ILogger<RoomHub> _logger;
        private readonly UserScoreService _userScoreService;

        public RoomHub(RoomService roomService, ILogger<RoomHub> logger, UserScoreService userScoreService)
        {
            _roomService = roomService;
            _logger = logger;
            _userScoreService = userScoreService;
        }

        private (bool isValid, PartyRoom room, string username) TryEnsure()
        {
            var username = Context.User.Claims.GetSpotifyUsername();

            var hasRoom = _roomService.TryGetRoomForUsername(username, out var room);

            return (hasRoom, room, username);
        }

        public override async Task OnConnectedAsync()
        {
            // ensure room
            var (isValid, room, username) = TryEnsure();
            if (!isValid) return;

            // add to room group
            await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            return Task.CompletedTask;
        }

        public async Task Hello(long clientTime)
        {
            // ensure room
            var (isValid, room, username) = TryEnsure();
            if (!isValid) return;

            // assign connection ID
            room.Members.First(x => x.UserName == username).ConnectionId = Context.ConnectionId;

            _logger.Log(LogLevel.Information, "{Username} saying hello", username);

            // send down state
            var (listeners, djs) = await room.Members.UpdateAndSplitMembers(_userScoreService);

            await Clients.Caller.ListenersChanged(listeners);
            await Clients.Caller.DjsChanged(djs);

            await Clients.Caller.ApplyClientTimeDifference(DateTimeOffset.Now.ToUnixTimeMilliseconds() - clientTime);

            await Clients.Caller.UpdateReactionTotals(room.CurrentReactionTotals.ToClientReactionTotals());

            // play song if mid-song
            if (room.CurrentRoomState.IsPlayingSong) await Clients.Caller.SongChanged(room.CurrentRoomState);
        }

        public Task ToggleDj(bool isDj)
        {
            // ensure room
            var (isValid, room, username) = TryEnsure();
            if (!isValid) return Task.CompletedTask;

            _logger.Log(LogLevel.Information,
                // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                isDj ? "{Username} became DJ in {Room}" : "{Username} became listener in {Room}", username,
                room.RoomId);

            // set to dj
            room.ToggleDj(room.Members.First(x => x.UserName == username), isDj);

            return Task.CompletedTask;
        }

        public Task VoteSkipSong()
        {
            // ensure room
            var (isValid, room, username) = TryEnsure();
            if (!isValid) return Task.CompletedTask;

            _logger.Log(LogLevel.Information, "{Username} voted to skip song in {Room}", username, room.RoomId);

            // vote skip
            room.VoteSkipSong(room.Members.First(x => x.UserName == username));

            return Task.CompletedTask;
        }

        public async Task AddToLiked()
        {
            // ensure room
            var (isValid, room, username) = TryEnsure();
            if (!isValid) return;

            _logger.Log(LogLevel.Information, "{Username} in {Room} added the current song to their liked songs", username, room.RoomId);

            // add to liked
            await room.AddToLiked(room.Members.First(x => x.UserName == username));
        }

        public Task ReactionPressed(string reaction)
        {
            // ensure room
            var (isValid, room, username) = TryEnsure();
            if (!isValid) return Task.CompletedTask;

            // mark reaction
            var didChangeReactionState = room.UserReaction(room.Members.First(x => x.UserName == username), reaction);

            if (!didChangeReactionState) return Task.CompletedTask;

            _logger.Log(LogLevel.Information, "{Username} in {Room} reacted to the current song with {Reaction}", username, room.RoomId, reaction);

            return Task.CompletedTask;
        }

        private static readonly ConcurrentDictionary<string, DateTimeOffset> ConnectionTimeouts = new ConcurrentDictionary<string, DateTimeOffset>();

        private static readonly TimeSpan ChatTimeout = TimeSpan.FromSeconds(3.0);

        public async Task SendMessage(string message)
        {
            // no empty messages
            if (string.IsNullOrWhiteSpace(message)) return;

            // no too long messages
            if (message.Length > Constants.MaxChatMessageLength)
            {
                await Clients.Caller.ShowNotification(RoomNotificationCategory.Error.Stringify(), $"Chat message has to be shorter than {Constants.MaxChatMessageLength} characters.");
                return;
            }

            // ensure room
            var (isValid, room, username) = TryEnsure();
            if (!isValid) return;

            // ensure value
            if (!ConnectionTimeouts.ContainsKey(Context.ConnectionId))  ConnectionTimeouts[Context.ConnectionId] = DateTimeOffset.MinValue;

            var hasTimeout = ConnectionTimeouts.TryGetValue(Context.ConnectionId, out var timeout);
            if (!hasTimeout) return;

            if (DateTimeOffset.Now - timeout > ChatTimeout)
            {
                switch (message)
                {
                    case "/fixplaying":
                        // fix playback for current user
                        room.TryFixPlaybackForMember(room.Members.First(x => x.UserName == username));
                        break;
                    case "/djskip":
                        // skip the current song if it came from this user
                        room.TryForceSkipAsDj(room.Members.First(x => x.UserName == username));
                        break;
                    default:
                        // send message
                        await Clients.Group(room.RoomId).Chat(room.Members.First(x => x.UserName == username).FriendlyName, message);
                        break;
                }

                // set new timeout
                ConnectionTimeouts[Context.ConnectionId] = DateTimeOffset.Now;
            } else
            {
                // pls wait
                await Clients.Caller.Chat(Constants.SystemChatUsername, "Chat disabled for 3 seconds.");
            }
        }
    }
}
