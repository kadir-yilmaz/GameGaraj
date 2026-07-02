using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
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
            var serviceSlug = serviceName.ToLowerInvariant().Replace(".", "-");
            var environmentSlug = environment.ToLowerInvariant();

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(LogLevelManager.GetSwitch(serviceName))
                .Enrich.FromLogContext()
                .Enrich.WithExceptionDetails()
                .Enrich.WithProperty("Environment", environment)
                .Enrich.WithProperty("Service", serviceName)
                .Enrich.WithProperty("MachineName", Environment.MachineName)
                .Enrich.WithSpan()
                .WriteTo.Console()
                .WriteTo.File(
                    path: $"../ConsoleLogs/serilog-{serviceSlug}-.txt",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7);

            if (!string.IsNullOrEmpty(elasticUri))
            {
                loggerConfig.WriteTo.Logger(logs => logs
                    .Filter.ByIncludingOnly(evt =>
                        evt.Properties.TryGetValue("LogType", out var logType) &&
                        logType is ScalarValue sv &&
                        sv.Value?.ToString() == "HttpRequest")
                    .Enrich.With<RequestLogPropertyPruner>()
                    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUri))
                    {
                        AutoRegisterTemplate = true,
                        IndexFormat = $"gamegaraj-logs-{serviceSlug}-{environmentSlug}",
                        NumberOfReplicas = 0,
                        NumberOfShards = 1
                    }));
            }

            Log.Logger = loggerConfig.CreateLogger();
            builder.Host.UseSerilog();
        }
    }
}
