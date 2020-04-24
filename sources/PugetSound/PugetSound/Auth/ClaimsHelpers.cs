using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace PugetSound.Auth
{
    public static class ClaimsHelpers
    {
        public static string GetSpotifyUsername(this IEnumerable<Claim> claims) => claims.FirstOrDefault(x => x.Issuer == Constants.Spotify && x.Type.EndsWith("nameidentifier"))?.Value ?? "";

        public static string GetSpotifyFriendlyName(this IEnumerable<Claim> claims) => claims.FirstOrDefault(x => x.Issuer == Constants.Spotify && x.Type.EndsWith("name"))?.Value ?? "";
    }
}
