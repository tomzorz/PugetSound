using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace PugetSound.Logic
{
    public class DevicePersistenceService
    {
        private readonly ILogger<DevicePersistenceService> _logger;
        private readonly Dictionary<string, string> _usernameToActiveDeviceMap;

        public DevicePersistenceService(ILogger<DevicePersistenceService> logger)
        {
            _logger = logger;
            _usernameToActiveDeviceMap = new Dictionary<string, string>();
        }

        public void SetDeviceState(string username, string activeDeviceId)
        {
            _logger.Log(LogLevel.Information, "{DeviceId} saved as active device for {Username}", activeDeviceId, username);

            _usernameToActiveDeviceMap[username] = activeDeviceId;
        }

        public bool TryGetActiveDeviceId(string username, out string activeDeviceId) => _usernameToActiveDeviceMap.TryGetValue(username, out activeDeviceId);

        public void CleanDeviceState(string username)
        {
            _logger.Log(LogLevel.Information, "Cleared active device for {Username}", username);

            _usernameToActiveDeviceMap.Remove(username);
        }
    }
}
