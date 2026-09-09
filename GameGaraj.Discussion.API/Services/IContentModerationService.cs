namespace GameGaraj.Discussion.API.Services;

public interface IContentModerationService
{
    ContentAnalysisResult Analyze(string text, IReadOnlyList<string> recentUserTexts);
}

public class ContentAnalysisResult
{
    public bool HasProfanity { get; set; }
    public bool IsSpamSuspected { get; set; }
    public List<string> DetectedProfanities { get; set; } = new();
    public List<string> SpamReasons { get; set; } = new();
}
