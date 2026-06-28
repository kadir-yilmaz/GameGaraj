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
        private readonly Counter<long> _basketsDeleted;
        private readonly Counter<long> _favoritesAdded;
        private readonly Counter<long> _favoritesRemoved;

        public BasketMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create("GameGaraj.Basket");

            _itemsAdded = meter.CreateCounter<long>(
                "basket.items.added.total", "items", "Total items added to baskets");

            _itemsRemoved = meter.CreateCounter<long>(
                "basket.items.removed.total", "items", "Total items removed from baskets");

            _basketsDeleted = meter.CreateCounter<long>(
                "basket.deleted.total", "baskets", "Total baskets deleted");

            _favoritesAdded = meter.CreateCounter<long>(
                "basket.favorites.added.total", "favorites", "Total favorites added");

            _favoritesRemoved = meter.CreateCounter<long>(
                "basket.favorites.removed.total", "favorites", "Total favorites removed");
        }

        public void ItemAdded() => _itemsAdded.Add(1);
        public void ItemRemoved() => _itemsRemoved.Add(1);
        public void BasketDeleted() => _basketsDeleted.Add(1);
        public void FavoriteAdded() => _favoritesAdded.Add(1);
        public void FavoriteRemoved() => _favoritesRemoved.Add(1);
    }
}
