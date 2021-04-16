using System.Threading;

namespace PugetSound.Logic
{
    public class StatisticsService
    {
        private int _userCount = 0;

        private int _roomCount = 0;

        public int UserCount => _userCount;

        public int RoomCount => _roomCount;

        public StatisticsService()
        {

        }

        public void IncrementUserCount() => Interlocked.Increment(ref _userCount);

        public void DecrementUserCount() => Interlocked.Decrement(ref _userCount);

        public void IncrementRoomCount() => Interlocked.Increment(ref _roomCount);

        public void DecrementRoomCount() => Interlocked.Decrement(ref _roomCount);
    }
}
