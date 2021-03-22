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
                    var path = context.Context.Request.Path.Value;
                    if (path.EndsWith(".html") && path.LastIndexOf("/") == 0) // disable cache for pages only
                    {
                        context.Context.Response.Headers.Add("Cache-Control", "no-cache, no-store");
                        context.Context.Response.Headers.Add("Expires", "-1");
                    }
                }
            });

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
                    context.Response.Redirect("/");
                }
                else
                {
                    context.Response.Redirect("/login.html?invalid");
                }

                return false;
            }
            else if ((context.Session.GetString("password") ?? "") != myHome.Config.Password && !IsResouce(context.Request.Path) &&
                context.Request.Path != "/api/sensor/data")
            {
                if (!context.Request.Path.StartsWithSegments("/api")) // pages only
                    context.Response.Redirect("/login.html");
                else
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return false;
            }
            return true;
        }

        private static bool IsResouce(string path)
        {
            return path.StartsWith("/external/") || path.StartsWith("/images/") ||
                path == "/scripts.js" || path == "/style.css" || path == "/login.html";
        }
    }
}
