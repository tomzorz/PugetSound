using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PugetSound.Data.Services;
using PugetSound.Logic;

namespace PugetSound.Helpers
{
    public static class MemberHelpers
    {
        public static async Task<(List<RoomMember> djs, List<RoomMember> listeners)> UpdateAndSplitMembers(this IReadOnlyCollection<RoomMember> members, UserScoreService scoringService)
        {
            await scoringService.FillScores(members);

            return (members.Where(x => !x.IsDj).ToList(),
                members.Where(x => x.IsDj).OrderBy(y => y.DjOrderNumber).ToList());
        }
    }
}
