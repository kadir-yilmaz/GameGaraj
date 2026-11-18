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
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public IActionResult Profile()
        {
            var userId = _identityService.GetUserId();
            var email = GetClaimValue(System.Security.Claims.ClaimTypes.Email, "email") ?? User.Identity?.Name ?? "";
            var name = GetClaimValue(System.Security.Claims.ClaimTypes.GivenName, "given_name") ?? "";
            var surname = GetClaimValue(System.Security.Claims.ClaimTypes.Surname, "family_name") ?? "";

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname))
            {
                var fullName = GetClaimValue("name") ?? User.Identity?.Name ?? "";
                var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (string.IsNullOrWhiteSpace(name) && parts.Length > 0)
                    name = parts[0];
                if (string.IsNullOrWhiteSpace(surname) && parts.Length > 1)
                    surname = string.Join(' ', parts.Skip(1));
            }

            var model = new ProfileViewModel
            {
                UserId = userId,
                Email = email,
                Name = name,
                Surname = surname,
                Roles = User.Claims
                    .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role")
                    .Select(c => c.Value)
                    .Distinct()
                    .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileModel model)
        {
            var firstName = model?.FirstName?.Trim() ?? string.Empty;
            var lastName = model?.LastName?.Trim() ?? string.Empty;

            if (firstName.Length < 2 || lastName.Length < 2)
            {
                return Json(new { success = false, message = "Ad ve soyad en az 2 karakter olmalıdır." });
            }

            var userId = _identityService.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Json(new { success = false, message = "Kullanıcı oturumu bulunamadı." });
            }

            var (success, message) = await _identityService.UpdateUserProfileAsync(userId, firstName, lastName);
            if (!success)
            {
                return Json(new { success = false, message });
            }

            await RefreshSessionNameClaimsAsync(firstName, lastName);
            return Json(new { success = true, message, fullName = $"{firstName} {lastName}" });
        }

        private string? GetClaimValue(params string[] claimTypes) =>
            claimTypes
                .Select(t => User.Claims.FirstOrDefault(c => c.Type == t)?.Value)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        private async Task RefreshSessionNameClaimsAsync(string firstName, string lastName)
        {
            if (User.Identity is not System.Security.Claims.ClaimsIdentity identity)
            {
                return;
            }

            var replacedTypes = new HashSet<string>
            {
                System.Security.Claims.ClaimTypes.GivenName,
                "given_name",
                System.Security.Claims.ClaimTypes.Surname,
                "family_name",
                "name"
            };

            var keptClaims = identity.Claims.Where(c => !replacedTypes.Contains(c.Type)).ToList();
            keptClaims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.GivenName, firstName));
            keptClaims.Add(new System.Security.Claims.Claim("given_name", firstName));
            keptClaims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Surname, lastName));
            keptClaims.Add(new System.Security.Claims.Claim("family_name", lastName));
            keptClaims.Add(new System.Security.Claims.Claim("name", $"{firstName} {lastName}"));

            var newIdentity = new System.Security.Claims.ClaimsIdentity(keptClaims, identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType);
            await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignInAsync(
                HttpContext,
                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
                new System.Security.Claims.ClaimsPrincipal(newIdentity));
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

    public class UpdateProfileModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }
}
