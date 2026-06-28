using System.Diagnostics.Metrics;

namespace GameGaraj.Shared.Observability.Metrics
{
    /// <summary>
    /// Business metrics for PhotoStock Service.
    /// Tracks photo uploads, downloads, and storage operations.
    /// </summary>
    public sealed class PhotoStockMetrics
    {
        private readonly Counter<long> _photosUploaded;
        private readonly Counter<long> _photosDeleted;
        private readonly Counter<long> _uploadsFailed;
        private readonly Histogram<double> _uploadDuration;

        public PhotoStockMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create("GameGaraj.PhotoStock");

            _photosUploaded = meter.CreateCounter<long>(
                "photostock.photos.uploaded.total", "photos", "Total photos uploaded");

            _photosDeleted = meter.CreateCounter<long>(
                "photostock.photos.deleted.total", "photos", "Total photos deleted");

            _uploadsFailed = meter.CreateCounter<long>(
                "photostock.uploads.failed.total", "uploads", "Total failed photo uploads");

            _uploadDuration = meter.CreateHistogram<double>(
                "photostock.upload.duration", "ms", "Photo upload duration in milliseconds");
        }

        public void PhotoUploaded() => _photosUploaded.Add(1);
        public void PhotoDeleted() => _photosDeleted.Add(1);
        public void UploadFailed() => _uploadsFailed.Add(1);
        public TrackedDuration TrackUpload() => new(_uploadDuration);
    }
}
