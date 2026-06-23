using GameGaraj.Campaign.API.Models;

namespace GameGaraj.Campaign.API.Rules
{
    /// <summary>
    /// Belirli bir kategoriden X adet alınca Y tanesi bedava.
    /// Örnek: Kategori 2'den 6 adet alınca 1 tanesi ücretsiz.
    /// En ucuz ürün(ler) bedava yapılır.
    /// </summary>
    public class BuyXGetYFreeRule : ICampaignRule
    {
        public string RuleType => "BuyXGetYFree";

        public CalculateDiscountResponse? Calculate(CalculateDiscountRequest request, CampaignRule rule)
        {
            if (rule.MinQuantity == null || rule.FreeQuantity == null)
                return null;

            // Filtreleme: Önce Ürün ID'sine, yoksa Kategori ID'sine bak
            var targetedItems = request.Items.AsEnumerable();

            if (!string.IsNullOrEmpty(rule.ProductId))
            {
                targetedItems = targetedItems.Where(i => i.ProductId.Equals(rule.ProductId, StringComparison.OrdinalIgnoreCase));
            }
            else if (!string.IsNullOrEmpty(rule.CategoryId))
            {
                targetedItems = targetedItems.Where(i => !string.IsNullOrEmpty(i.CategoryId) && i.CategoryId.Equals(rule.CategoryId, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                // Ne ürün ne kategori seçilmişse (ve kural türü bu ise) kural uygulanamaz
                return null;
            }

            var categoryItems = targetedItems.ToList();

            // Toplam adet
            var totalQty = categoryItems.Sum(i => i.Quantity);

            // Minimum adet kontrolü
            if (totalQty < rule.MinQuantity.Value)
                return null;

            // Kaç set var? Her MinQuantity adet için FreeQuantity adet bedava
            var sets = totalQty / rule.MinQuantity.Value;
            var freeCount = sets * rule.FreeQuantity.Value;

            // Tüm ürünleri birim bazında aç ve fiyata göre sırala (en ucuzdan)
            var unitList = categoryItems
                .SelectMany(i => Enumerable.Repeat(
                    new { i.ProductId, i.ProductName, i.UnitPrice }, i.Quantity))
                .OrderBy(u => u.UnitPrice)
                .ToList();

            // En ucuz freeCount adet ürünün toplam fiyatı = indirim
            var freeItems = unitList.Take((int)freeCount).ToList();
            var totalDiscount = freeItems.Sum(f => f.UnitPrice);

            var originalTotal = request.Items.Sum(i => i.UnitPrice * i.Quantity);

            // Ürün bazlı detay oluştur — bedava olan ürünlerin indirimini işaretle
            var freeByProduct = freeItems
                .GroupBy(f => f.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.UnitPrice));

            var details = request.Items.Select(item =>
            {
                var lineTotal = item.UnitPrice * item.Quantity;
                var itemDiscount = freeByProduct.GetValueOrDefault(item.ProductId, 0m);
                return new DiscountDetail
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    OriginalLineTotal = lineTotal,
                    DiscountAmount = itemDiscount,
                    DiscountedLineTotal = lineTotal - itemDiscount
                };
            }).ToList();

            return new CalculateDiscountResponse
            {
                OriginalTotal = originalTotal,
                TotalDiscount = totalDiscount,
                FinalTotal = originalTotal - totalDiscount,
                AppliedRuleId = rule.Id,
                AppliedRuleName = rule.Name,
                Details = details
            };
        }
    }
}
