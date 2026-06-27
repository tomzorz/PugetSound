using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using PugetSound.Auth;
using PugetSound.Data;
using PugetSound.Data.Services;
using PugetSound.Hubs;
using PugetSound.Logic;
using PugetSound.Routing;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using SpotifyAPI.Web;

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
     *
     * BUGS
     * - spotify refresh token shenanigans
     *
     */

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configuration precedence: prefer .NET user secrets, then fall back to environment
            // variables for any value not present in the secret store. User secrets are added last
            // so they take priority over the (already registered) environment variables source.
            builder.Configuration.AddUserSecrets<Program>(optional: true);

            var configuration = builder.Configuration;

            var seqKey = configuration["SeqApiKey"];
            var seqUri = configuration["SeqClientAddress"];

            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning) // with the serilog middleware
                .MinimumLevel.Override("System", LogEventLevel.Warning) // with the serilog middleware
                .Enrich.FromLogContext()
                .Enrich.WithExceptionDetails()
                .Enrich.With<LogEnricher>();
#if DEBUG
            loggerConfiguration.WriteTo.Console()
                .WriteTo.Debug();
#endif

            if (!string.IsNullOrWhiteSpace(seqKey) && !string.IsNullOrWhiteSpace(seqUri))
            {
                loggerConfiguration.WriteTo.Seq(seqUri, apiKey: seqKey);
            }

            Log.Logger = loggerConfiguration.CreateLogger();

            builder.Host.UseSerilog();

            try
            {
                Log.Information($"Initializing PugetSound revision {Revision.Footer}");

                Log.Information("Creating web host...");

                ConfigureServices(builder.Services, configuration);
                builder.Services.AddHostedService<RoomWorker>();

                var app = builder.Build();

                Log.Information("Initializing database...");
                using (var scope = app.Services.CreateScope())
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

                Configure(app, app.Environment);

                Log.Information("Starting web host...");
                app.Run();
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

        // This method gets called by the runtime. Use this method to add services to the container.
        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite("Data Source=pugetsound.sqlite");
            });

            services.AddSingleton<StatisticsService>();
            services.AddSingleton<UserScoreService>();
            services.AddSingleton<DevicePersistenceService>();
            services.AddSingleton<RoomService>();

            services.AddControllersWithViews(configure =>
            {
                configure.Conventions.Add(new RouteTokenTransformerConvention(new LowerCaseParameterTransformer()));
            });

            services.AddSignalR(options =>
            {
                options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
            });

            services.AddSingleton(serviceProvider =>
            {
                var logger = serviceProvider.GetService<ILogger<SpotifyAccessService>>();
                var sas = new SpotifyAccessService(logger);
                sas.SetAccessKeys(configuration["SpotifyClientId"], configuration["SpotifyClientSecret"]);
                return sas;
            });

            services.AddAuthentication(o => o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme)
              .AddCookie(options =>
              {
                  options.Events = new CookieAuthenticationEvents
                  {
                      OnValidatePrincipal = async context =>
                      {
                          var sas = context.HttpContext.RequestServices.GetService<SpotifyAccessService>();

                          //check to see if user is authenticated first
                          if (context.Principal.Identity.IsAuthenticated)
                          {
                              try
                              {
                                  /*
                                   * big thanks to https://stackoverflow.com/questions/52175302/handling-expired-refresh-tokens-in-asp-net-core
                                   * see https://developer.spotify.com/documentation/general/guides/authorization-guide/#authorization-code-flow
                                   * could be useful to debug https://stackoverflow.com/questions/18924996/logging-request-response-messages-when-using-httpclient
                                   */

                                  //get the users tokens
                                  var tokens = context.Properties.GetTokens().ToList();
                                  var refreshToken = tokens.First(t => t.Name == "refresh_token");
                                  var accessToken = tokens.First(t => t.Name == "access_token");
                                  var exp = tokens.First(t => t.Name == "expires_at");
                                  var expires = DateTime.Parse(exp.Value);

                                  //check to see if the token has expired
                                  if (expires < DateTime.Now)
                                  {
                                      //token is expired, let's attempt to renew
                                      var tokenResponse = await sas.TryRefreshTokenAsync(refreshToken.Value);

                                      //set new token values
                                      if (string.IsNullOrWhiteSpace(tokenResponse.refresh_token)) refreshToken.Value = tokenResponse.refresh_token;
                                      accessToken.Value = tokenResponse.access_token;

                                      //set new expiration date
                                      var newExpires = DateTime.UtcNow + TimeSpan.FromSeconds(tokenResponse.expires_in);
                                      exp.Value = newExpires.ToString("o", CultureInfo.InvariantCulture);

                                      //set tokens in auth properties
                                      context.Properties.StoreTokens(tokens);

                                      //trigger context to renew cookie with new token values
                                      context.ShouldRenew = true;
                                  }

                                  // set new api
                                  sas.StoreMemberApi(context.Principal.Claims.GetSpotifyUsername(), accessToken.Value.FromAccessToken(), expires);

                                  // store latest tokens
                                  sas.StoreToken(context.Principal.Claims.GetSpotifyUsername(), refreshToken.Value);
                              }
                              catch (Exception e)
                              {
                                  Debug.WriteLine(e);
                                  context.RejectPrincipal();
                              }
                          }
                      }
                  };
              })
              .AddSpotify(options =>
              {
                  var scopes = new[]
                  {
                      Scopes.AppRemoteControl,
                      Scopes.UserReadPlaybackState,
                      Scopes.UserModifyPlaybackState,
                      Scopes.PlaylistModifyPrivate,
                      Scopes.PlaylistReadPrivate,
                      Scopes.PlaylistModifyPublic,
                      Scopes.PlaylistReadCollaborative,
                      Scopes.UserLibraryRead,
                      Scopes.UserLibraryModify,
                      Scopes.UserReadPrivate,
                      Scopes.UserReadCurrentlyPlaying
                  };
                  foreach (var scope in scopes) options.Scope.Add(scope);
                  options.ClientId = configuration["SpotifyClientId"];
                  options.ClientSecret = configuration["SpotifyClientSecret"];
                  options.CallbackPath = "/callback";
                  options.Events.OnRemoteFailure = context => Task.CompletedTask; // TODO handle rip
                  options.SaveTokens = true;
                  options.Events.OnTicketReceived = context => Task.CompletedTask; // maybe add log?
                  options.Events.OnCreatingTicket = context =>
                  {
                      var username = context.Principal.Claims.GetSpotifyUsername();
                      var sas = context.HttpContext.RequestServices.GetService<SpotifyAccessService>();
                      sas.StoreToken(username, context.RefreshToken);
                      sas.StoreMemberApi(username, context.AccessToken.FromAccessToken());
                      return Task.CompletedTask;
                  };
              });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        private static void Configure(WebApplication app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.Use((context, next) =>
            {
                // disable google's floc cancer
                context.Response.Headers.Append("Permissions-Policy", "interest-cohort=()");
                return next.Invoke();
            });

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseSerilogRequestLogging(options =>
            {
                options.EnrichDiagnosticContext = EnrichDiagnosticContext;
            });

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(name: "internal",
                pattern: "internal/{action=Index}/{id?}",
                defaults: new { controller = "Internal", action = "Index" });

            app.MapControllerRoute(name: "external",
                pattern: "{action=Index}/{id?}",
                defaults: new { controller = "External", action = "Index" });

            app.MapHub<RoomHub>("/roomhub");
        }

        private static void EnrichDiagnosticContext(IDiagnosticContext diagnosticContext, HttpContext httpContext)
        {
            // add common
            diagnosticContext.Set("Protocol", httpContext.Request.Protocol);
            diagnosticContext.Set("Scheme", httpContext.Request.Scheme);

            // add user-agent
            if (httpContext.Request.Headers.TryGetValue(HeaderNames.UserAgent, out var userAgent))
            {
                diagnosticContext.Set("UserAgent", userAgent.ToString());
            }

            // add spotify username
            var username = httpContext.User.Claims.GetSpotifyUsername();
            if (!string.IsNullOrWhiteSpace(username))
            {
                diagnosticContext.Set("Username", username);
            }
        }
    }
}