using GameGaraj.Campaign.API.Models;
using GameGaraj.Campaign.API.Rules;
using GameGaraj.Campaign.API.Services.Abstract;

namespace GameGaraj.Campaign.API.Services.Concrete
{
    /// <summary>
    /// Kural motoru: Aktif kuralları getirir, her birini ilgili strateji ile hesaplar,
    /// ve Best Single Discount stratejisi ile en avantajlı kuralı seçer.
    /// </summary>
    public class CampaignCalculationService : ICampaignCalculationService
    {
        private readonly ICampaignRuleService _ruleService;
        private readonly IEnumerable<ICampaignRule> _ruleStrategies;
        private readonly ILogger<CampaignCalculationService> _logger;

        public CampaignCalculationService(
            ICampaignRuleService ruleService,
            IEnumerable<ICampaignRule> ruleStrategies,
            ILogger<CampaignCalculationService> logger)
        {
            _ruleService = ruleService;
            _ruleStrategies = ruleStrategies;
            _logger = logger;
        }

        public async Task<CalculateDiscountResponse> CalculateAsync(CalculateDiscountRequest request)
        {
            var originalTotal = request.Items.Sum(i => i.UnitPrice * i.Quantity);
            if (!request.Items.Any()) return NoDiscount(originalTotal);

            var activeRules = await _ruleService.GetActiveAsync();
            if (!activeRules.Any()) return NoDiscount(originalTotal);

            var strategyMap = _ruleStrategies.ToDictionary(s => s.RuleType, s => s);

            // --- 1. AŞAMA: Ürün Bazlı İndirimler (Item-Level) ---
            var itemLevelRules = activeRules.Where(r => r.RuleType == "BuyXGetYFree" || r.RuleType == "CheapestItemDiscount").ToList();
            var appliedItemRules = new List<CalculateDiscountResponse>();

            // Çakışma kontrolü: Ürün bazlı kuralları hesapla ve en iyisini seç (Şimdilik basit mantık: Tüm ürün bazlılardan en yüksek toplam indirimi verenleri birleştir)
            // Not: Gerçek hayatta burada ürün kümesi bazlı çakışma kontrolü yapılır. 
            // Kullanıcı talebi: Aynı ürün için çakışanlarda en iyisini seç.
            
            foreach (var rule in itemLevelRules)
            {
                if (strategyMap.TryGetValue(rule.RuleType, out var strategy))
                {
                    var result = strategy.Calculate(request, rule);
                    if (result != null && result.TotalDiscount > 0)
                    {
                        appliedItemRules.Add(result);
                    }
                }
            }

            // Çakışma kontrolü: Aynı hedef (Ürün veya Kategori) için birden fazla kural varsa en iyisini seç
            var finalItemDiscounts = appliedItemRules
                .GroupBy(r => {
                    // Kuralın hangi kural nesnesinden geldiğini bulup hedefini belirle
                    var rule = itemLevelRules.First(ir => ir.Id == r.AppliedRuleId);
                    return !string.IsNullOrEmpty(rule.ProductId) ? $"P_{rule.ProductId}" : $"C_{rule.CategoryId}";
                })
                .Select(g => g.OrderByDescending(r => r.TotalDiscount).First())
                .ToList();

            decimal totalItemDiscount = finalItemDiscounts.Sum(r => r.TotalDiscount);
            var subTotalAfterItems = originalTotal - totalItemDiscount;

            // --- 2. AŞAMA: Sepet Bazlı İndirimler (Global Level) ---
            var globalRules = activeRules.Where(r => r.RuleType == "TotalAmount").ToList();
            CalculateDiscountResponse? bestGlobalResult = null;

            foreach (var rule in globalRules)
            {
                if (strategyMap.TryGetValue(rule.RuleType, out var strategy))
                {
                    // TotalAmountRule ara toplam üzerinden çalışmalı. Manuel hesaplıyoruz:
                    if (rule.MinAmount.HasValue && subTotalAfterItems >= rule.MinAmount.Value)
                    {
                        var discountRate = (rule.DiscountRate ?? 0) / 100m;
                        var globalDiscount = Math.Round(subTotalAfterItems * discountRate, 2);

                        if (bestGlobalResult == null || globalDiscount > bestGlobalResult.TotalDiscount)
                        {
                            bestGlobalResult = new CalculateDiscountResponse
                            {
                                TotalDiscount = globalDiscount,
                                AppliedRuleId = rule.Id,
                                AppliedRuleName = rule.Name
                            };
                        }
                    }
                }
            }

            // --- SONUÇ BİRLEŞTİRME ---
            var finalResponse = new CalculateDiscountResponse
            {
                OriginalTotal = originalTotal,
                TotalDiscount = totalItemDiscount + (bestGlobalResult?.TotalDiscount ?? 0),
                FinalTotal = originalTotal - (totalItemDiscount + (bestGlobalResult?.TotalDiscount ?? 0)),
                Details = new List<DiscountDetail>(),
                AppliedRules = new List<AppliedRuleSummary>()
            };

            // Ürün bazlı kuralları listeye ekle
            foreach (var itemRes in finalItemDiscounts)
            {
                finalResponse.AppliedRules.Add(new AppliedRuleSummary
                {
                    RuleId = itemRes.AppliedRuleId ?? 0,
                    RuleName = itemRes.AppliedRuleName ?? "İndirim",
                    DiscountAmount = itemRes.TotalDiscount
                });
            }

            // Sepet bazlı kuralı listeye ekle
            if (bestGlobalResult != null)
            {
                finalResponse.AppliedRules.Add(new AppliedRuleSummary
                {
                    RuleId = bestGlobalResult.AppliedRuleId ?? 0,
                    RuleName = bestGlobalResult.AppliedRuleName ?? "Sepet İndirimi",
                    DiscountAmount = bestGlobalResult.TotalDiscount
                });
            }

            finalResponse.AppliedRuleName = string.Join(" + ", finalResponse.AppliedRules.Select(r => r.RuleName));

            // Detayları birleştir
            foreach (var item in request.Items)
            {
                var originalLineTotal = item.UnitPrice * item.Quantity;
                var itemLineDiscount = finalItemDiscounts.Sum(r => r.Details.FirstOrDefault(d => d.ProductId == item.ProductId)?.DiscountAmount ?? 0);
                
                // Sepet indirimi oransal olarak her satıra dağıtılır (ara toplam üzerinden)
                var globalLineDiscount = 0m;
                if (bestGlobalResult != null && subTotalAfterItems > 0)
                {
                    var remainingLineTotal = originalLineTotal - itemLineDiscount;
                    var ratio = remainingLineTotal / subTotalAfterItems;
                    globalLineDiscount = Math.Round(bestGlobalResult.TotalDiscount * ratio, 2);
                }

                finalResponse.Details.Add(new DiscountDetail
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    OriginalLineTotal = originalLineTotal,
                    DiscountAmount = itemLineDiscount + globalLineDiscount,
                    DiscountedLineTotal = originalLineTotal - (itemLineDiscount + globalLineDiscount)
                });
            }

            return finalResponse;
        }

        private static CalculateDiscountResponse NoDiscount(decimal originalTotal)
        {
            return new CalculateDiscountResponse
            {
                OriginalTotal = originalTotal,
                TotalDiscount = 0,
                FinalTotal = originalTotal,
                AppliedRuleId = null,
                AppliedRuleName = null,
                Details = new()
            };
        }
    }
}
