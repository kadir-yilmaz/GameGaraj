using GameGaraj.WebUI.Models.Auth;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

using AspNetCoreHero.ToastNotification.Abstractions;

namespace GameGaraj.WebUI.Controllers
{
    public class AuthController : Controller
    {
        private readonly IIdentityService _identityService;
        private readonly IBasketService _basketService;
        private readonly INotyfService _notyf;

        public AuthController(IIdentityService identityService, IBasketService basketService, INotyfService notyf)
        {
            _identityService = identityService;
            _basketService = basketService;
            _notyf = notyf;
        }

        [HttpGet]
        public IActionResult SignIn(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SignIn(SignInViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Giriş öncesi misafir cookie ID'sini oku
            var guestCookieName = "GameGarajGuestId";
            string? guestId = null;
            if (HttpContext.Request.Cookies.TryGetValue(guestCookieName, out var gid))
            {
                guestId = gid;
            }

            var signInResult = await _identityService.SignInAsync(model);
            if (!string.IsNullOrEmpty(signInResult.Error))
            {
                ModelState.AddModelError(string.Empty, signInResult.Error);
                return View(model);
            }

            // Giriş başarılı, sepet senkronizasyonunu başlat
            if (!string.IsNullOrEmpty(guestId) && !string.IsNullOrEmpty(signInResult.UserId))
            {
                await _basketService.SyncBasketAsync(guestId, signInResult.UserId);
                HttpContext.Response.Cookies.Delete(guestCookieName);
            }

            return Redirect(GetPostLoginRedirectUrl(returnUrl));
        }

        [HttpGet]
        public IActionResult SignUp(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SignUp(SignUpViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
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
            return RedirectToAction(nameof(SignIn), new { returnUrl });
        }

        [HttpGet]
        public IActionResult ForgotPassword(string? email = null, int step = 1)
        {
            ViewBag.Step = step;
            if (step == 2)
            {
                return View(new ResetPasswordOtpViewModel { Email = email ?? string.Empty });
            }

            return View(new ForgotPasswordViewModel { Email = email ?? string.Empty });
        }

        [HttpPost]
        public async Task<IActionResult> SendResetOtp(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Step = 1;
                return View("ForgotPassword", model);
            }

            var (success, message) = await _identityService.SendPasswordResetOtpAsync(model.Email);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, message ?? "Doğrulama kodu gönderilemedi.");
                ViewBag.Step = 1;
                return View("ForgotPassword", model);
            }

            _notyf.Success("6 haneli doğrulama kodu e-posta adresinize gönderildi.");
            return RedirectToAction(nameof(ForgotPassword), new { email = model.Email, step = 2 });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPasswordWithOtp(ResetPasswordOtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Step = 2;
                return View("ForgotPassword", model);
            }

            var (success, message) = await _identityService.ResetPasswordWithOtpAsync(model.Email, model.OtpCode, model.NewPassword);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, message ?? "Şifre sıfırlanamadı.");
                ViewBag.Step = 2;
                return View("ForgotPassword", model);
            }

            _notyf.Success("Şifreniz başarıyla sıfırlandı! Yeni şifrenizle giriş yapabilirsiniz.");
            return RedirectToAction(nameof(SignIn));
        }

        public new async Task<IActionResult> SignOut(string? returnUrl = null)
        {
            await _identityService.RevokeRefreshToken(); // Keycloak backchannel logout

            var redirectUri = !string.IsNullOrWhiteSpace(returnUrl) && (Url.IsLocalUrl(returnUrl) || returnUrl.StartsWith("/"))
                ? returnUrl
                : (Url.Action("Index", "Home") ?? "/");

            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUri
            };

            // Eğer kullanıcı Keycloak OIDC şeması üzerinden giriş yapmışsa OIDC logout tetikle.
            // Aksi halde (e-posta/şifre ise), sadece local cookie'yi temizle.
            if (User.Identity?.AuthenticationType == "Keycloak")
            {
                return SignOut(
                    properties,
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    "Keycloak");
            }
            else
            {
                return SignOut(
                    properties,
                    CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }

        [HttpGet]
        public IActionResult GoogleSignIn(string? returnUrl = null, bool popup = false)
        {
            var redirectUrl = Url.Action(nameof(GoogleSignInCallback), new { returnUrl, popup }) ?? Url.Action("Index", "Home")!;
            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl,
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            properties.Items["kc_idp_hint"] = "google";
            properties.Items["prompt"] = "select_account";

            return Challenge(properties, "Keycloak");
        }

        [HttpGet]
        public async Task<IActionResult> GoogleSignInCallback(string? returnUrl = null, bool popup = false)
        {
            // Giriş sonrası sepet senkronizasyonunu başlat
            var guestCookieName = "GameGarajGuestId";
            if (HttpContext.Request.Cookies.TryGetValue(guestCookieName, out var guestId) && !string.IsNullOrEmpty(guestId))
            {
                var loggedInUserId = _identityService.GetUserId();
                if (!string.IsNullOrEmpty(loggedInUserId) && loggedInUserId != "anonymous-user")
                {
                    await _basketService.SyncBasketAsync(guestId, loggedInUserId);
                    HttpContext.Response.Cookies.Delete(guestCookieName);
                }
            }

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

        [HttpPost]
        public async Task<IActionResult> SendPasswordResetEmail([FromBody] ResetPasswordEmailModel model)
        {
            var email = model?.Email;
            if (string.IsNullOrWhiteSpace(email))
            {
                email = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value
                     ?? User.Identity?.Name;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { success = false, message = "E-posta adresi belirtilmedi." });
            }

            var (success, message) = await _identityService.SendPasswordResetEmailAsync(email);
            return Json(new { success, message });
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.NewPassword) || model.NewPassword.Length < 6)
            {
                return Json(new { success = false, message = "Yeni şifre en az 6 karakter olmalıdır." });
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                return Json(new { success = false, message = "Şifreler birbiriyle eşleşmiyor." });
            }

            var userId = _identityService.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                userId = User.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Json(new { success = false, message = "Kullanıcı oturumu bulunamadı." });
            }

            var (success, message) = await _identityService.ChangePasswordAsync(userId, model.NewPassword);
            return Json(new { success, message });
        }

        [HttpGet]
        [Route("Auth/AccessDenied")]
        [Route("Admin/Auth/AccessDenied")]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
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

    public class ResetPasswordEmailModel
    {
        public string? Email { get; set; }
    }

    public class ChangePasswordModel
    {
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
