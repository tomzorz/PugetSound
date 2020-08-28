using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PugetSound.Data.Models;
using PugetSound.Logic;

namespace PugetSound.Data.Services
{
    public class UserScoreService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<UserScoreService> _logger;
        private readonly Dictionary<string, string> _userHashCache;

        public UserScoreService(IServiceScopeFactory serviceScopeFactory, ILogger<UserScoreService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _userHashCache = new Dictionary<string, string>();
        }

        public async Task FillScores(IReadOnlyCollection<RoomMember> members)
        {
            using var scope = _serviceScopeFactory.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var memberHashes = members.ToDictionary(x => x.UserName, y => GetUserHash(y.UserName));
            var scores = await dbContext.UserScores
                .Where(x => memberHashes.Values.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, y => y.Score);
            foreach (var roomMember in members)
            {
                roomMember.Score = scores.ContainsKey(memberHashes[roomMember.UserName])
                    ? "point".ToQuantity(scores[memberHashes[roomMember.UserName]])
                    : "No points.";
            }
        }

        public async Task IncreaseScoreFoUser(string username, int score)
        {
            using var scope = _serviceScopeFactory.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var userHash = GetUserHash(username);

            var entity = dbContext.Find<UserScore>(userHash);

            if (entity != null)
            {
                // mark as updated and increase value
                dbContext.Update(entity);
                entity.Score += score;

                _logger.Log(LogLevel.Information, "Awarded {Points} points to {Username}, making their new score {Score} points", score, username, entity.Score);
            }
            else
            {
                // add a new one
                await dbContext.UserScores.AddAsync(new UserScore
                {
                    Id = userHash,
                    Score = score
                });

                _logger.Log(LogLevel.Information, "Added {Username} to the database with the initial score {Score} point(s)", username, score);
            }

            await dbContext.SaveChangesAsync();
        }

        private string GetUserHash(string username)
        {
            // try cache
            if (_userHashCache.ContainsKey(username)) return _userHashCache[username];

            // create new
            var hashAlgorithm = new Org.BouncyCastle.Crypto.Digests.Sha3Digest(512);
            var input = Encoding.ASCII.GetBytes(username);
            hashAlgorithm.BlockUpdate(input, 0, input.Length);
            var result = new byte[64];
            hashAlgorithm.DoFinal(result, 0);
            var hashString = BitConverter.ToString(result);

            // add to cache and return
            _userHashCache[username] = hashString;
            return hashString;
        }
    }
}
