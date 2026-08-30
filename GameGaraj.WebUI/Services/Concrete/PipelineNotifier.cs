using GameGaraj.WebUI.Hubs;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.SignalR;

namespace GameGaraj.WebUI.Services.Concrete
{
    public class PipelineNotifier : IPipelineNotifier
    {
        public static IPipelineNotifier? Instance { get; private set; }

        private readonly IHubContext<PipelineHub> _hubContext;
        private readonly ILogger<PipelineNotifier> _logger;

        public PipelineNotifier(IHubContext<PipelineHub> hubContext, ILogger<PipelineNotifier> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
            Instance = this;
        }

        public async Task NotifyAsync(string sender, string message, string type = "info", int? step = null, object? data = null)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                await _hubContext.Clients.All.SendAsync("ReceivePipelineEvent", new
                {
                    Sender = sender,
                    Message = message,
                    Type = type, // info, success, warning, error
                    Step = step, // 1 (Catalog), 2 (Order), 3 (Payment), 4 (RabbitMQ), 5 (Downstream)
                    Data = data,
                    Timestamp = timestamp
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[PipelineNotifier] Failed to broadcast event via SignalR");
            }
        }
    }
}
