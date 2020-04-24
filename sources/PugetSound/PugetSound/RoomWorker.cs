using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace PugetSound
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

                Debug.WriteLine("RoomWorker poke");

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }

            // TODO cleanup?
        }
    }
}
