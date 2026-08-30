using Microsoft.AspNetCore.SignalR;

namespace GameGaraj.WebUI.Hubs
{
    public class PipelineHub : Hub
    {
        public async Task BroadcastPipelineEvent(string sender, string message, string type, int? step = null, object? data = null)
        {
            await Clients.All.SendAsync("ReceivePipelineEvent", new
            {
                Sender = sender,
                Message = message,
                Type = type,
                Step = step,
                Data = data,
                Timestamp = DateTime.Now.ToString("HH:mm:ss")
            });
        }
    }
}
