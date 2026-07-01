using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GameGaraj.Review.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace GameGaraj.Review.API.Services;

public class ContentModerationService : IContentModerationService
{
    private const string ProfanityTermType = "profanity";
    private const string PriceTermType = "price";
    private const string ModerationTermsCacheKey = "review-api:moderation-terms:v1";

    private readonly ReviewDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ContentModerationService> _logger;

    private static readonly Regex PricePatternRegex = new(@"(\d+[\.,]?\d*)\s*(tl|try|lira|kurus|\$|\u20ac|eur|usd)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new(@"https?://|www\.|\.com|\.net|\.org", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RepeatedCharsRegex = new(@"(.)\1{5,}", RegexOptions.Compiled);

    public ContentModerationService(
        ReviewDbContext dbContext,
        IDistributedCache cache,
        ILogger<ContentModerationService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
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

    private void DetectProfanity(string text, ContentAnalysisResult result)
    {
        var normalizedText = NormalizeText(text);
        var strippedText = StripSeparators(normalizedText);
        var terms = GetModerationTerms();

        foreach (var term in terms.ProfanityTerms)
        {
            if (ContainsWord(normalizedText, term) ||
                (term.Length >= 3 && strippedText.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                result.HasProfanity = true;
                if (!result.DetectedProfanities.Contains(term))
                {
                    result.DetectedProfanities.Add(term);
                }
            }
        }
    }

    private void DetectPriceInfo(string text, ContentAnalysisResult result)
    {
        var lowerText = NormalizeText(text);
        var terms = GetModerationTerms();

        foreach (var keyword in terms.PriceTerms)
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

    private ModerationTermsSnapshot GetModerationTerms()
    {
        var cachedStr = _cache.GetString(ModerationTermsCacheKey);
        if (!string.IsNullOrEmpty(cachedStr))
        {
            try
            {
                var cachedSnapshot = JsonSerializer.Deserialize<ModerationTermsSnapshot>(cachedStr);
                if (cachedSnapshot != null)
                {
                    return cachedSnapshot;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached moderation terms.");
            }
        }

        try
        {
            var terms = _dbContext.ModerationTerms
                .AsNoTracking()
                .Where(term => term.IsActive)
                .Select(term => new { term.Type, term.Term })
                .ToList();

            var snapshot = new ModerationTermsSnapshot(
                terms
                    .Where(term => term.Type == ProfanityTermType)
                    .Select(term => NormalizeText(term.Term))
                    .Where(term => !string.IsNullOrWhiteSpace(term))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                terms
                    .Where(term => term.Type == PriceTermType)
                    .Select(term => NormalizeText(term.Term))
                    .Where(term => !string.IsNullOrWhiteSpace(term))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray());
                    
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            _cache.SetString(ModerationTermsCacheKey, JsonSerializer.Serialize(snapshot), options);

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Moderation terms could not be loaded from database.");
            return ModerationTermsSnapshot.Empty;
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

    private sealed record ModerationTermsSnapshot(string[] ProfanityTerms, string[] PriceTerms)
    {
        public static readonly ModerationTermsSnapshot Empty = new([], []);
    }
}
