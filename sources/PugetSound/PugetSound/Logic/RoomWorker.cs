using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace PugetSound.Logic
{
    public class RoomWorker : BackgroundService
    {
        private readonly RoomService _roomService;

        public RoomWorker(RoomService roomService)
        {
            _roomService = roomService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await _roomService.ProcessRoomsAsync();

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}
