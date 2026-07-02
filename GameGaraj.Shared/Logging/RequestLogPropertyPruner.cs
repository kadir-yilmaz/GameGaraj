using Serilog.Core;
using Serilog.Events;

namespace GameGaraj.Shared.Logging
{
    public sealed class RequestLogPropertyPruner : ILogEventEnricher
    {
        private static readonly HashSet<string> AllowedProperties = new(StringComparer.Ordinal)
        {
            "Service",
            "Environment",
            "RequestMethod",
            "RequestPath",
            "StatusCode",
            "Elapsed",
            "TraceId",
            "SpanId",
            "UserIdentity",
            "UserId",
            "UserAgent"
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
