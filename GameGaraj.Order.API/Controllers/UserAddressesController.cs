using GameGaraj.Order.Application.Dtos;
using GameGaraj.Order.Application.Services.Abstract;
using GameGaraj.Order.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameGaraj.Order.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class UserAddressesController : ControllerBase
    {
        private readonly IUserAddressService _service;
        private readonly ILogger<UserAddressesController> _logger;

        public UserAddressesController(IUserAddressService service, ILogger<UserAddressesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private string GetUserId()
        {
            // Try to get from claims first (if authenticated)
            var userIdFromClaims = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst("sub")?.Value;
            
            if (!string.IsNullOrEmpty(userIdFromClaims))
                return userIdFromClaims;
            
            // Fallback to X-User-Id header (sent by WebUI)
            if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader))
                return userIdHeader.ToString();
            
            return string.Empty;
        }

        /// <summary>
        /// Kullanıcının tüm adreslerini getirir
        /// </summary>
        /// <param name="type">Adres tipi (opsiyonel): 1=Delivery, 2=Invoice</param>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] AddressType? type = null)
        {
            var userId = GetUserId();
            _logger.LogInformation($"[UserAddresses] GetAll - UserId: {userId}, Type: {type}");
            
            var addresses = await _service.GetUserAddressesAsync(userId, type);
            return Ok(addresses);
        }

        /// <summary>
        /// ID'ye göre adres getirir
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = GetUserId();
            var address = await _service.GetByIdAsync(id, userId);
            
            if (address == null)
            {
                _logger.LogWarning($"[UserAddresses] Address not found - Id: {id}, UserId: {userId}");
                return NotFound(new { message = "Adres bulunamadı." });
            }
            
            return Ok(address);
        }

        /// <summary>
        /// Varsayılan adresi getirir
        /// </summary>
        /// <param name="type">Adres tipi: 1=Delivery, 2=Invoice</param>
        [HttpGet("default/{type}")]
        public async Task<IActionResult> GetDefault(AddressType type)
        {
            var userId = GetUserId();
            var address = await _service.GetDefaultAddressAsync(userId, type);
            
            if (address == null)
            {
                _logger.LogInformation($"[UserAddresses] No default address - UserId: {userId}, Type: {type}");
                return NotFound(new { message = "Varsayılan adres bulunamadı." });
            }
            
            return Ok(address);
        }

        /// <summary>
        /// Yeni adres oluşturur
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserAddressDto dto)
        {
            try
            {
                var userId = GetUserId();
                _logger.LogInformation($"[UserAddresses] Creating address - UserId: {userId}, Type: {dto.Type}");
                
                var address = await _service.CreateAsync(userId, dto);
                
                _logger.LogInformation($"[UserAddresses] Address created - Id: {address.Id}");
                return CreatedAtAction(nameof(GetById), new { id = address.Id }, address);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning($"[UserAddresses] Create failed - {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Adresi günceller
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserAddressDto dto)
        {
            if (id != dto.Id)
            {
                return BadRequest(new { message = "ID uyuşmazlığı." });
            }
            
            try
            {
                var userId = GetUserId();
                _logger.LogInformation($"[UserAddresses] Updating address - Id: {id}, UserId: {userId}");
                
                var result = await _service.UpdateAsync(userId, dto);
                
                if (!result)
                {
                    _logger.LogWarning($"[UserAddresses] Address not found for update - Id: {id}");
                    return NotFound(new { message = "Adres bulunamadı." });
                }
                
                _logger.LogInformation($"[UserAddresses] Address updated - Id: {id}");
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning($"[UserAddresses] Unauthorized update attempt - Id: {id}, Message: {ex.Message}");
                return Forbid();
            }
        }

        /// <summary>
        /// Adresi siler
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userId = GetUserId();
                _logger.LogInformation($"[UserAddresses] Deleting address - Id: {id}, UserId: {userId}");
                
                var result = await _service.DeleteAsync(id, userId);
                
                if (!result)
                {
                    _logger.LogWarning($"[UserAddresses] Address not found for delete - Id: {id}");
                    return NotFound(new { message = "Adres bulunamadı." });
                }
                
                _logger.LogInformation($"[UserAddresses] Address deleted - Id: {id}");
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning($"[UserAddresses] Unauthorized delete attempt - Id: {id}, Message: {ex.Message}");
                return Forbid();
            }
        }

        /// <summary>
        /// Adresi varsayılan yapar
        /// </summary>
        [HttpPost("{id}/set-default")]
        public async Task<IActionResult> SetDefault(int id, [FromQuery] AddressType type)
        {
            try
            {
                var userId = GetUserId();
                _logger.LogInformation($"[UserAddresses] Setting default address - Id: {id}, UserId: {userId}, Type: {type}");
                
                var result = await _service.SetAsDefaultAsync(id, userId, type);
                
                if (!result)
                {
                    _logger.LogWarning($"[UserAddresses] Address not found for set default - Id: {id}");
                    return NotFound(new { message = "Adres bulunamadı." });
                }
                
                _logger.LogInformation($"[UserAddresses] Default address set - Id: {id}");
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning($"[UserAddresses] Unauthorized set default attempt - Id: {id}, Message: {ex.Message}");
                return Forbid();
            }
        }
    }
}
