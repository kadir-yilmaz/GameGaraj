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
            await _identityService.RevokeRefreshToken(); // Opsiyonel: Keycloak'tan da oturumu kapatmak için

            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action("Index", "Home")
            };

            return SignOut(
                properties,
                CookieAuthenticationDefaults.AuthenticationScheme,
                "Keycloak");
        }

        [HttpGet]
        public IActionResult GoogleSignIn(string? returnUrl = null, bool popup = false)
        {
            var redirectUrl = Url.Action(nameof(GoogleSignInCallback), new { returnUrl, popup }) ?? Url.Action("Index", "Home")!;
            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl
            };

            properties.Items["kc_idp_hint"] = "google";
            properties.Items["prompt"] = "select_account";

            return Challenge(properties, "Keycloak");
        }

        [HttpGet]
        public IActionResult GoogleSignInCallback(string? returnUrl = null, bool popup = false)
        {
            if (popup)
            {
                var targetUrl = GetPostLoginRedirectUrl(returnUrl);
                var escapedTargetUrl = System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(targetUrl);

                return Content($$"""
                <!DOCTYPE html>
                <html lang="tr">
                <head>
                    <meta charset="utf-8" />
                    <title>Giriş tamamlandı</title>
                </head>
                <body>
                    <script>
                        (function () {
                            var payload = { type: 'gamegaraj:auth-complete', targetUrl: '{{escapedTargetUrl}}' };
                            try {
                                if (window.opener && !window.opener.closed) {
                                    window.opener.postMessage(payload, window.location.origin);
                                }
                            } catch (e) {
                            }
                            window.close();
                            document.body.innerText = 'Giriş tamamlandı. Bu pencereyi kapatabilirsiniz.';
                        })();
                    </script>
                </body>
                </html>
                """, "text/html");
            }

            return Redirect(GetPostLoginRedirectUrl(returnUrl));
        }

        private string GetPostLoginRedirectUrl(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                if (returnUrl.StartsWith("/auth/signin", StringComparison.OrdinalIgnoreCase) ||
                    returnUrl.StartsWith("/auth/signup", StringComparison.OrdinalIgnoreCase))
                {
                    return Url.Action("Index", "Home") ?? "/";
                }

                return returnUrl;
            }

            if (User.IsInRole("admin") || User.IsInRole("editor"))
            {
                return Url.Action("Index", "Dashboard", new { area = "Admin" }) ?? "/";
            }

            return Url.Action("Index", "Home") ?? "/";
        }
    }
}
