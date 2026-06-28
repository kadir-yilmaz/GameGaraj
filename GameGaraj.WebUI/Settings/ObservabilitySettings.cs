namespace GameGaraj.WebUI.Settings
{
    public class ObservabilitySettings
    {
        public string ElasticSearchUri { get; set; } = string.Empty;
        public Dictionary<string, string> Services { get; set; } = new();
    }
}
