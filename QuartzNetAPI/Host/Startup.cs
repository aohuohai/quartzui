using Host.Common;
using Host.Controllers;
using Host.Filters;
using Host.Managers;
using Host.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Host
{
    public class Startup
    {
        private string appDirectory = Path.Combine(AppContext.BaseDirectory, "File");

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // 日志配置
            LogConfig();

            AppConfig.DbProviderName = Configuration.GetValue<string>("Quartz:dbProviderName");
            AppConfig.ConnectionString = Configuration.GetValue<string>("Quartz:connectionString");
            EncryptDecryptExtension.des3key = Configuration.GetValue<string>("DES3Key", "73495773n~@^v&B6");

            #region 跨域     
            services.AddCors(options =>
            {
                options.AddPolicy("AllowSameDomain", policyBuilder =>
                {
                    policyBuilder
                        .AllowAnyMethod()
                        .AllowAnyHeader();

                    var allowedHosts = Configuration.GetSection("AllowedHosts").Get<List<string>>();
                    if (allowedHosts?.Any(t => t == "*") ?? false)
                        policyBuilder.AllowAnyOrigin(); //允许任何来源的主机访问
                    else if (allowedHosts?.Any() ?? false)
                        policyBuilder.AllowCredentials().WithOrigins(allowedHosts.ToArray()); //允许类似http://localhost:8080等主机访问
                });
            });

            //services.AddRouting(r => r.SuppressCheckForUnhandledSecurityMetadata = true);
            #endregion

            //services.AddMvc();
            services.AddControllersWithViews(
                t =>
                {
                    t.Filters.Add<AuthorizationFilter>();
                }).AddNewtonsoftJson();

            services.AddHostedService<HostedService>();
            services.AddSingleton<SchedulerCenter>();

            SetingController.refreshIntervalPath = Path.Combine(appDirectory, SetingController.refreshIntervalPath);
            SetingController.loginPasswordPath = Path.Combine(appDirectory, SetingController.loginPasswordPath);
            FileConfig.filePath = Path.Combine(appDirectory, FileConfig.filePath);
            FileConfig.mqttFilePath = Path.Combine(appDirectory, FileConfig.mqttFilePath);
            FileConfig.rabbitFilePath = Path.Combine(appDirectory, FileConfig.rabbitFilePath);

            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "MsSystem API"
                });

                //Determine base path for the application.  
                var basePath = PlatformServices.Default.Application.ApplicationBasePath;
                //Set the comments path for the swagger json and ui.  
                var xmlPath = Path.Combine(basePath, "Host.xml");
                options.IncludeXmlComments(xmlPath);
            });


        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //app.UseMvc();

            app.Use(async (context, next) =>
            {
                await next();
                if (context.Response.StatusCode == 404 &&
                   !Path.HasExtension(context.Request.Path.Value) &&
                   !context.Request.Path.Value.StartsWith("/api/"))
                {
                    context.Request.Path = "/index.html";
                    await next();
                }
            });

            //app.UseMvcWithDefaultRoute();

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "MsSystem API V1");
            });

            app.UseRouting();
            //https://docs.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-3.0
            app.UseCors("AllowSameDomain");
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        /// <summary>
        /// 日志配置
        /// </summary>      
        private void LogConfig()
        {
            string directory = Path.Combine(appDirectory, "logs") + Path.DirectorySeparatorChar;
            //nuget导入
            //Serilog.Extensions.Logging
            //Serilog.Sinks.RollingFile
            //Serilog.Sinks.Async
            var fileSize = 1024 * 1024 * 10;//10M
            var fileCount = 2;
            Log.Logger = new LoggerConfiguration()
                                 .Enrich.FromLogContext()
                                 //.MinimumLevel.Debug()
                                 .MinimumLevel.Information()
                                 .MinimumLevel.Override("System", LogEventLevel.Information)
                                 .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                                 .WriteTo.Logger(lg => lg.Filter.ByIncludingOnly(p => p.Level == LogEventLevel.Debug).WriteTo.Async(
                                     a =>
                                     {
                                         a.RollingFile(directory + "log-{Date}-Debug.txt", fileSizeLimitBytes: fileSize, retainedFileCountLimit: fileCount);
                                     }
                                 ))
                                 .WriteTo.Logger(lg => lg.Filter.ByIncludingOnly(p => p.Level == LogEventLevel.Information).WriteTo.Async(
                                     a =>
                                     {
                                         a.RollingFile(directory + "log-{Date}-Information.txt", fileSizeLimitBytes: fileSize, retainedFileCountLimit: fileCount);
                                     }
                                 ))
                                 .WriteTo.Logger(lg => lg.Filter.ByIncludingOnly(p => p.Level == LogEventLevel.Warning).WriteTo.Async(
                                     a =>
                                     {
                                         a.RollingFile(directory + "log-{Date}-Warning.txt", fileSizeLimitBytes: fileSize, retainedFileCountLimit: fileCount);
                                     }
                                 ))
                                 .WriteTo.Logger(lg => lg.Filter.ByIncludingOnly(p => p.Level == LogEventLevel.Error).WriteTo.Async(
                                     a =>
                                     {
                                         a.RollingFile(directory + "log-{Date}-Error.txt", fileSizeLimitBytes: fileSize, retainedFileCountLimit: fileCount);
                                     }
                                 ))
                                 .WriteTo.Logger(lg => lg.Filter.ByIncludingOnly(p => p.Level == LogEventLevel.Fatal).WriteTo.Async(
                                     a =>
                                     {
                                         a.RollingFile(directory + "log-{Date}-Fatal.txt", fileSizeLimitBytes: fileSize, retainedFileCountLimit: fileCount);

                                     }
                                 ))
                                 //所有情况
                                 .WriteTo.Logger(lg => lg.Filter.ByIncludingOnly(p => true)).WriteTo.Async(
                                     a =>
                                     {
                                         a.RollingFile(directory + "log-{Date}-All.txt", fileSizeLimitBytes: fileSize, retainedFileCountLimit: fileCount);
                                     }
                                 )
                                .CreateLogger();
        }
    }
}
