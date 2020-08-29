using System.Collections.Generic;
using System.Threading.Tasks;
using PugetSound.Logic;

namespace PugetSound.Hubs
{
    public interface IRoomHubInterface
    {
        Task ListenersChanged(List<RoomMember> listeners);

        Task DjsChanged(List<RoomMember> djs);

        Task SongChanged(RoomState state);

        Task ApplyClientTimeDifference(long difference);

        Task ShowNotification(string category, string message);

        Task UpdateReactionTotals(Dictionary<string, int> reactionTotals);

        Task Chat(string fromUser, string message);
    }
}
