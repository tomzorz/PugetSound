﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SpotifyAPI.Web;

namespace PugetSound
{
    public class RoomMember
    {
        [JsonIgnore]
        public SpotifyWebAPI MemberApi { get; }

        public bool IsDj { get; set; }

        public string UserName { get; }

        public string FriendlyName { get; }

        public string PlaylistId { get; }

        public bool VotedSkipSong { get; set; }

        public int DjOrderNumber { get; set; }

        public RoomMember(string username, string friendlyName, string playlistId, SpotifyWebAPI memberApi)
        {
            UserName = username;
            FriendlyName = friendlyName;
            PlaylistId = playlistId;
            MemberApi = memberApi;
        }
    }
}
