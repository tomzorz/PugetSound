namespace PugetSound.Logic
{
    public class RoomState
    {
        public string CurrentDjName { get; set; }

        public string CurrentDjUsername { get; set; }

        public string CurrentSongTitle { get; set; }

        public string CurrentSongArtist { get; set; }

        public string CurrentSongArtUrl { get; set; }

        public bool IsPlayingSong { get; set; }

        public long? SongStartedAtUnixTimestamp { get; set; }

        public long? SongFinishesAtUnixTimestamp { get; set; }
    }
}
