using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PugetSound.Auth;
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
     * FEATURES
     * - add phone / pc webapp meta info maybe
     * - clean up code (ongoing effort)
     * - add whitelist?
     * - create playlist from room history
     *
     * IMPROVEMENTS
     * - add retry play for 5xx error
     * - add spotify API call timings to log
     *
     * BUGS
     * - fix footer with smaller screens
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