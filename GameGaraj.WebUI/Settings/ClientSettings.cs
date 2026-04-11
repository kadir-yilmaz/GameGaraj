namespace GameGaraj.WebUI.Settings
{
    public class ClientSettings
    {
        public Client WebClient { get; set; } = new();
        public Client WebClientForUser { get; set; } = new();
    }
}
