import sys

file_path = r'd:\Kadir\Projeler\GameGaraj\GameGaraj.WebUI\Views\Home\Index.cshtml'
with open(file_path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

start_index = -1
end_index = -1
for i, line in enumerate(lines):
    if '<!-- Hero Carousel -->' in line:
        start_index = i
        break

for i in range(start_index, len(lines)):
    if '<!-- Featured Products Section -->' in lines[i]:
        end_index = i
        break

if start_index != -1 and end_index != -1:
    new_content = '''<!-- Hero Carousel -->
@{
    var carouselImages = new[]
    {
        "https://images.unsplash.com/photo-1587202372634-32705e3bf49c?w=1920&auto=format&fit=crop&q=80",
        "https://images.unsplash.com/photo-1593640408182-31c70c8268f5?w=1920&auto=format&fit=crop&q=80",
        "https://images.unsplash.com/photo-1591488320449-011701bb6704?w=1920&auto=format&fit=crop&q=80"
    };
    var carouselBtnClasses = new[] { "btn-primary", "btn-warning text-dark", "btn-success" };
    var carouselTextClasses = new[] { "text-primary", "text-warning", "text-success" };
    var carouselItemsCount = (activeRules.Any() || publicCoupons.Any() || rewardRules.Any()) 
        ? activeRules.Count + publicCoupons.Count + rewardRules.Count 
        : 3;
}
<div id="heroCarousel" class="carousel slide hero-carousel" data-bs-ride="carousel">
    <div class="carousel-indicators">
        @for (int i = 0; i < carouselItemsCount; i++)
        {
            <button type="button" data-bs-target="#heroCarousel" data-bs-slide-to="@i" class="@(i == 0 ? "active" : "")"></button>
        }
    </div>
    
    <div class="carousel-inner">
        @{
            int globalIndex = 0;
            if (activeRules.Any() || publicCoupons.Any() || rewardRules.Any())
            {
                foreach (var rule in activeRules)
                {
                    var bgImage = carouselImages[globalIndex % carouselImages.Length];
                    var btnClass = carouselBtnClasses[globalIndex % carouselBtnClasses.Length];
                    var textClass = carouselTextClasses[globalIndex % carouselTextClasses.Length];
                    
                    string discountText = "";
                    if (rule.RuleType == "TotalAmount") discountText = $"Sepette {rule.MinAmount?.ToString("N0")} TL ve üzerine {(rule.FixedDiscount > 0 ? $"{rule.FixedDiscount?.ToString("N0")} TL" : $"%{rule.DiscountRate?.ToString("N0")}")} indirim!";
                    else if (rule.RuleType == "BuyXGetYFree") discountText = $"{rule.MinQuantity} Al {rule.FreeQuantity} Öde fırsatı!";
                    else if (rule.RuleType == "CheapestItemDiscount") discountText = $"En ucuz ürüne %{rule.DiscountRate?.ToString("N0")} indirim!";
                    else if (rule.RuleType == "BrandDiscount") discountText = $"{(string.IsNullOrEmpty(rule.BrandName) ? "Seçili" : rule.BrandName)} markalı ürünlerde %{rule.DiscountRate?.ToString("N0")} indirim!";
                    else discountText = rule.Description;

                    <div class="carousel-item @(globalIndex == 0 ? "active" : "")" style="background-image: url('@bgImage');">
                        <div class="carousel-caption">
                            <h1 class="mb-3">@rule.Name <span class="@textClass">Fırsatı</span></h1>
                            <p class="mb-4">@discountText</p>
                            <a asp-controller="Campaign" asp-action="Detail" asp-route-id="@rule.Id" class="btn @btnClass btn-lg px-5 py-3 rounded-pill fw-bold">Kampanyayı İncele</a>
                        </div>
                    </div>
                    globalIndex++;
                }

                foreach (var coupon in publicCoupons)
                {
                    var bgImage = carouselImages[globalIndex % carouselImages.Length];
                    var btnClass = carouselBtnClasses[globalIndex % carouselBtnClasses.Length];
                    var textClass = carouselTextClasses[globalIndex % carouselTextClasses.Length];
                    
                    string discountText = "";
                    if (coupon.DiscountType == "FixedAmount") discountText = $"{coupon.Amount?.ToString("N0")} TL İndirim Kuponu!";
                    else if (coupon.DiscountType == "Percentage") discountText = $"%{coupon.DiscountRate?.ToString("N0")} İndirim Kuponu!";
                    else if (coupon.DiscountType == "FreeShipping") discountText = "Kargo Ücretsiz Kuponu!";

                    <div class="carousel-item @(globalIndex == 0 ? "active" : "")" style="background-image: url('@bgImage');">
                        <div class="carousel-caption">
                            <h1 class="mb-3">@coupon.Code <span class="@textClass">Kuponu</span></h1>
                            <p class="mb-4">@discountText <br/> <small>Alt Limit: @(coupon.MinPurchaseAmount > 0 ? coupon.MinPurchaseAmount?.ToString("N0") + " TL" : "Yok")</small></p>
                            <button onclick="copyCouponCode('@coupon.Code', this)" class="btn @btnClass btn-lg px-5 py-3 rounded-pill fw-bold"><i class="fa-regular fa-copy me-2"></i>Kodu Kopyala</button>
                        </div>
                    </div>
                    globalIndex++;
                }

                foreach (var reward in rewardRules)
                {
                    var bgImage = carouselImages[globalIndex % carouselImages.Length];
                    var btnClass = carouselBtnClasses[globalIndex % carouselBtnClasses.Length];
                    var textClass = carouselTextClasses[globalIndex % carouselTextClasses.Length];
                    
                    string rewardValue = reward.RewardCouponAmount > 0 ? $"{reward.RewardCouponAmount?.ToString("N0")} TL" : $"%{reward.RewardCouponRate?.ToString("N0")}";

                    <div class="carousel-item @(globalIndex == 0 ? "active" : "")" style="background-image: url('@bgImage');">
                        <div class="carousel-caption">
                            <h1 class="mb-3">@reward.RuleName <span class="@textClass">Görevi</span></h1>
                            <p class="mb-4">@reward.PeriodInDays günde @(reward.RequiredSpendAmount.ToString("N0")) TL harca, @rewardValue Kupon Kazan!</p>
                            <a asp-controller="Campaign" asp-action="RewardDetail" asp-route-id="@reward.Id" class="btn @btnClass btn-lg px-5 py-3 rounded-pill fw-bold">Detay Gör</a>
                        </div>
                    </div>
                    globalIndex++;
                }
            }
            else
            {
                <div class="carousel-item active" style="background-image: url('https://images.unsplash.com/photo-1587202372634-32705e3bf49c?w=1920&auto=format&fit=crop&q=80');">
                    <div class="carousel-caption">
                        <h1 class="mb-3">Geleceğin Teknolojisi <span class="text-primary">Burada</span></h1>
                        <p class="mb-4">En güçlü oyun bilgisayarları ve ekipmanları</p>
                        <a asp-controller="Product" asp-action="Index" class="btn btn-primary btn-lg px-5 py-3 rounded-pill fw-bold">Keşfet</a>
                    </div>
                </div>
                
                <div class="carousel-item" style="background-image: url('https://images.unsplash.com/photo-1593640408182-31c70c8268f5?w=1920&auto=format&fit=crop&q=80');">
                    <div class="carousel-caption">
                        <h1 class="mb-3">Profesyonel <span class="text-warning">Ekipmanlar</span></h1>
                        <p class="mb-4">E-spor oyuncuları için özel seçilmiş ürünler</p>
                        <a asp-controller="Product" asp-action="Index" class="btn btn-warning btn-lg px-5 py-3 rounded-pill fw-bold text-dark">İncele</a>
                    </div>
                </div>
                
                <div class="carousel-item" style="background-image: url('https://images.unsplash.com/photo-1591488320449-011701bb6704?w=1920&auto=format&fit=crop&q=80');">
                    <div class="carousel-caption">
                        <h1 class="mb-3">Kampanyalı <span class="text-success">Fırsatlar</span></h1>
                        <p class="mb-4">Sınırlı sayıda özel indirimler</p>
                        <a asp-controller="Product" asp-action="Index" class="btn btn-success btn-lg px-5 py-3 rounded-pill fw-bold">Fırsatları Gör</a>
                    </div>
                </div>
            }
        }
    </div>
    
    <button class="carousel-control-prev" type="button" data-bs-target="#heroCarousel" data-bs-slide="prev">
        <span class="carousel-control-prev-icon"></span>
    </button>
    <button class="carousel-control-next" type="button" data-bs-target="#heroCarousel" data-bs-slide="next">
        <span class="carousel-control-next-icon"></span>
    </button>
</div>

'''

    lines[start_index:end_index] = [new_content]
    
    with open(file_path, 'w', encoding='utf-8') as f:
        f.writelines(lines)
    print('Updated successfully!')
else:
    print(f'Error: start_index={start_index}, end_index={end_index}')
