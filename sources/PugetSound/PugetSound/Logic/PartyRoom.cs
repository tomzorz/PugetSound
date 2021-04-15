using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PugetSound.Auth;
using PugetSound.Data.Services;
using PugetSound.Logic;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;

namespace PugetSound
{
    public class PartyRoom
    {
        private readonly ILogger _logger;
        private readonly SpotifyAccessService _spotifyAccessService;
        private readonly UserScoreService _userScoreService;
        private DateTimeOffset _handledUntil;
        private DateTimeOffset _timeSinceEmpty;
        private int _currentDjNumber;
        private FullTrack _currentTrack;

        private readonly DateTimeOffset _customFutureDateTimeOffset = new DateTimeOffset(9999, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly List<Func<Task>> _roomRetries;

        public DateTimeOffset TimeSinceEmpty => _timeSinceEmpty;

        public string RoomId { get; }

        public RoomState CurrentRoomState { get; private set; }

        private readonly List<RoomMember> _members;

        public IReadOnlyCollection<RoomMember> Members => _members;

        private readonly List<IRoomEvent> _roomEvents;

        public IReadOnlyCollection<IRoomEvent> RoomEvents => _roomEvents;

        public Dictionary<Reaction, int> CurrentReactionTotals { get; private set; }

        public PartyRoom(string roomId, ILogger logger, SpotifyAccessService spotifyAccessService, UserScoreService userScoreService)
        {
            _logger = logger;
            _spotifyAccessService = spotifyAccessService;
            _userScoreService = userScoreService;
            RoomId = roomId;
            _members = new List<RoomMember>();
            _roomEvents = new List<IRoomEvent>();
            _timeSinceEmpty = _customFutureDateTimeOffset;
            _roomRetries = new List<Func<Task>>();

            _handledUntil = DateTimeOffset.Now;
            _currentDjNumber = -1;
            _currentTrack = null;
            CurrentRoomState = new RoomState();

            UpdateReactionTotals(false);
        }

        public event EventHandler<string> OnRoomMembersChanged;

        public event EventHandler<RoomNotification> OnRoomNotification;

        public event EventHandler<RoomCurrentReactions> OnRoomCurrentReactionsChanged;

        public void MemberJoin(RoomMember member)
        {
            if (_members.Any(x => x.UserName == member.UserName)) return;

            _members.Add(member);
            OnRoomMembersChanged?.Invoke(this, member.UserName);

            _roomEvents.Add(new UserEvent(member.UserName, member.FriendlyName, UserEventType.JoinedRoom));
            OnRoomNotification?.Invoke(this, new RoomNotification
            {
                Category = RoomNotificationCategory.Information,
                Message = $"{member.FriendlyName} joined room"
            });

            ToggleDj(member, false);

            if (_currentTrack != null) StartSongForMemberUgly(member);

            _timeSinceEmpty = _customFutureDateTimeOffset;
        }

        public void TryFixPlaybackForMember(RoomMember member)
        {
            if (_currentTrack == null) return;
            StartSongForMemberUgly(member);
        }

        private async void StartSongForMemberUgly(RoomMember member)
        {
            var left = _handledUntil.ToUnixTimeMilliseconds() - DateTimeOffset.Now.ToUnixTimeMilliseconds();
            await PlaySong(member, _currentTrack, (int) (_currentTrack.DurationMs - left));
        }

        public void ToggleDj(RoomMember member, bool isDj)
        {
            member.IsDj = isDj;

            member.DjOrderNumber = isDj ? _members.Where(x => x.IsDj).Max(y => y.DjOrderNumber) + 1 : -1;

            _roomEvents.Add(new UserEvent(member.UserName, member.FriendlyName, isDj ? UserEventType.BecameDj : UserEventType.BecameListener));
            OnRoomNotification?.Invoke(this, new RoomNotification
            {
                Category = RoomNotificationCategory.Information,
                Message = $"{member.FriendlyName} became a {(isDj ? "DJ" : "listener")}"
            });

            OnRoomMembersChanged?.Invoke(this, null);
        }

        public void VoteSkipSong(RoomMember member)
        {
            var oldVal = member.VotedSkipSong;

            member.VotedSkipSong = true;

            var requiredVotes = _members.Count / 2;
            var totalVotes = _members.Count(x => x.VotedSkipSong);

            var changedCount = oldVal == false;

            if (requiredVotes > totalVotes)
            {
                if (changedCount)
                {
                    OnRoomNotification?.Invoke(this, new RoomNotification
                    {
                        Category = RoomNotificationCategory.Information,
                        Message = $"{member.FriendlyName} voted to skip song, {requiredVotes-totalVotes} more vote(s) until skipping..."
                    });
                }
                return;
            }

            _roomEvents.Add(new SongSkippedEvent());
            OnRoomNotification?.Invoke(this, new RoomNotification
            {
                Category = RoomNotificationCategory.Success,
                Message = $"Skipping song with {_members.Count(x => x.VotedSkipSong)} vote(s)"
            });

            _handledUntil = DateTimeOffset.Now;
            foreach (var roomMember in _members)
            {
                roomMember.VotedSkipSong = false;
            }
        }

        public void MemberLeave(RoomMember member)
        {
            var didRemove = _members.Remove(member);
            if (!didRemove) return;

            _roomEvents.Add(new UserEvent(member.UserName, member.FriendlyName, UserEventType.LeftRoom));
            OnRoomNotification?.Invoke(this, new RoomNotification
            {
                Category = RoomNotificationCategory.Information,
                Message = $"{member.FriendlyName} left the room"
            });

            OnRoomMembersChanged?.Invoke(this, member.UserName);

            UpdateReactionTotals();

            // this was the last member to leave
            if (!_members.Any())
            {
                _timeSinceEmpty = DateTimeOffset.Now;
            }
        }

        public async Task<RoomState> TryPlayNext(bool force = false)
        {
            while (true)
            {
                // return if song is playing right now, except when we're skipping a song
                if (!force && DateTimeOffset.Now < _handledUntil)
                {
                    // we don't change room state here
                    return new RoomState();
                }

                // award points based on the track if there are multiple users in the room
                if (_currentTrack != null && _members.Count > 1)
                {
                    // 0 points base if song got skipped, 1 if not
                    var score = CurrentRoomState.SongFinishesAtUnixTimestamp > DateTimeOffset.Now.ToUnixTimeMilliseconds() ? 0 : 1;
                    // then 1 point for every reaction
                    foreach (var roomMember in _members)
                    {
                        // could write something for this, but eh...
                        if (roomMember.ReactionFlagsForCurrentTrack.HasFlag(Reaction.Heart)) score += 1;
                        if (roomMember.ReactionFlagsForCurrentTrack.HasFlag(Reaction.Rock)) score += 1;
                        if (roomMember.ReactionFlagsForCurrentTrack.HasFlag(Reaction.Flame)) score += 1;
                        if (roomMember.ReactionFlagsForCurrentTrack.HasFlag(Reaction.Clap)) score += 1;
                    }

                    // update score if needed
                    if (score > 0)
                    {
                        await _userScoreService.IncreaseScoreForUser(CurrentRoomState.CurrentDjUsername, score);

                        // update values as we have new scores
                        OnRoomMembersChanged?.Invoke(this, null);
                    }
                }

                _currentTrack = null;

                // try getting next player
                var nextPlayer = GetNextPlayer();

                // if we don't find any we don't have a DJ - this will exit the recursion if we run out of DJs
                if (nextPlayer == null)
                {
                    CurrentRoomState = new RoomState();
                    return CurrentRoomState;
                }

                var song = await GetSongFromQueue(nextPlayer, nextPlayer.PlaylistId);

                // success
                if (song != null)
                {
                    _currentDjNumber = nextPlayer.DjOrderNumber;

                    // do the loop on a tmp list of members, so if someone joins mid-play we don't err out
                    var tmpMembers = _members.ToList();

                    // start songs for everyone (NEW)
                    var sw = new Stopwatch();
                    sw.Start();
                    var playTasks = tmpMembers.Select(x => PlaySong(x, song)).ToList();
                    await Task.WhenAll(playTasks);
                    sw.Stop();
                    _logger.Log(LogLevel.Information, "Took {TimedApiPlayForAll} to start songs for {MemberCount} room members", sw.Elapsed, tmpMembers.Count);

                    // set handled
                    _handledUntil = DateTimeOffset.Now.AddMilliseconds(song.DurationMs);
                    _currentTrack = song;

                    // clear reactions & update them
                    foreach (var roomMember in tmpMembers)
                    {
                        roomMember.ReactionFlagsForCurrentTrack = Reaction.None;
                    }
                    UpdateReactionTotals();

                    var artistSum = string.Join(", ", song.Artists.Select(x => x.Name).ToArray());

                    // return state
                    CurrentRoomState = new RoomState
                    {
                        IsPlayingSong = true,
                        CurrentDjUsername = nextPlayer.UserName,
                        CurrentDjName = nextPlayer.FriendlyName,
                        CurrentSongArtist = artistSum.Length > 50 ? artistSum.Substring(0, 50) + "..." : artistSum,
                        CurrentSongTitle = song.Name.Length > 50 ? song.Name.Substring(0, 50) + "..." : song.Name,
                        CurrentSongArtUrl = song?.Album.Images.FirstOrDefault()?.Url ?? "/images/missingart.jpg",
                        SongStartedAtUnixTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                        SongFinishesAtUnixTimestamp = _handledUntil.ToUnixTimeMilliseconds()
                    };

                    _roomEvents.Add(new SongPlayedEvent(nextPlayer.UserName, nextPlayer.FriendlyName,
                        $"{CurrentRoomState.CurrentSongArtist} - {CurrentRoomState.CurrentSongTitle}", song.Id, song.Uri));

                    return CurrentRoomState;
                }

                // remove user as DJ if song is null
                nextPlayer.IsDj = false;
                OnRoomMembersChanged?.Invoke(this, null);

                // then try again
            }
        }

        private async Task PlaySong(RoomMember member, FullTrack song, int positionMs = 0, bool canRetry = true)
        {
            try
            {
                var api = await _spotifyAccessService.TryGetMemberApi(member.UserName);

                var devices = await api.GetDevicesAsync();

                devices.ThrowOnError(nameof(api.GetDevices));

                if (!devices.Devices.Any()) throw new Exception("No devices available to play on!");

                var device = devices.Devices.FirstOrDefault(x => x.IsActive) ?? devices.Devices.First();

                var resume = await api.ResumePlaybackAsync(deviceId:device.Id, uris: new List<string> { song.Uri }, offset: 0, positionMs: positionMs);

                resume.ThrowOnError(nameof(api.ResumePlaybackAsync));
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Warning, "Failed to play song for {Username} because {@Exception}", member.UserName, e);
                OnRoomNotification?.Invoke(this, new RoomNotification
                {
                    Category = RoomNotificationCategory.Error,
                    Message = $"Failed to play song",
                    TargetId = member.ConnectionId
                });

                if (e.Message.Contains("HTTP 5") && canRetry)
                {
                    // if it's a server side error let's add it to the retry queue
                    async Task RetryTask()
                    {
                        // make sure user hasn't left in the last room-cycle
                        if (!_members.Contains(member)) return;
                        // try starting song again
                        var left = _handledUntil.ToUnixTimeMilliseconds() - DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        await PlaySong(member, _currentTrack, (int) (_currentTrack.DurationMs - left), false);
                    }

                    _logger.Log(LogLevel.Information, "Added retry task for {UserName}", member.UserName);

                    _roomRetries.Add(RetryTask);
                }
                // else oh well

                Debug.WriteLine(e);
            }
        }

