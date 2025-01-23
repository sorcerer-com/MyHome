using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using JWT.Algorithms;
using JWT.Builder;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NLog;
using NLog.Web;

namespace MyHome
{
    public static class Program
    {
        private static readonly Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

        public static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                logger.Debug("Init main");
                var builder = WebApplication.CreateBuilder(args);

                // setup logging
                builder.Logging.ClearProviders();
                builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                builder.Host.UseNLog();

                // setup services
                ConfigureServices(builder.Services);

                var app = builder.Build();
                Configure(app, app.Services.GetService<MyHome>());
                app.Run();
            }
            catch (Exception exception)
            {
                //NLog: catch setup errors
                logger.Error("Stopped program because of exception");
                logger.Debug(exception);
                throw;
            }
            finally
            {
                // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
                LogManager.Shutdown();
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(new MyHome());
            services.AddControllers();
            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(2);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });
            services.AddResponseCompression();
            services.AddOpenApi();
        }

        private static void Configure(WebApplication app, MyHome myHome)
        {
            if (app.Environment.IsDevelopment())
            {
                logger.Warn("Running in development mode");
                app.UseDeveloperExceptionPage();
            }

            app.UseSession();
            app.UseResponseCompression();

            app.Use(async (context, next) =>
            {
                if (Login(context, myHome))
                    await next();
            });

            var fileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "UI"));
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider,
                OnPrepareResponse = context =>
                {
                    // allow cache for 1 day
                    context.Context.Response.Headers.CacheControl = "private, max-age=86400, stale-while-revalidate=86400";
                }
            });

            app.UseWebSockets();
            app.UseRouting();

            app.MapControllers();
            app.MapOpenApi();

            app.Lifetime.ApplicationStopping.Register(myHome.Stop);
        }


        private static bool Login(HttpContext context, MyHome myHome)
        {
            if (context.Request.Path == "/login" && context.Request.Method == "POST")
            {
                return Authenticate(context, myHome);
            }
            else if (!ShouldSkipAuthentication(context.Request.Path) && !Authenticated(context, myHome))
            {
                if (!context.Request.Path.StartsWithSegments("/api")) // pages only
                    context.Response.Redirect("./login.html");
                else
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Session.Clear();
                return false;
            }
            context.Session.SetString("time", DateTime.Now.ToString());
            return true;
        }

        private static bool Authenticate(HttpContext context, MyHome myHome)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(context.Request.Form["password"]));
            var hashStr = Convert.ToHexString(hash);
            if (myHome.Config.Password == hashStr)
            {
                if (context.Request.Form.ContainsKey("token")) // generate JWT token
                {
                    var expiration = DateTimeOffset.UtcNow.Add(GetExpirationTime(context, myHome));
                    var token = JwtBuilder.Create()
                              .WithAlgorithm(new HMACSHA256Algorithm())
                              .WithSecret(myHome.Config.Password)
                              .AddClaim("exp", expiration.ToUnixTimeSeconds())
                              .Encode();
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(token));
                }
                else
                {
                    logger.Info("LogIn: Correct password");
                    context.Session.SetString("password", hashStr);
                    context.Response.Redirect("./");
                }
            }
            else
            {
                logger.Warn("LogIn: Incorrect password");
                context.Response.Redirect("./login.html?invalid");
            }

            return false;
        }


        private static bool ShouldSkipAuthentication(PathString path)
        {
            var isResource = path.StartsWithSegments("/external") || path.StartsWithSegments("/images") ||
                path == "/scripts.js" || path == "/style.css" || path == "/login.html";
            return isResource || path.StartsWithSegments("/api/status") || path.StartsWithSegments("/api/songs");
        }

        private static bool Authenticated(HttpContext context, MyHome myHome)
        {
            // JWT token authentication
            if (!string.IsNullOrEmpty(context.Request.Headers.Authorization))
            {
                try
                {
                    string token = JwtBuilder.Create()
                        .WithAlgorithm(new HMACSHA256Algorithm())
                        .WithSecret(myHome.Config.Password)
                        .MustVerifySignature()
                        .Decode(context.Request.Headers.Authorization);
                    return !string.IsNullOrEmpty(token);
                }
                catch
                {
                    return false;
                }
            }

            // session authentication
            return (context.Session.GetString("password") ?? "") == myHome.Config.Password && !IsSessionExpired(context, myHome);
        }

        private static bool IsSessionExpired(HttpContext context, MyHome myHome)
        {
            if (!context.Session.Keys.Contains("time"))
                return false;
            var time = DateTime.Parse(context.Session.GetString("time"), CultureInfo.CurrentCulture);
            var duration = GetExpirationTime(context, myHome);
            return DateTime.Now - time > duration;
        }

        private static TimeSpan GetExpirationTime(HttpContext context, MyHome myHome)
        {
            var ip = context.Connection.RemoteIpAddress.MapToIPv4();
            var knownIp = myHome.SecuritySystem.PresenceDeviceIPs.ContainsValue(ip.ToString());
            return knownIp ? TimeSpan.FromMinutes(120) : TimeSpan.FromMinutes(15);// if known device set session duration to 2h
        }
    }
}
