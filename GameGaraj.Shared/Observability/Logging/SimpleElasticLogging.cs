using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace GameGaraj.Shared.Observability;

/// <summary>
/// Minimal, dependency-free Elasticsearch logger that writes to a single fixed index per service:
/// gamegaraj-{serviceName}-logs (no TTL, no daily rotation).
/// </summary>
public static class SimpleElasticLoggingExtensions
{
    public static WebApplicationBuilder AddSimpleElasticLogging(this WebApplicationBuilder builder, string serviceName)
    {
        var esUri = builder.Configuration["ElasticSearchSettings:Uri"]
                    ?? builder.Configuration["ObservabilitySettings:ElasticSearchUri"];

        if (string.IsNullOrWhiteSpace(esUri) || esUri == "disabled")
        {
            return builder;
        }

        var indexName = $"gamegaraj-{serviceName.ToLowerInvariant()}-logs";
        builder.Logging.AddProvider(new ElasticsearchLoggerProvider(esUri.TrimEnd('/'), indexName, serviceName));
        return builder;
    }
}

internal sealed class ElasticsearchLoggerProvider : ILoggerProvider
{
    private readonly string _bulkUrl;
    private readonly string _indexActionJson;
    private readonly string _serviceName;
    private readonly HttpClient _httpClient;
    private readonly Channel<string> _queue;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    public ElasticsearchLoggerProvider(string esUri, string indexName, string serviceName)
    {
        _bulkUrl = $"{esUri}/_bulk";
        _indexActionJson = JsonSerializer.Serialize(new { index = new { _index = indexName } });
        _serviceName = serviceName;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        _queue = Channel.CreateBounded<string>(new BoundedChannelOptions(2048)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

        _worker = Task.Run(FlushLoopAsync);
    }

    public ILogger CreateLogger(string categoryName) => new ElasticsearchLogger(this, categoryName);

    internal void Enqueue(LogLevel level, string category, string message, Exception? exception)
    {
        var doc = new
        {
            @timestamp = DateTime.UtcNow.ToString("O"),
            level = level.ToString(),
            service = _serviceName,
            category,
            message,
            exception = exception?.ToString(),
            traceId = Activity.Current?.TraceId.ToString()
        };

        _queue.Writer.TryWrite(JsonSerializer.Serialize(doc));
    }

    private async Task FlushLoopAsync()
    {
        var batch = new List<string>(50);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                while (batch.Count < 50 && _queue.Reader.TryRead(out var line))
                {
                    batch.Add(line);
                }

                if (batch.Count > 0)
                {
                    await SendBatchAsync(batch);
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }

    private async Task SendBatchAsync(List<string> docs)
    {
        var sb = new StringBuilder(docs.Count * 256);
        foreach (var doc in docs)
        {
            sb.Append(_indexActionJson).Append('\n');
            sb.Append(doc).Append('\n');
        }

        try
        {
            using var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/x-ndjson");
            await _httpClient.PostAsync(_bulkUrl, content, _cts.Token);
        }
        catch
        {
            // Never crash the app if Elasticsearch is temporarily unreachable
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _queue.Writer.TryComplete();
        try { _worker.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
        _httpClient.Dispose();
        _cts.Dispose();
    }
}

internal sealed class ElasticsearchLogger : ILogger
{
    private readonly ElasticsearchLoggerProvider _provider;
    private readonly string _category;

    public ElasticsearchLogger(ElasticsearchLoggerProvider provider, string category)
    {
        _provider = provider;
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel)
    {
        if (logLevel == LogLevel.None) return false;

        // Avoid noisy ASP.NET/EF/MassTransit info logs; keep Warning+ for framework, Information+ for app code.
        if (_category.StartsWith("Microsoft.", StringComparison.Ordinal) ||
            _category.StartsWith("System.", StringComparison.Ordinal))
        {
            return logLevel >= LogLevel.Warning;
        }

        return logLevel >= LogLevel.Information;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message) && exception == null) return;

        _provider.Enqueue(logLevel, _category, message, exception);
    }
}
