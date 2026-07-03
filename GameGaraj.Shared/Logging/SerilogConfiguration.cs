using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
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
            var indexFormat = $"gamegaraj-logs-{serviceSlug}-{environmentSlug}";

            if (!string.IsNullOrEmpty(elasticUri))
            {
                EnsureElasticsearchLogIndex(elasticUri, indexFormat);
            }

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
                        IndexFormat = indexFormat,
                        NumberOfReplicas = 0,
                        NumberOfShards = 1
                    }));
            }

            Log.Logger = loggerConfig.CreateLogger();
            builder.Host.UseSerilog();
        }

        private static void EnsureElasticsearchLogIndex(string elasticUri, string indexName)
        {
            try
            {
                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(3)
                };

                var indexUrl = $"{elasticUri.TrimEnd('/')}/{indexName}";
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, indexUrl);
                using var headResponse = httpClient.Send(headRequest);

                if (headResponse.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }

                if (headResponse.StatusCode != HttpStatusCode.NotFound)
                {
                    Console.Error.WriteLine($"Unable to verify Elasticsearch log index {indexName}. StatusCode={headResponse.StatusCode}");
                    return;
                }

                const string body = """
                {
                  "settings": {
                    "number_of_shards": 1,
                    "number_of_replicas": 0
                  }
                }
                """;

                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var putResponse = httpClient.PutAsync(indexUrl, content).GetAwaiter().GetResult();

                if (!putResponse.IsSuccessStatusCode &&
                    putResponse.StatusCode != HttpStatusCode.BadRequest)
                {
                    Console.Error.WriteLine($"Unable to create Elasticsearch log index {indexName}. StatusCode={putResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unable to ensure Elasticsearch log index {indexName}: {ex.Message}");
            }
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
