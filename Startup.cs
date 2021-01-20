using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;
using NLog.Targets;

namespace MyHome
{
    public class Startup
    {
        private readonly ILogger logger = LogManager.GetCurrentClassLogger();

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<MyHome>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                // TODO: remove
                var myHome = new MyHome();
                endpoints.MapGet("/", async context =>
                {
                    logger.Info("Test");
                    var result = "Hello World!\n";

                    var logs = LogManager.Configuration.FindTargetByName<MemoryTarget>("memory").Logs;
                    result += string.Join("\n", logs);
                    myHome.Save();
                    await context.Response.WriteAsync(result);
                });
                endpoints.MapGet("/save", async context =>
                {
                    myHome.Save();
                    await context.Response.WriteAsync("OK");
                });
            });
        }
    }
}
