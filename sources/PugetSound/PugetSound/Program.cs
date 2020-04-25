using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PugetSound.Logic;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.Destructurers;

namespace PugetSound
{
    // TODO

    /*
     * - add phone / pc webapp meta info maybe
     * - clean up code (ongoing effort)
     * - add whitelist?
     * - delete room when the last user leaves
     * - create playlist from room history
     * - IF the bug regarding playback sometime stopping for users is caused by their token expiring, fix that...
     * - figure out why the <hr> doesn't appear on the playback page -> actually it looks better without, just in general WHY
     *
     * - add on error token check
     * - add log event for song played for user
     * - add retry play for 5xx error
     */

    public class Program
    {
        public static void Main(string[] args)
        {
            var seqKey = Environment.GetEnvironmentVariable("SeqApiKey");
            var seqUri = Environment.GetEnvironmentVariable("SeqClientAddress");

            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning) // with the serilog middleware
                .Enrich.FromLogContext()
                .Enrich.WithExceptionDetails();
#if DEBUG
            loggerConfiguration.WriteTo.Console()
                .WriteTo.Debug();
#endif

            if (!string.IsNullOrWhiteSpace(seqKey) && !string.IsNullOrWhiteSpace(seqUri))
            {
                loggerConfiguration.WriteTo.Seq(seqUri, compact: true, apiKey: seqKey);
            }

            Log.Logger = loggerConfiguration.CreateLogger();

            try
            {
                Log.Information("Starting web host");
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
              .ConfigureLogging(logging =>
              {
                  //logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
                  //logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Debug);
              })
              .ConfigureWebHostDefaults(webBuilder =>
              {
                  webBuilder.UseStartup<Startup>();
              }).ConfigureAppConfiguration((hostingContext, config) =>
              {
                  config.AddEnvironmentVariables();
              }).ConfigureServices(services => { services.AddHostedService<RoomWorker>(); })
              .UseSerilog();
    }
}