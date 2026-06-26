using SpotifyAPI.Web;

namespace PugetSound.Logic
{
    public static class SpotifyHelpers
    {
        public static ISpotifyClient FromAccessToken(this string accessToken) =>
            new SpotifyClient(SpotifyClientConfig.CreateDefault().WithToken(accessToken));
    }
}
