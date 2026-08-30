namespace GameGaraj.WebUI.Services.Abstract
{
    public interface IPipelineNotifier
    {
        Task NotifyAsync(string sender, string message, string type = "info", int? step = null, object? data = null);
    }
}
