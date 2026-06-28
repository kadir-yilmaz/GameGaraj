using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Exceptions;
using Serilog.Sinks.Elasticsearch;
using Serilog.Enrichers.Span;

namespace GameGaraj.Shared.Logging
{
    public static class SerilogConfiguration
    {
        public static void AddSerilogLogging(this WebApplicationBuilder builder, string serviceName)
        {
            var environment = builder.Environment.EnvironmentName;
            var elasticUri = builder.Configuration["ElasticSearchSettings:Uri"];

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(LogLevelManager.GetSwitch(serviceName))
                .Enrich.FromLogContext()
                .Enrich.WithExceptionDetails()
                .Enrich.WithProperty("Environment", environment)
                .Enrich.WithProperty("Service", serviceName)
                .Enrich.WithProperty("MachineName", Environment.MachineName)
                .Enrich.WithSpan()  // Adds TraceId + SpanId from Activity (OpenTelemetry)
                .WriteTo.Console();

            // Fallback file logging - daily rolling, automatically purged after 7 days
            loggerConfig.WriteTo.File(
                path: $"../ConsoleLogs/serilog-{serviceName.ToLower()}-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7
            );

            // Elasticsearch logging (if configured)
            if (!string.IsNullOrEmpty(elasticUri))
            {
                loggerConfig.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUri))
                {
                    AutoRegisterTemplate = true,
                    IndexFormat = $"gamegaraj-logs-{serviceName.ToLower()}-{environment.ToLower()}-{DateTime.UtcNow:yyyy.MM}",
                    NumberOfReplicas = 0,
                    NumberOfShards = 1
                });
            }

            Log.Logger = loggerConfig.CreateLogger();

            // Override .NET generic host logger provider with Serilog
            builder.Host.UseSerilog();
        }
    }
}
