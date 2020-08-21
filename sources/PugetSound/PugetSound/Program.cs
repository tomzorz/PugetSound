using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PugetSound.Logic;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

namespace PugetSound
{
    // TODO

    /*
     * FEATURES
     * - add phone / pc webapp meta info maybe
     * - clean up code (ongoing effort)
     * - add whitelist?
     * - create playlist from room history
     * - add chat
     * - add reactions
     *
     * IMPROVEMENTS
     * - add retry play for 5xx error (room failQueue, FailEvent counts down, room worker calls fail events)
     * - add room history who voted to skip song
     * - add progress bar / votes required to vote skip song button
     * - fetch upcoming 1-3 songs for every user on queue listing, show them as upcoming
     *
     * BUGS
     * - leave room doesn't always leaves room? only do it on actual disconnect success
     * - after leave room joining a new one doesn't work?
     *
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
                .MinimumLevel.Override("System", LogEventLevel.Warning) // with the serilog middleware
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