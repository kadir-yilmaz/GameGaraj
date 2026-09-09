using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GameGaraj.Discussion.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace GameGaraj.Discussion.API.Services;

public class ContentModerationService : IContentModerationService
{
    private const string ProfanityTermType = "profanity";
    private const string ModerationTermsCacheKey = "discussion-api:moderation-terms:v1";

    private readonly DiscussionDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ContentModerationService> _logger;

    private static readonly Regex UrlRegex = new(@"https?://|www\.|\.\bcom\b|\.\bnet\b|\.\borg\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RepeatedCharsRegex = new(@"(.)\1{5,}", RegexOptions.Compiled);

    public ContentModerationService(
        DiscussionDbContext dbContext,
        IDistributedCache cache,
        ILogger<ContentModerationService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public ContentAnalysisResult Analyze(string text, IReadOnlyList<string> recentUserTexts)
    {
        var result = new ContentAnalysisResult();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        DetectProfanity(text, result);
        DetectSpam(text, recentUserTexts, result);
        return result;
    }

    private void DetectProfanity(string text, ContentAnalysisResult result)
    {
        var normalizedText = NormalizeText(text);
        var strippedText = StripSeparators(normalizedText);
        var terms = GetModerationTerms();

        foreach (var term in terms)
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

    private string[] GetModerationTerms()
    {
        var cachedStr = _cache.GetString(ModerationTermsCacheKey);
        if (!string.IsNullOrEmpty(cachedStr))
        {
            try
            {
                var cachedTerms = JsonSerializer.Deserialize<string[]>(cachedStr);
                if (cachedTerms != null)
                {
                    return cachedTerms;
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
                .Where(t => t.IsActive && t.Type == ProfanityTermType)
                .Select(t => t.Term)
                .ToList()
                .Select(t => NormalizeText(t))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            _cache.SetString(ModerationTermsCacheKey, JsonSerializer.Serialize(terms), options);

            return terms;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Moderation terms could not be loaded from database.");
            return [];
        }
    }

    private static void DetectSpam(string text, IReadOnlyList<string> recentUserTexts, ContentAnalysisResult result)
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

        if (recentUserTexts.Any(t => NormalizeText(t) == normalizedText))
        {
            result.IsSpamSuspected = true;
            result.SpamReasons.Add("duplicate-text");
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
        return Regex.Replace(text, @"[\s\.\-_\*\+\#\!\?\,\;\:\'\""/\\]", "");
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
