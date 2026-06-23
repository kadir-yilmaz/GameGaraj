using GameGaraj.Campaign.API.Models;

namespace GameGaraj.Campaign.API.Rules
{
    /// <summary>
    /// Toplam sipariş tutarı belirli bir miktarı aştığında
    /// tüm sepete yüzde indirim uygular.
    /// Örnek: 1000 TL üzeri alışverişe %10 indirim.
    /// </summary>
    public class TotalAmountRule : ICampaignRule
    {
        public string RuleType => "TotalAmount";

        public CalculateDiscountResponse? Calculate(CalculateDiscountRequest request, CampaignRule rule)
        {
            if (rule.MinAmount == null)
                return null;

            // En az DiscountRate veya FixedDiscount olmalı
            if (rule.DiscountRate == null && rule.FixedDiscount == null)
                return null;

            // Filtreleme: Önce Ürün ID'sine, yoksa Kategori ID'sine bak. Hiçbiri yoksa tüm sepet.
            var targetedItems = request.Items.AsEnumerable();

            if (!string.IsNullOrEmpty(rule.ProductId))
            {
                targetedItems = targetedItems.Where(i => i.ProductId == rule.ProductId);
            }
            else if (!string.IsNullOrEmpty(rule.CategoryId))
            {
                targetedItems = targetedItems.Where(i => i.CategoryId == rule.CategoryId);
            }

            var categoryItems = targetedItems.ToList();
            var targetedTotal = categoryItems.Sum(i => i.UnitPrice * i.Quantity);
            var originalTotal = request.Items.Sum(i => i.UnitPrice * i.Quantity);

            // Hedeflenen gruptaki toplam tutar minimum tutarı aşmalı
            if (targetedTotal < rule.MinAmount.Value)
                return null;

            decimal totalDiscount;
            decimal discountRate;

            if (rule.FixedDiscount.HasValue && rule.FixedDiscount.Value > 0)
            {
                // Sabit tutar indirimi: Sepet 1000 TL ise 100 TL indirim
                totalDiscount = Math.Min(rule.FixedDiscount.Value, originalTotal);
                discountRate = originalTotal > 0 ? totalDiscount / originalTotal : 0;
            }
            else
            {
                // Yüzdelik indirim
                discountRate = rule.DiscountRate!.Value / 100m;
                totalDiscount = Math.Round(originalTotal * discountRate, 2);
            }

            var details = request.Items.Select(item =>
            {
                var lineTotal = item.UnitPrice * item.Quantity;
                var itemDiscount = Math.Round(lineTotal * (totalDiscount / originalTotal), 2);
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
