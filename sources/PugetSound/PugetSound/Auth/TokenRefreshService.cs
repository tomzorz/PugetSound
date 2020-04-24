using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AspNet.Security.OAuth.Spotify;
using Newtonsoft.Json;

namespace PugetSound.Auth
{
    public class TokenRefreshService
    {
        private Dictionary<string, string> _usernameToRefreshTokenStore;

        private TokenRefreshService()
        {
            _usernameToRefreshTokenStore = new Dictionary<string, string>();
        }

        private static TokenRefreshService _instance;
        private string _clientId;
        private string _clientSecret;

        public static TokenRefreshService Instance => _instance ??= new TokenRefreshService();

        public void SetAccessKeys(string clientId, string clientSecret)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
        }

        public void StoreToken(string username, string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return;
            _usernameToRefreshTokenStore[username] = refreshToken;
        }

        public bool TryGetRefreshToken(string username, out string refreshToken)
        {
            var res = _usernameToRefreshTokenStore.TryGetValue(username, out var t);
            refreshToken = res ? t : null;
            return res;
        }

        public async Task<TokenResponse> TryRefreshTokenAsync(string refreshToken)
        {
            var htc = new HttpClient();

            var keyBytes = Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}");
            htc.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(keyBytes));

            var result = await htc.PostAsync(SpotifyAuthenticationDefaults.TokenEndpoint, new FormUrlEncodedContent(
                new List<KeyValuePair<string, string>>
                {
                      // not the flow we need apparently
                      //new KeyValuePair<string, string>("grant_type", "authorization_code"),
                      //new KeyValuePair<string, string>("code", accessToken.Value),
                      //new KeyValuePair<string, string>("redirect_uri", "/callback")

                      // this is the flow we need
                      new KeyValuePair<string, string>("refresh_token", refreshToken),
                      new KeyValuePair<string, string>("grant_type", "refresh_token"),
                }));

            var resultBody = await result.Content.ReadAsStringAsync();

            result.EnsureSuccessStatusCode();

            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(resultBody);

            if (!string.IsNullOrWhiteSpace(tokenResponse.access_token)) return tokenResponse;

            // something is not right
            Debug.WriteLine(resultBody);
            throw new Exception("TokenResponse AuthToken was empty!");

        }
    }
}
