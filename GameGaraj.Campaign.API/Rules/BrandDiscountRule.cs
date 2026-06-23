using GameGaraj.Campaign.API.Models;

namespace GameGaraj.Campaign.API.Rules
{
    /// <summary>
    /// Belirli bir markaya (opsiyonel olarak + kategori) ait ürünlere yüzdelik indirim uygular.
    /// Örnek: "Samsung telefonlara %20 indirim" (BrandName=Samsung, CategoryId=telefon-kategorisi)
    /// Örnek: "Samsung tüm ürünlerine %10 indirim" (BrandName=Samsung, CategoryId=null)
    /// </summary>
    public class BrandDiscountRule : ICampaignRule
    {
        public string RuleType => "BrandDiscount";

        public CalculateDiscountResponse? Calculate(CalculateDiscountRequest request, CampaignRule rule)
        {
            if (rule.DiscountRate == null)
                return null;

            // Kapsam eşleme
            var targetedItems = request.Items.AsEnumerable();

            if (!string.IsNullOrEmpty(rule.ProductId))
            {
                // Ürün bazlı
                targetedItems = targetedItems.Where(i => i.ProductId.Equals(rule.ProductId, StringComparison.OrdinalIgnoreCase));
            }
            else if (!string.IsNullOrEmpty(rule.BrandName) && !string.IsNullOrEmpty(rule.CategoryId))
            {
                // Marka + Kategori bazlı
                targetedItems = targetedItems.Where(i => i.CategoryId == rule.CategoryId &&
                                                         !string.IsNullOrEmpty(i.Brand) &&
                                                         i.Brand.Equals(rule.BrandName, StringComparison.OrdinalIgnoreCase));
            }
            else if (!string.IsNullOrEmpty(rule.CategoryId))
            {
                // Kategori bazlı
                targetedItems = targetedItems.Where(i => i.CategoryId == rule.CategoryId);
            }
            else if (!string.IsNullOrEmpty(rule.BrandName))
            {
                // Marka bazlı
                targetedItems = targetedItems.Where(i => !string.IsNullOrEmpty(i.Brand) &&
                                                         i.Brand.Equals(rule.BrandName, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                // Hiçbiri girilmemişse indirim uygulanamaz
                return null;
            }

            var matchedItems = targetedItems.ToList();
            if (!matchedItems.Any())
                return null;

            var discountRate = rule.DiscountRate.Value / 100m;
            var originalTotal = request.Items.Sum(i => i.UnitPrice * i.Quantity);
            
            // Sadece eşleşen marka/ürün/kategori ürünlerine indirim uygula
            var matchedProductIds = matchedItems.Select(m => m.ProductId).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var details = request.Items.Select(item =>
            {
                var lineTotal = item.UnitPrice * item.Quantity;
                var itemDiscount = 0m;

                if (matchedProductIds.Contains(item.ProductId))
                {
                    itemDiscount = Math.Round(lineTotal * discountRate, 2);
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

            var totalDiscount = details.Sum(d => d.DiscountAmount);

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
