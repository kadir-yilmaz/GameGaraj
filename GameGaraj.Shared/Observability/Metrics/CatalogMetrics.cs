using System.Diagnostics.Metrics;

namespace GameGaraj.Shared.Observability.Metrics
{
    /// <summary>
    /// Business metrics for Catalog Service.
    /// Tracks product searches, stock reservations, and category operations.
    /// </summary>
    public sealed class CatalogMetrics
    {
        private readonly Counter<long> _productSearches;
        private readonly Counter<long> _stockReserved;
        private readonly Counter<long> _stockReservationFailed;
        private readonly Counter<long> _productCreated;
        private readonly Counter<long> _productUpdated;
        private readonly Histogram<double> _searchDuration;

        public CatalogMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create("GameGaraj.Catalog");

            _productSearches = meter.CreateCounter<long>(
                "catalog.product.searches.total", "searches", "Total product searches");

            _stockReserved = meter.CreateCounter<long>(
                "catalog.stock.reserved.total", "reservations", "Total stock reservations");

            _stockReservationFailed = meter.CreateCounter<long>(
                "catalog.stock.reservation.failed.total", "failures", "Total failed stock reservations");

            _productCreated = meter.CreateCounter<long>(
                "catalog.product.created.total", "products", "Total products created");

            _productUpdated = meter.CreateCounter<long>(
                "catalog.product.updated.total", "products", "Total products updated");

            _searchDuration = meter.CreateHistogram<double>(
                "catalog.search.duration", "ms", "Product search duration in milliseconds");
        }

        public void ProductSearched() => _productSearches.Add(1);
        public void StockReserved() => _stockReserved.Add(1);
        public void StockReservationFailed() => _stockReservationFailed.Add(1);
        public void ProductCreated() => _productCreated.Add(1);
        public void ProductUpdated() => _productUpdated.Add(1);
        public TrackedDuration TrackSearch() => new(_searchDuration);
    }
}
