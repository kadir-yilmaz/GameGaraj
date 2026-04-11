using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Controllers
{
    [Authorize]
    public class DebugController : Controller
    {
        [HttpGet]
        public IActionResult Claims()
        {
            var claims = User.Claims.Select(c => new
            {
                Type = c.Type,
                Value = c.Value
            }).ToList();

            var roles = User.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            var isAdmin = User.IsInRole("Admin");
            var isEditor = User.IsInRole("Editor");
            var isAdminLower = User.IsInRole("admin");
            var isEditorLower = User.IsInRole("editor");

            var debugInfo = new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                Username = User.Identity?.Name,
                AllClaims = claims,
                RoleClaims = roles,
                RoleChecks = new
                {
                    IsAdmin = isAdmin,
                    IsEditor = isEditor,
                    IsAdminLower = isAdminLower,
                    IsEditorLower = isEditorLower
                }
            };

            return Json(debugInfo);
        }
    }
}
