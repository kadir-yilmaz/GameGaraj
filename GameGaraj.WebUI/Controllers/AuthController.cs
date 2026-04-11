using GameGaraj.WebUI.Models.Auth;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Controllers
{
    public class AuthController : Controller
    {
        private readonly IIdentityService _identityService;

        public AuthController(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        [HttpGet]
        public IActionResult SignIn()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SignIn(SignInViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var error = await _identityService.SignInAsync(model);
            if (!string.IsNullOrEmpty(error))
            {
                ModelState.AddModelError(string.Empty, error);
                return View(model);
            }

            // Kullanıcı admin veya editor ise admin paneline yönlendir
            if (User.IsInRole("admin") || User.IsInRole("editor"))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult SignUp()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SignUp(SignUpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var error = await _identityService.SignUpAsync(model);
            if (!string.IsNullOrEmpty(error))
            {
                ModelState.AddModelError(string.Empty, error);
                return View(model);
            }

            TempData["Success"] = "Kayıt başarılı! Giriş yapabilirsiniz.";
            return RedirectToAction(nameof(SignIn));
        }

        public new async Task<IActionResult> SignOut()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await _identityService.RevokeRefreshToken(); // Opsiyonel: Keycloak'tan da oturumu kapatmak için
            return RedirectToAction("Index", "Home");
        }
    }
}
