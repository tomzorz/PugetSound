using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AspNet.Security.OAuth.Spotify;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PugetSound.Logic;
using SpotifyAPI.Web;

namespace PugetSound.Auth
{
    public class SpotifyAccessService
    {
        private readonly Dictionary<string, string> _usernameToRefreshTokenStore;

        private readonly Dictionary<string, (DateTimeOffset expiresAt, SpotifyWebAPI memberApi)> _usernameToApiStore;

        public SpotifyAccessService(ILogger<SpotifyAccessService> logger)
        {
            _logger = logger;
            _usernameToRefreshTokenStore = new Dictionary<string, string>();
            _usernameToApiStore = new Dictionary<string, (DateTimeOffset expiresAt, SpotifyWebAPI memberApi)>();
        }

        private string _clientId;
        private string _clientSecret;
        private readonly ILogger<SpotifyAccessService> _logger;

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

        public void StoreMemberApi(string username, SpotifyWebAPI api)
        {
            _usernameToApiStore[username] = (DateTimeOffset.Now.AddMinutes(59.0), api);
        }

        public async Task<SpotifyWebAPI> TryGetMemberApi(string username)
        {
            // we have one cached
            if(_usernameToApiStore.ContainsKey(username))
            {
                var (expiresAt, api) = _usernameToApiStore[username];

                // we have a valid api, return it
                if (expiresAt > DateTimeOffset.Now) return api;

                // otherwise clear it out and continue
                _usernameToApiStore.Remove(username);
            }

            // try refresh to get a new one
            if (!TryGetRefreshToken(username, out var refreshToken)) throw new Exception("Tried to renew access token, but couldn't find a refresh token");

            // actual refresh logic
            var tr = await TryRefreshTokenAsync(refreshToken);

            if (string.IsNullOrWhiteSpace(tr.access_token)) throw new Exception("Tried to renew access token but failed to do so");

            if (!string.IsNullOrWhiteSpace(tr.refresh_token)) StoreToken(username, tr.refresh_token);

            _usernameToApiStore[username] = (DateTimeOffset.Now.AddMinutes(59.0), tr.access_token.FromAccessToken());

            _logger.Log(LogLevel.Information, "Renewed access token for {Username}", username);

            return _usernameToApiStore[username].memberApi;
        }
    }
}
