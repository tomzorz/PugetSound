using System;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Models;

namespace PugetSound.Logic
{
    public static class SpotifyHelpers
    {
        public static void ThrowOnError(this BasicModel model, string action)
        {
            if (!model.HasError()) return;

            throw new Exception($"{action} failed. HTTP {model.Error.Status} - {model.Error.Message}");
        }

        public static SpotifyWebAPI FromAccessToken(this string accessToken) => new SpotifyWebAPI
        {
            AccessToken = accessToken,
            TokenType = "Bearer"
        };
    }
}
