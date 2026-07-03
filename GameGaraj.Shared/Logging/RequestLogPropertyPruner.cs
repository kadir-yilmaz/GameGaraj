using Serilog.Core;
using Serilog.Events;

namespace GameGaraj.Shared.Logging
{
    public sealed class RequestLogPropertyPruner : ILogEventEnricher
    {
        private static readonly HashSet<string> AllowedProperties = new(StringComparer.Ordinal)
        {
            "LogType",
            "Event",
            "Operation",
            "Service",
            "Environment",
            "RequestMethod",
            "RequestPath",
            "RequestArea",
            "IncomingPath",
            "TargetPath",
            "TargetService",
            "StatusCode",
            "Elapsed",
            "DurationMs",
            "TraceId",
            "SpanId",
            "UserIdentity",
            "UserId",
            "SearchTerm",
            "SearchQuery",
            "Source",
            "ResultCount",
            "Count",
            "Page",
            "PageSize",
            "CacheHit",
            "EsDurationMs",
            "UsedFallback",
            "Reason",
            "DependencyType"
        };

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            foreach (var propertyName in logEvent.Properties.Keys.ToList())
            {
                if (!AllowedProperties.Contains(propertyName))
                {
                    logEvent.RemovePropertyIfPresent(propertyName);
                }
            }
        }
    }
}
