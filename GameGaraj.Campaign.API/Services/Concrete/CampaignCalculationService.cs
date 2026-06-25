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
        private readonly ICouponService _couponService;
        private readonly IEnumerable<ICampaignRule> _ruleStrategies;
        private readonly ILogger<CampaignCalculationService> _logger;

        public CampaignCalculationService(
            ICampaignRuleService ruleService,
            ICouponService couponService,
            IEnumerable<ICampaignRule> ruleStrategies,
            ILogger<CampaignCalculationService> logger)
        {
            _ruleService = ruleService;
            _couponService = couponService;
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
            var itemLevelRules = activeRules.Where(r => r.RuleType == "BuyXGetYFree" || r.RuleType == "CheapestItemDiscount" || r.RuleType == "BrandDiscount").ToList();
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

            // Çakışma kontrolü: Aynı hedef (Ürün, Kategori veya Marka) için birden fazla kural varsa en iyisini seç
            var finalItemDiscounts = appliedItemRules
                .GroupBy(r => {
                    // Kuralın hangi kural nesnesinden geldiğini bulup hedefini belirle
                    var rule = itemLevelRules.First(ir => ir.Id == r.AppliedRuleId);
                    if (!string.IsNullOrEmpty(rule.ProductId))
                        return $"P_{rule.ProductId.Trim()}";
                    if (!string.IsNullOrEmpty(rule.CategoryId) && !string.IsNullOrEmpty(rule.BrandName))
                        return $"C_{rule.CategoryId.Trim()}_B_{rule.BrandName.Trim()}";
                    if (!string.IsNullOrEmpty(rule.CategoryId))
                        return $"C_{rule.CategoryId.Trim()}";
                    if (!string.IsNullOrEmpty(rule.BrandName))
                        return $"B_{rule.BrandName.Trim()}";
                    return $"G_{rule.Id}";
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
                        decimal globalDiscount = 0m;
                        if (rule.FixedDiscount.HasValue && rule.FixedDiscount.Value > 0)
                        {
                            globalDiscount = Math.Min(rule.FixedDiscount.Value, subTotalAfterItems);
                        }
                        else if (rule.DiscountRate.HasValue && rule.DiscountRate.Value > 0)
                        {
                            var discountRate = rule.DiscountRate.Value / 100m;
                            globalDiscount = Math.Round(subTotalAfterItems * discountRate, 2);
                        }

                        if (globalDiscount > 0 && (bestGlobalResult == null || globalDiscount > bestGlobalResult.TotalDiscount))
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
                TotalDiscount = Math.Min(originalTotal, totalItemDiscount + (bestGlobalResult?.TotalDiscount ?? 0)),
                FinalTotal = Math.Max(0, originalTotal - (totalItemDiscount + (bestGlobalResult?.TotalDiscount ?? 0))),
                Details = new List<DiscountDetail>(),
                AppliedRules = new List<AppliedRuleSummary>(),
                IsCouponApplied = false
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

            // Detayları birleştir
            foreach (var item in request.Items)
            {
                var originalLineTotal = item.UnitPrice * item.Quantity;
                var itemLineDiscount = finalItemDiscounts.Sum(r => r.Details.FirstOrDefault(d => d.ProductId.Equals(item.ProductId, StringComparison.OrdinalIgnoreCase))?.DiscountAmount ?? 0);
                
                // Sepet indirimi oransal olarak her satıra dağıtılır (ara toplam üzerinden)
                var globalLineDiscount = 0m;
                if (bestGlobalResult != null && subTotalAfterItems > 0)
                {
                    var remainingLineTotal = originalLineTotal - itemLineDiscount;
                    var ratio = remainingLineTotal / subTotalAfterItems;
                    globalLineDiscount = Math.Round(bestGlobalResult.TotalDiscount * ratio, 2);
                }

                var matchedRule = finalItemDiscounts.FirstOrDefault(r => r.Details.Any(d => d.ProductId.Equals(item.ProductId, StringComparison.OrdinalIgnoreCase) && d.DiscountAmount > 0));
                var ruleName = matchedRule?.AppliedRuleName;

                if (string.IsNullOrEmpty(ruleName) && globalLineDiscount > 0)
                {
                    ruleName = bestGlobalResult?.AppliedRuleName;
                }

                finalResponse.Details.Add(new DiscountDetail
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    OriginalLineTotal = originalLineTotal,
                    DiscountAmount = itemLineDiscount + globalLineDiscount,
                    DiscountedLineTotal = originalLineTotal - (itemLineDiscount + globalLineDiscount),
                    RuleName = ruleName
                });
            }

            // --- 3. AŞAMA: KUPON KONTROLÜ VE UYGULAMASI ---
            if (!string.IsNullOrWhiteSpace(request.CouponCode))
            {
                var coupon = await _couponService.GetByCodeAsync(request.CouponCode);
                if (coupon == null || !coupon.IsActive)
                {
                    finalResponse.CouponMessage = "Geçersiz veya pasif kupon kodu.";
                }
                else if (coupon.IsUsed)
                {
                    finalResponse.CouponMessage = "Bu kupon daha önce kullanılmış.";
                }
                else if (coupon.ExpirationDate.HasValue && coupon.ExpirationDate.Value < DateTime.UtcNow)
                {
                    finalResponse.CouponMessage = "Kuponun son kullanma tarihi geçmiş.";
                }
                else if (!string.IsNullOrEmpty(coupon.UserId) && !coupon.UserId.Equals(request.UserId, StringComparison.OrdinalIgnoreCase))
                {
                    finalResponse.CouponMessage = "Bu kupon sizin hesabınıza tanımlanmamış.";
                }
                else if (coupon.MinOrderAmount.HasValue && finalResponse.FinalTotal < coupon.MinOrderAmount.Value)
                {
                    finalResponse.CouponMessage = $"Bu kuponu kullanabilmek için sepet tutarı en az {coupon.MinOrderAmount.Value:N2} TL olmalıdır.";
                }
                else if (!coupon.AllowWithOtherCampaigns && finalResponse.TotalDiscount > 0)
                {
                    finalResponse.CouponMessage = "Bu kupon sepetteki diğer kampanyalarla birleştirilemez.";
                }
                else
                {
                    // Kupon geçerli, indirimi hesapla
                    decimal couponDiscount = 0m;
                    if (coupon.CouponType == "FixedAmount" && coupon.Amount.HasValue)
                    {
                        couponDiscount = Math.Min(coupon.Amount.Value, finalResponse.FinalTotal);
                    }
                    else if (coupon.CouponType == "Percentage" && coupon.Rate.HasValue)
                    {
                        couponDiscount = Math.Round(finalResponse.FinalTotal * (coupon.Rate.Value / 100m), 2);
                        if (coupon.MaxDiscountAmount.HasValue && couponDiscount > coupon.MaxDiscountAmount.Value)
                        {
                            couponDiscount = coupon.MaxDiscountAmount.Value;
                        }
                    }
                    else if (coupon.CouponType == "FreeShipping")
                    {
                        // Kargo bedava kuponu doğrudan tutara yansımaz, ancak kargo fiyatlandırmasında dikkate alınır.
                        // OrderPricingViewModel'da TotalShipping = 0 yapılacak. Burada bilgi olarak ekliyoruz.
                        finalResponse.CouponMessage = "Kargo Bedava kuponu uygulandı.";
                        finalResponse.IsCouponApplied = true;
                    }

                    if (couponDiscount > 0)
                    {
                        finalResponse.TotalDiscount = Math.Min(finalResponse.OriginalTotal, finalResponse.TotalDiscount + couponDiscount);
                        finalResponse.FinalTotal = Math.Max(0, finalResponse.FinalTotal - couponDiscount);
                        finalResponse.IsCouponApplied = true;
                        finalResponse.CouponMessage = "Kupon başarıyla uygulandı.";

                        finalResponse.AppliedRules.Add(new AppliedRuleSummary
                        {
                            RuleId = coupon.Id,
                            RuleName = $"Kupon ({coupon.Code})",
                            DiscountAmount = couponDiscount
                        });
                        
                        // Kupon indirimi de oransal olarak dağıtılabilir (şu an için toplamdan düşüldü)
                    }
                }
            }

            finalResponse.AppliedRuleName = string.Join(" + ", finalResponse.AppliedRules.Select(r => r.RuleName));

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
                Details = new(),
                AppliedRules = new()
            };
        }
    }
}
