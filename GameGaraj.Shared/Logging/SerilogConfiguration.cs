using System;
using System.IO;
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
            // Enable Serilog SelfLog to help diagnose Elasticsearch/File sink issues
            try
            {
                var logDirectory = "../ConsoleLogs";
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
                var selfLogPath = Path.Combine(logDirectory, "serilog-selflog.txt");
                Serilog.Debugging.SelfLog.Enable(TextWriter.Synchronized(new StreamWriter(selfLogPath, true)));
            }
            catch
            {
                Serilog.Debugging.SelfLog.Enable(Console.Error);
            }

            var environment = builder.Environment.EnvironmentName;
            var elasticUri = builder.Configuration["ElasticSearchSettings:Uri"]
                             ?? builder.Configuration["ObservabilitySettings:ElasticSearchUri"];

            // Fallback to localhost:9201 in development if not configured
            if (string.IsNullOrEmpty(elasticUri) && builder.Environment.IsDevelopment())
            {
                elasticUri = "http://localhost:9201";
            }

            var serviceSlug = serviceName.ToLowerInvariant().Replace(".", "-");
            var environmentSlug = environment.ToLowerInvariant();

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(LogLevelManager.GetSwitch(serviceName))
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting", LogEventLevel.Warning)
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
                    .Filter.ByIncludingOnly(IsSearchableLog)
                    .Enrich.With<RequestLogPropertyPruner>()
                    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUri))
                    {
                        AutoRegisterTemplate = true,
                        AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
                        DetectElasticsearchVersion = true,
                        EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog |
                                           EmitEventFailureHandling.RaiseCallback,
                        FailureCallback = (evt, ex) =>
                            Console.Error.WriteLine($"Unable to submit log event to Elasticsearch: {evt.MessageTemplate}. {ex.Message}"),
                        RegisterTemplateFailure = RegisterTemplateRecovery.IndexAnyway,
                        IndexFormat = $"gamegaraj-logs-{serviceSlug}-{environmentSlug}",
                        NumberOfReplicas = 0,
                        NumberOfShards = 1
                    }));
            }

            Log.Logger = loggerConfig.CreateLogger();
            builder.Host.UseSerilog();
        }

        private static bool IsSearchableLog(LogEvent evt)
        {
            if (IsHttpRequestLog(evt))
            {
                return true;
            }

            if (evt.Properties.TryGetValue("LogType", out var logType) &&
                logType is ScalarValue logTypeValue)
            {
                var value = logTypeValue.Value?.ToString();
                return string.Equals(value, "BusinessRequest", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(value, "ExternalDependency", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool IsHttpRequestLog(LogEvent evt)
        {
            if (evt.Properties.TryGetValue("LogType", out var logType) &&
                logType is ScalarValue logTypeValue &&
                logTypeValue.Value?.ToString() == "HttpRequest")
            {
                return true;
            }

            return evt.Properties.TryGetValue("SourceContext", out var sourceContext) &&
                   sourceContext is ScalarValue sourceContextValue &&
                   sourceContextValue.Value?.ToString() == "Serilog.AspNetCore.RequestLoggingMiddleware";
        }
    }
}