        private async Task<FullTrack> GetSongFromQueue(RoomMember member, string playlist)
        {
            try
            {
                var api = await _spotifyAccessService.TryGetMemberApi(member.UserName);

                var queueList = await api.GetPlaylistTracksAsync(playlist);

                queueList.ThrowOnError(nameof(api.GetPlaylistTracks));

                if (!queueList.Items.Any()) return null;

                var track = queueList.Items.First().Track;

                var remove = await api.RemovePlaylistTrackAsync(playlist, new DeleteTrackUri(track.Uri, 0));

                remove.ThrowOnError(nameof(api.RemovePlaylistTrackAsync));

                return track;

            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Warning, "Failed to get song from {Username}'s queue because {@Exception}", member.UserName, e);
                OnRoomNotification?.Invoke(this, new RoomNotification
                {
                    Category = RoomNotificationCategory.Warning,
                    Message = $"Failed to get song from {member.FriendlyName}'s queue"
                });
                Debug.WriteLine(e);
                return null;
            }
        }

        private RoomMember GetNextPlayer()
        {
            if (!_members.Any(x => x.IsDj)) return null;
            var orderedDjs = _members.Where(x => x.IsDj).OrderBy(y => y.DjOrderNumber).ToList();
            var nextDj = orderedDjs.FirstOrDefault(x => x.DjOrderNumber > _currentDjNumber);
            return nextDj ?? orderedDjs.First();
        }

