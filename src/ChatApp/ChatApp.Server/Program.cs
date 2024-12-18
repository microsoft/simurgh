using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ChatApp.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOptions(builder.Configuration);
        // Add services to the container.
        builder.Services.AddAuthorization();

        #region OpenTelemetry

        var connectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);

            // Add OpenTelemetry and configure it to use Azure Monitor.
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(serviceName: builder.Environment.ApplicationName))
                .WithTracing(tracing => tracing
                    .AddSource("Microsoft.SemanticKernel*")
                    .AddAspNetCoreInstrumentation())
                .WithMetrics(metrics => metrics
                    .AddMeter("Microsoft.SemanticKernel*")
                    .AddAspNetCoreInstrumentation())
                .UseAzureMonitor();

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                // Add OpenTelemetry as a logging provider
                builder.AddOpenTelemetry(options =>
                {
                    options.AddAzureMonitorLogExporter(options => options.ConnectionString = connectionString);
                    // Format log messages. This is default to false.
                    options.IncludeFormattedMessage = true;
                });
                builder.SetMinimumLevel(LogLevel.Information);
            });
        }

        #endregion

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // for making http requests
        builder.Services.AddHttpClient();

        // Register all of our things from ChatAppExtensions
        builder.Services.AddChatAppServices(builder.Configuration);

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapApiEndpoints();
        app.MapHistoryEndpoints();

        app.MapFallbackToFile("/index.html");

        app.Run();
    }
}
