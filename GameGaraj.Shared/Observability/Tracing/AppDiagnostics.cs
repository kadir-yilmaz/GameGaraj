using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace GameGaraj.Shared.Observability
{
    /// <summary>
    /// Sipariş akışı adımları için manuel span yardımcısı.
    /// Tüm span'ler OrderTracing.Source ("GameGaraj.OrderFlow") üzerinden açılır.
    /// </summary>
    public static class AppDiagnostics
    {
        // Jaeger'da gürültü yapan, tek başına anlamı olmayan ara adımlar.
        private static readonly HashSet<string> SkippedSpans = new(StringComparer.OrdinalIgnoreCase)
        {
            "Build Order Pricing Snapshot",
            "Build Payment Pricing Snapshot",
            "Build Payment Request",
            "Build Iyzico Request",
            "Build Order Aggregate"
        };

        public static ActivitySource ActivitySource => OrderTracing.Source;

        public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
            => SkippedSpans.Contains(name) ? null : OrderTracing.Source.StartActivity(name, kind);

        public static Activity? StartActivity(string name, ActivityKind kind, ActivityContext parentContext)
            => SkippedSpans.Contains(name) ? null : OrderTracing.Source.StartActivity(name, kind, parentContext);
    }
}
