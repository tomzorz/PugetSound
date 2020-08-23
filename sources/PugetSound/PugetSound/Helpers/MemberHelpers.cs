using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PugetSound.Logic;

namespace PugetSound.Helpers
{
    public static class MemberHelpers
    {
        public static (List<RoomMember> djs, List<RoomMember> listeners) SplitMembers(this IReadOnlyCollection<RoomMember> members) =>
            (members.Where(x => !x.IsDj).ToList(), members.Where(x => x.IsDj).OrderBy(y => y.DjOrderNumber).ToList());
    }
}
