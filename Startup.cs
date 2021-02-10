using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MyHome.Systems;
using MyHome.Systems.Devices;

using NLog;
using NLog.Targets;

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
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostApplicationLifetime applicationLifetime, IWebHostEnvironment env,
            MyHome myHome)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                // TODO: remove
                var system = (DevicesSystem)myHome.Systems["DevicesSystem"];
                if (system.Devices.Count == 0)
                    system.Devices.Add(new MyMultiSensor(system, "test", "test", "test"));
                if (system.Devices.Count == 1)
                    system.Devices.Add(new Camera(system, "camera", "camera", "username:password@http://192.168.0.120:8899"));
                endpoints.MapGet("/", async context =>
                {
                    logger.Info("Test");
                    var result = "Hello World!\n";

                    var logs = LogManager.Configuration.FindTargetByName<MemoryTarget>("memory").Logs;
                    result += string.Join("\n", logs);
                    await context.Response.WriteAsync(result);
                });
                endpoints.MapGet("/stop", async context =>
                {
                    Environment.Exit(0);
                    await context.Response.WriteAsync("OK");
                });
            });

            applicationLifetime.ApplicationStopping.Register(() => myHome.Stop());
        }
    }
}
