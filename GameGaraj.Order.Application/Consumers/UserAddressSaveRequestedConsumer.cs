using GameGaraj.Order.Application.Dtos;
using GameGaraj.Order.Application.Services.Abstract;
using GameGaraj.Shared.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace GameGaraj.Order.Application.Consumers
{
    public class UserAddressSaveRequestedConsumer : IConsumer<UserAddressSaveRequested>
    {
        private readonly IUserAddressService _userAddressService;
        private readonly ILogger<UserAddressSaveRequestedConsumer> _logger;

        public UserAddressSaveRequestedConsumer(IUserAddressService userAddressService, ILogger<UserAddressSaveRequestedConsumer> logger)
        {
            _userAddressService = userAddressService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<UserAddressSaveRequested> context)
        {
            var message = context.Message;
            _logger.LogInformation($"[UserAddressSaveRequestedConsumer] Received address save request for UserId: {message.UserId}, Title: {message.Title}");

            try
            {
                var dto = new CreateUserAddressDto
                {
                    Type = (GameGaraj.Order.Domain.Enums.AddressType)message.Type,
                    Title = message.Title,
                    IsDefault = false,
                    FirstName = message.FirstName,
                    LastName = message.LastName,
                    PhoneNumber = message.PhoneNumber,
                    Province = message.Province,
                    District = message.District,
                    Neighborhood = message.Neighborhood,
                    PostalCode = message.PostalCode,
                    AddressDetail = message.AddressDetail
                };

                var result = await _userAddressService.CreateAsync(message.UserId, dto);
                _logger.LogInformation($"[UserAddressSaveRequestedConsumer] Address successfully saved with Id: {result.Id} for UserId: {message.UserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[UserAddressSaveRequestedConsumer] Error saving address for UserId: {message.UserId}");
            }
        }
    }
}
