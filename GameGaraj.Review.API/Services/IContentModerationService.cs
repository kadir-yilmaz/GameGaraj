namespace GameGaraj.Review.API.Services;

public interface IContentModerationService
{
    ContentAnalysisResult Analyze(string text, IReadOnlyList<string> recentUserComments);
}

public class ContentAnalysisResult
{
    public bool HasProfanity { get; set; }
    public bool HasPriceInfo { get; set; }
    public bool IsSpamSuspected { get; set; }
    public List<string> DetectedProfanities { get; set; } = new();
    public List<string> DetectedPricePatterns { get; set; } = new();
    public List<string> SpamReasons { get; set; } = new();
}
