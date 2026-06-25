using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameGaraj.Review.API.Services;

public class ContentModerationService : IContentModerationService
{
    private readonly string[] _profanityRoots;
    private readonly string[] _priceKeywords;
    private readonly ILogger<ContentModerationService> _logger;

    private static readonly Regex PricePatternRegex = new(@"(\d+[\.,]?\d*)\s*(tl|try|lira|kurus|\$|\u20ac|eur|usd)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new(@"https?://|www\.|\.com|\.net|\.org", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RepeatedCharsRegex = new(@"(.)\1{5,}", RegexOptions.Compiled);

    private static readonly string[] DefaultProfanityRoots =
    [
        "amk",
        "aq",
        "siktir",
        "orospu",
        "pic",
        "oc",
        "pezevenk",
        "gavat",
        "ibne",
        "got",
        "yarak",
        "salak",
        "aptal",
        "mal",
        "serefsiz",
        "namussuz",
        "kahpe",
        "pust",
        "bok",
        "sktr"
    ];

    public ContentModerationService(IConfiguration configuration, ILogger<ContentModerationService> logger)
    {
        _logger = logger;
        var dataDir = ResolveDataDirectory(configuration);
        Directory.CreateDirectory(dataDir);

        _profanityRoots = LoadWordList(Path.Combine(dataDir, "profanity-words.json"), DefaultProfanityRoots);
        _priceKeywords = LoadWordList(Path.Combine(dataDir, "price-keywords.json"));
    }

    public ContentAnalysisResult Analyze(string text, IReadOnlyList<string> recentUserComments)
    {
        var result = new ContentAnalysisResult();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        DetectProfanity(text, result);
        DetectPriceInfo(text, result);
        DetectSpam(text, recentUserComments, result);
        return result;
    }

    private static string ResolveDataDirectory(IConfiguration configuration)
    {
        var configuredPath = configuration["Moderation:DataPath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
    }

    private string[] LoadWordList(string path, IEnumerable<string>? fallbackWords = null)
    {
        var fallback = (fallbackWords ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(NormalizeText);

        if (!File.Exists(path))
        {
            File.WriteAllText(path, "[]", Encoding.UTF8);
            _logger.LogWarning("Moderation word list was missing and an empty file was created at {Path}", path);
            return fallback.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        try
        {
            var json = File.ReadAllText(path);
            var words = JsonSerializer.Deserialize<string[]>(json)?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(NormalizeText) ?? [];

            return words
                .Concat(fallback)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Moderation word list could not be loaded from {Path}", path);
            return fallback.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    private void DetectProfanity(string text, ContentAnalysisResult result)
    {
        var normalizedText = NormalizeText(text);
        var strippedText = StripSeparators(normalizedText);

        foreach (var profanity in _profanityRoots)
        {
            if (ContainsWord(normalizedText, profanity) || strippedText.Contains(profanity, StringComparison.OrdinalIgnoreCase))
            {
                result.HasProfanity = true;
                if (!result.DetectedProfanities.Contains(profanity))
                {
                    result.DetectedProfanities.Add(profanity);
                }
            }
        }
    }

    private void DetectPriceInfo(string text, ContentAnalysisResult result)
    {
        var lowerText = NormalizeText(text);

        foreach (var keyword in _priceKeywords)
        {
            if (lowerText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                result.HasPriceInfo = true;
                result.DetectedPricePatterns.Add(keyword);
            }
        }

        foreach (Match match in PricePatternRegex.Matches(text))
        {
            result.HasPriceInfo = true;
            result.DetectedPricePatterns.Add(match.Value);
        }
    }

    private static void DetectSpam(string text, IReadOnlyList<string> recentUserComments, ContentAnalysisResult result)
    {
        var normalizedText = NormalizeText(text);

        if (UrlRegex.Matches(text).Count >= 2)
        {
            result.IsSpamSuspected = true;
            result.SpamReasons.Add("multiple-links");
        }

        if (RepeatedCharsRegex.IsMatch(normalizedText))
        {
            result.IsSpamSuspected = true;
            result.SpamReasons.Add("repeated-characters");
        }

        if (recentUserComments.Any(comment => NormalizeText(comment) == normalizedText))
        {
            result.IsSpamSuspected = true;
            result.SpamReasons.Add("duplicate-comment");
        }
    }

    private static string NormalizeText(string text)
    {
        var normalized = text
            .Trim()
            .ToLowerInvariant()
            .Replace('\u0131', 'i')
            .Replace('\u011f', 'g')
            .Replace('\u00fc', 'u')
            .Replace('\u015f', 's')
            .Replace('\u00f6', 'o')
            .Replace('\u00e7', 'c')
            .Replace('\u00e2', 'a')
            .Replace('\u00ee', 'i')
            .Replace('\u00fb', 'u')
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string StripSeparators(string text)
    {
        return Regex.Replace(text, @"[\s\.\-_\*\+\#\!\?\,\;\:\'\""\/\\]", "");
    }

    private static bool ContainsWord(string text, string word)
    {
        if (word.Length <= 3)
        {
            return Regex.IsMatch(text, $@"(?<!\w){Regex.Escape(word)}(?!\w)", RegexOptions.IgnoreCase);
        }

        return text.Contains(word, StringComparison.OrdinalIgnoreCase);
    }
}