        public async Task AddToLiked(RoomMember member)
        {
            try
            {
                var api = await _spotifyAccessService.TryGetMemberApi(member.UserName);

                var track = _currentTrack;

                var result = await api.SaveTrackAsync(track.Id);

                result.ThrowOnError(nameof(api.SaveTrackAsync));

                OnRoomNotification?.Invoke(this, new RoomNotification
                {
                    Category = RoomNotificationCategory.Success,
                    Message = $"Successfully added {string.Join(", ", track.Artists.Select(x => x.Name).ToArray())} - {track.Name} to your Liked Songs",
                    TargetId = member.ConnectionId
                });
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Warning, "Failed to add song to {Username}'s liked songs because {@Exception}", member.UserName, e);
                OnRoomNotification?.Invoke(this, new RoomNotification
                {
                    Category = RoomNotificationCategory.Error,
                    Message = $"Failed to add song to your Liked Songs",
                    TargetId = member.ConnectionId
                });
                Debug.WriteLine(e);
            }
        }

        public bool UserReaction(RoomMember member, string reaction)
        {
            // no awarding your own song
            if (member.UserName == CurrentRoomState.CurrentDjUsername) return false;

            try
            {
                var actualReaction = Enum.Parse<Reaction>(reaction, true);

                var currentReactions = member.ReactionFlagsForCurrentTrack;
                member.ReactionFlagsForCurrentTrack |= actualReaction;

                if (currentReactions == member.ReactionFlagsForCurrentTrack) return false;

                UpdateReactionTotals();

                return true;
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Warning, "{Username} failed to react to track with {Reaction} because {@Exception}", member.UserName, reaction, e);
                Debug.WriteLine(e);
                return false;
            }
        }

        private void UpdateReactionTotals(bool sendUpdate = true)
        {
            var reactionTotals = Enum.GetValues(typeof(Reaction))
                .Cast<Reaction>()
                .ToDictionary(
                    x => x,
                    y => _members.Count(z => z.ReactionFlagsForCurrentTrack.HasFlag(y)));

            CurrentReactionTotals = reactionTotals;

            if (!sendUpdate) return;

            OnRoomCurrentReactionsChanged?.Invoke(this, new RoomCurrentReactions
            {
                ReactionTotals = reactionTotals
            });
        }

        public async Task DoRetryTasks()
        {
            // do nothing if there are no retry events
            if (!_roomRetries.Any()) return;

            var tasks = _roomRetries.Select(x => x()).ToList();

            var sw = new Stopwatch();
            sw.Start();
            await Task.WhenAll(tasks);
            sw.Stop();
            _logger.Log(LogLevel.Information, "Took {TimedApiRetryPlayForAll} to retry starting songs for {RetryCount} room members", sw.Elapsed, tasks.Count);

            _roomRetries.Clear();
        }
    }
}
