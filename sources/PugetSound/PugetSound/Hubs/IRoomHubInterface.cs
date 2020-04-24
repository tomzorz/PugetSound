using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
