using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Server.Kestrel.Core;
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
using SpotifyAPI.Web;
using SpotifyAPI.Web.Enums;

namespace PugetSound
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite("Data Source=pugetsound.sqlite");
            });

            services.AddSingleton<StatisticsService>();
            services.AddSingleton<UserScoreService>();
            services.AddSingleton<RoomService>();

            services.AddControllersWithViews(configure =>
            {
                configure.Conventions.Add(new RouteTokenTransformerConvention(new LowerCaseParameterTransformer()));
            });

            services.AddSignalR();

            services.AddSingleton(serviceProvider =>
            {
                var logger = serviceProvider.GetService<ILogger<SpotifyAccessService>>();
                var sas = new SpotifyAccessService(logger);
                sas.SetAccessKeys(Configuration["SpotifyClientId"], Configuration["SpotifyClientSecret"]);
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
                  var scopes = Scope.AppRemoteControl
                               | Scope.UserReadPlaybackState
                               | Scope.UserModifyPlaybackState
                               | Scope.PlaylistModifyPrivate
                               | Scope.PlaylistReadPrivate
                               | Scope.PlaylistModifyPublic
                               | Scope.PlaylistReadCollaborative
                               | Scope.UserLibraryRead
                               | Scope.UserLibraryModify
                               | Scope.UserReadPrivate
                               | Scope.UserReadCurrentlyPlaying;
                  options.Scope.Add(scopes.GetStringAttribute(","));
                  options.ClientId = Configuration["SpotifyClientId"];
                  options.ClientSecret = Configuration["SpotifyClientSecret"];
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
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
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
                context.Response.Headers.Add("Permissions-Policy", "interest-cohort=()");
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

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(name: "internal",
                    pattern: "internal/{action=Index}/{id?}",
                    defaults: new { controller = "Internal", action = "Index" });

                endpoints.MapControllerRoute(name: "external",
                    pattern: "{action=Index}/{id?}",
                    defaults: new { controller = "External", action = "Index" });

                //endpoints.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
                //endpoints.MapControllerRoute(name: "default", pattern: "{controller}{action=Index}/{id?}", defaults: new { controller = "Home", action = "Index"});

                endpoints.MapHub<RoomHub>("/roomhub");
            });
        }

        private void EnrichDiagnosticContext(IDiagnosticContext diagnosticContext, HttpContext httpContext)
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