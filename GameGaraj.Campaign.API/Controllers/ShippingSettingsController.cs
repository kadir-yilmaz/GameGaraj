using Dapper;
using GameGaraj.Campaign.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace GameGaraj.Campaign.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShippingSettingsController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ShippingSettingsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("SqlServer"));
            var setting = await connection.QueryFirstOrDefaultAsync<ShippingSetting>("SELECT * FROM ShippingSettings WHERE Id = 1");
            
            if (setting == null)
            {
                return NotFound();
            }

            return Ok(setting);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] ShippingSetting setting)
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("SqlServer"));
            var sql = @"
                UPDATE ShippingSettings 
                SET FreeShippingThreshold = @FreeShippingThreshold, 
                    DefaultShippingFee = @DefaultShippingFee, 
                    IsActive = @IsActive 
                WHERE Id = 1";

            var result = await connection.ExecuteAsync(sql, new 
            { 
                setting.FreeShippingThreshold, 
                setting.DefaultShippingFee, 
                setting.IsActive 
            });

            if (result > 0)
                return Ok();

            return BadRequest("Kargo ayarları güncellenemedi.");
        }
    }
}
