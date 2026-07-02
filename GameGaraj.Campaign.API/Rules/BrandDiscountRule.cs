using GameGaraj.Campaign.API.Models;

namespace GameGaraj.Campaign.API.Rules
{
    /// <summary>
    /// Applies a percentage discount to a selected product, category, brand, or category + brand scope.
    /// </summary>
    public class BrandDiscountRule : ICampaignRule
    {
        public string RuleType => "BrandDiscount";

        public CalculateDiscountResponse? Calculate(CalculateDiscountRequest request, CampaignRule rule)
        {
            if (rule.DiscountRate == null && rule.FixedDiscount == null)
                return null;

            var ruleProductId = rule.ProductId?.Trim();
            var ruleCategoryId = rule.CategoryId?.Trim();
            var ruleBrandName = rule.BrandName?.Trim();

            var targetedItems = request.Items.AsEnumerable();

            if (!string.IsNullOrEmpty(ruleProductId))
            {
                targetedItems = targetedItems.Where(i =>
                    i.ProductId.Equals(ruleProductId, StringComparison.OrdinalIgnoreCase));
            }
            else if (!string.IsNullOrEmpty(ruleBrandName) && !string.IsNullOrEmpty(ruleCategoryId))
            {
                targetedItems = targetedItems.Where(i =>
                    string.Equals(i.CategoryId?.Trim(), ruleCategoryId, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(i.Brand) &&
                    string.Equals(i.Brand.Trim(), ruleBrandName, StringComparison.OrdinalIgnoreCase));
            }
            else if (!string.IsNullOrEmpty(ruleCategoryId))
            {
                targetedItems = targetedItems.Where(i =>
                    string.Equals(i.CategoryId?.Trim(), ruleCategoryId, StringComparison.OrdinalIgnoreCase));
            }
            else if (!string.IsNullOrEmpty(ruleBrandName))
            {
                targetedItems = targetedItems.Where(i =>
                    !string.IsNullOrEmpty(i.Brand) &&
                    string.Equals(i.Brand.Trim(), ruleBrandName, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                return null;
            }

            var matchedItems = targetedItems.ToList();
            if (!matchedItems.Any())
                return null;

            var originalTotal = request.Items.Sum(i => i.UnitPrice * i.Quantity);
            var matchedProductIds = matchedItems.Select(m => m.ProductId).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var details = request.Items.Select(item =>
            {
                var lineTotal = item.UnitPrice * item.Quantity;
                var itemDiscount = 0m;

                if (matchedProductIds.Contains(item.ProductId))
                {
                    if (rule.DiscountRate.HasValue && rule.DiscountRate.Value > 0)
                    {
                        var discountRate = rule.DiscountRate.Value / 100m;
                        itemDiscount = Math.Round(lineTotal * discountRate, 2);
                    }
                    else if (rule.FixedDiscount.HasValue && rule.FixedDiscount.Value > 0)
                    {
                        var fixedLineDiscount = rule.FixedDiscount.Value * item.Quantity;
                        itemDiscount = Math.Min(lineTotal, fixedLineDiscount);
                    }
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
