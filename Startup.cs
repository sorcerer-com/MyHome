using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

using NLog;

namespace MyHome
{
    public class Startup
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(new MyHome());
            services.AddControllers();
            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(15);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });
            services.AddResponseCompression();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostApplicationLifetime applicationLifetime, IWebHostEnvironment env,
            MyHome myHome)
        {
            if (env.IsDevelopment())
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

            var fileProvider = new PhysicalFileProvider(Path.Combine(env.ContentRootPath, "UI"));
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider,
                OnPrepareResponse = context =>
                {
                    // allow cache for 1 day
                    context.Context.Response.Headers.Add("Cache-Control", "private, max-age=86400, stale-while-revalidate=86400");
                }
            });

            app.UseWebSockets();
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            applicationLifetime.ApplicationStopping.Register(() => myHome.Stop());
        }

        private static bool Login(HttpContext context, MyHome myHome)
        {
            if (context.Request.Path == "/login" && context.Request.Method == "POST")
            {
                using var shar256 = SHA256.Create();
                var hash = shar256.ComputeHash(Encoding.UTF8.GetBytes(context.Request.Form["password"]));
                var hashStr = BitConverter.ToString(hash).Replace("-", string.Empty);
                if (myHome.Config.Password == hashStr)
                {
                    logger.Info("LogIn: Correct password");
                    context.Session.SetString("password", hashStr);
                    context.Response.Redirect("./");
                }
                else
                {
                    logger.Warn("LogIn: Incorrect password");
                    context.Response.Redirect("./login.html?invalid");
                }

                return false;
            }
            else if (!IsResource(context.Request.Path) && !context.Request.Path.StartsWithSegments("/api/systems/MediaPlayer/songs") &&
                (context.Session.GetString("password") ?? "") != myHome.Config.Password)
            {
                if (!context.Request.Path.StartsWithSegments("/api")) // pages only
                    context.Response.Redirect("./login.html");
                else
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return false;
            }
            return true;
        }

        private static bool IsResource(string path)
        {
            return path.StartsWith("/external/") || path.StartsWith("/images/") ||
                path == "/scripts.js" || path == "/style.css" || path == "/login.html";
        }
    }
}
