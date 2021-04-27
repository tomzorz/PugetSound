using System.Collections.Generic;
using System.Linq;
using SpotifyAPI.Web.Models;

namespace PugetSound.Helpers
{
    public static class DeviceHelpers
    {
        public static Device PickPreferredDevice(this List<Device> devices) => (devices.FirstOrDefault(x => x.IsActive)
                                                                         ?? devices.FirstOrDefault(x => x.Type == "Computer"))
                                                                        ?? devices.First();
    }
}
