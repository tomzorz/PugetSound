using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PugetSound.Data;
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
     * - create playlist from room history
     *
     * IMPROVEMENTS
     * - add room history who voted to skip song
     * - add progress bar / votes required to vote skip song button
     * - fetch upcoming 1-3 songs for every user on queue listing, show them as upcoming
     * - allow cleaning up of rooms where there are only listeners and no song has been played for X time
     *
     * BUGS
     * - spotify refresh token shenanigans
     *
     */

    public class Program
    {
        public static void Main(string[] args)
        {
            var seqKey = Environment.GetEnvironmentVariable("SeqApiKey");
            var seqUri = Environment.GetEnvironmentVariable("SeqClientAddress");

            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Debug()
                //.MinimumLevel.Override("Microsoft", LogEventLevel.Warning) // with the serilog middleware
                //.MinimumLevel.Override("System", LogEventLevel.Warning) // with the serilog middleware
                .Enrich.FromLogContext()
                .Enrich.WithExceptionDetails()
                .Enrich.With<LogEnricher>();
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
                Log.Information($"Initializing PugetSound revision {Revision.Footer}");

                Log.Information("Creating web host...");
                var host = CreateHostBuilder(args).Build();

                Log.Information("Initializing database...");
                using (var scope = host.Services.CreateScope())
                {
                    try
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        DbInitializer.Initialize(context);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Failed to initialize database because {@Exception}", ex);
                    }
                }

                Log.Information("Starting web host...");
                host.Run();
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
                  logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
                  logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Debug);
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