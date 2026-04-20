using GameGaraj.Campaign.API.Models;

namespace GameGaraj.Campaign.API.Rules
{
    /// <summary>
    /// Belirli bir kategoriden 2 veya daha fazla ürün alındığında
    /// en ucuz ürüne yüzde indirim uygular.
    /// Örnek: Kategori 1'den 2+ ürün alınca en ucuza %20 indirim.
    /// </summary>
    public class CheapestItemDiscountRule : ICampaignRule
    {
        public string RuleType => "CheapestItemDiscount";

        public CalculateDiscountResponse? Calculate(CalculateDiscountRequest request, CampaignRule rule)
        {
            if (rule.MinQuantity == null || rule.DiscountRate == null)
                return null;

            // Filtreleme: Önce Ürün ID'sine, yoksa Kategori ID'sine bak
            var targetedItems = request.Items.AsEnumerable();

            if (!string.IsNullOrEmpty(rule.ProductId))
            {
                targetedItems = targetedItems.Where(i => i.ProductId == rule.ProductId);
            }
            else if (!string.IsNullOrEmpty(rule.CategoryId))
            {
                targetedItems = targetedItems.Where(i => i.CategoryId == rule.CategoryId);
            }
            else
            {
                return null;
            }

            var categoryItems = targetedItems.ToList();

            // Toplam adet kontrolü (farklı ürünlerden toplam adet)
            var totalQty = categoryItems.Sum(i => i.Quantity);
            if (totalQty < rule.MinQuantity.Value)
                return null;

            // En ucuz ürünü bul
            var cheapestItem = categoryItems
                .OrderBy(i => i.UnitPrice)
                .First();

            var discountRate = rule.DiscountRate.Value / 100m;
            var discountOnCheapest = Math.Round(cheapestItem.UnitPrice * discountRate, 2);

            var originalTotal = request.Items.Sum(i => i.UnitPrice * i.Quantity);

            var details = request.Items.Select(item =>
            {
                var lineTotal = item.UnitPrice * item.Quantity;
                var itemDiscount = 0m;

                // Sadece en ucuz ürüne indirim (1 adetine)
                if (item.ProductId == cheapestItem.ProductId)
                {
                    itemDiscount = discountOnCheapest;
                }

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
                TotalDiscount = discountOnCheapest,
                FinalTotal = originalTotal - discountOnCheapest,
                AppliedRuleId = rule.Id,
                AppliedRuleName = rule.Name,
                Details = details
            };
        }
    }
}
