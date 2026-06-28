using System.Diagnostics.Metrics;

namespace GameGaraj.Shared.Observability.Metrics
{
    /// <summary>
    /// Business metrics for Invoice Service.
    /// Tracks invoice generation, email sending, and failures.
    /// </summary>
    public sealed class InvoiceMetrics
    {
        private readonly Counter<long> _invoicesGenerated;
        private readonly Counter<long> _emailsSent;
        private readonly Counter<long> _emailsFailed;
        private readonly Histogram<double> _invoiceGenerationDuration;

        public InvoiceMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create("GameGaraj.Invoice");

            _invoicesGenerated = meter.CreateCounter<long>(
                "invoices.generated.total", "invoices", "Total invoices generated");

            _emailsSent = meter.CreateCounter<long>(
                "invoices.emails.sent.total", "emails", "Total invoice emails sent");

            _emailsFailed = meter.CreateCounter<long>(
                "invoices.emails.failed.total", "emails", "Total invoice emails failed");

            _invoiceGenerationDuration = meter.CreateHistogram<double>(
                "invoices.generation.duration", "ms", "Invoice generation duration in milliseconds");
        }

        public void InvoiceGenerated() => _invoicesGenerated.Add(1);
        public void EmailSent() => _emailsSent.Add(1);
        public void EmailFailed() => _emailsFailed.Add(1);
        public TrackedDuration TrackGeneration() => new(_invoiceGenerationDuration);
    }
}
