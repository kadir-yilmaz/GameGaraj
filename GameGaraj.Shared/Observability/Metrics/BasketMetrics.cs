using System.Diagnostics.Metrics;

namespace GameGaraj.Shared.Observability.Metrics
{
    /// <summary>
    /// Business metrics for Basket Service.
    /// Tracks basket item additions, removals, and checkout events.
    /// </summary>
    public sealed class BasketMetrics
    {
        private readonly Counter<long> _itemsAdded;
        private readonly Counter<long> _itemsRemoved;
        private readonly Counter<long> _favoritesAdded;
        private readonly Counter<long> _favoritesRemoved;

        public BasketMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create("GameGaraj.Basket");

            _itemsAdded = meter.CreateCounter<long>(
                "basket.items.added.total", null, "Total items added to baskets");

            _itemsRemoved = meter.CreateCounter<long>(
                "basket.items.removed.total", null, "Total items removed from baskets");

            _favoritesAdded = meter.CreateCounter<long>(
                "basket.favorites.added.total", null, "Total favorites added");

            _favoritesRemoved = meter.CreateCounter<long>(
                "basket.favorites.removed.total", null, "Total favorites removed");

            _itemsAdded.Add(0);
            _itemsRemoved.Add(0);
            _favoritesAdded.Add(0);
            _favoritesRemoved.Add(0);
        }

        public void ItemAdded() => _itemsAdded.Add(1);
        public void ItemRemoved() => _itemsRemoved.Add(1);
        public void FavoriteAdded() => _favoritesAdded.Add(1);
        public void FavoriteRemoved() => _favoritesRemoved.Add(1);
    }
}
