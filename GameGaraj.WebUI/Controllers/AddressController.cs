using GameGaraj.WebUI.Models.Addresses;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Controllers
{
    [Authorize]
    public class AddressController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<AddressController> _logger;

        public AddressController(IOrderService orderService, ILogger<AddressController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        /// <summary>
        /// Adres listesi sayfası (Teslimat / Fatura tab'ları)
        /// </summary>
        public async Task<IActionResult> Index(AddressType? tab = null)
        {
            var deliveryAddresses = await _orderService.GetUserAddressesAsync(AddressType.Delivery);
            var invoiceAddresses = await _orderService.GetUserAddressesAsync(AddressType.Invoice);
            
            var model = new AddressListViewModel
            {
                DeliveryAddresses = deliveryAddresses,
                InvoiceAddresses = invoiceAddresses,
                ActiveTab = tab ?? AddressType.Delivery
            };

            return View(model);
        }

        /// <summary>
        /// Yeni adres ekleme sayfası
        /// </summary>
        public IActionResult Create(AddressType type = AddressType.Delivery)
        {
            var model = new CreateUserAddressInput
            {
                Type = type
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserAddressInput model)
        {
            _logger.LogInformation($"[AddressController] Creating address - Type: {model.Type}, Title: {model.Title}");
            
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("[AddressController] ModelState invalid");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning($"  - {error.ErrorMessage}");
                }
                return View(model);
            }

            var result = await _orderService.CreateAddressAsync(model);

            if (result == null)
            {
                _logger.LogError("[AddressController] Failed to create address");
                TempData["ErrorMessage"] = "Adres eklenirken bir hata oluştu. Maksimum 3 adres ekleyebilirsiniz.";
                return View(model);
            }

            _logger.LogInformation($"[AddressController] Address created successfully - Id: {result.Id}");
            TempData["SuccessMessage"] = "Adres başarıyla eklendi.";
            return RedirectToAction(nameof(Index), new { tab = model.Type });
        }

        /// <summary>
        /// Adres düzenleme sayfası
        /// </summary>
        public async Task<IActionResult> Edit(int id)
        {
            var address = await _orderService.GetAddressByIdAsync(id);

            if (address == null)
            {
                TempData["ErrorMessage"] = "Adres bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var model = new UpdateUserAddressInput
            {
                Id = address.Id,
                Type = address.Type,
                Title = address.Title,
                IsDefault = address.IsDefault,
                FirstName = address.FirstName,
                LastName = address.LastName,
                PhoneNumber = address.PhoneNumber,
                Province = address.Province,
                District = address.District,
                Neighborhood = address.Neighborhood,
                PostalCode = address.PostalCode,
                AddressDetail = address.AddressDetail
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UpdateUserAddressInput model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _orderService.UpdateAddressAsync(model);

            if (!result)
            {
                TempData["ErrorMessage"] = "Adres güncellenirken bir hata oluştu.";
                return View(model);
            }

            TempData["SuccessMessage"] = "Adres başarıyla güncellendi.";
            return RedirectToAction(nameof(Index), new { tab = model.Type });
        }

        /// <summary>
        /// Adres silme
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, AddressType tab)
        {
            var result = await _orderService.DeleteAddressAsync(id);

            if (!result)
            {
                TempData["ErrorMessage"] = "Adres silinirken bir hata oluştu.";
            }
            else
            {
                TempData["SuccessMessage"] = "Adres başarıyla silindi.";
            }

            return RedirectToAction(nameof(Index), new { tab });
        }

        /// <summary>
        /// Adresi varsayılan yap
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefault(int id, AddressType type)
        {
            var result = await _orderService.SetAsDefaultAsync(id, type);

            if (!result)
            {
                TempData["ErrorMessage"] = "Varsayılan adres ayarlanırken bir hata oluştu.";
            }
            else
            {
                TempData["SuccessMessage"] = "Varsayılan adres güncellendi.";
            }

            return RedirectToAction(nameof(Index), new { tab = type });
        }
    }
}
