using GameGaraj.Discussion.API.Data;
using GameGaraj.Discussion.API.Dtos;
using GameGaraj.Discussion.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Discussion.API.Services;

public class DiscussionService : IDiscussionService
{
    private readonly DiscussionDbContext _context;
    private readonly IContentModerationService _contentModerationService;
    private readonly ILogger<DiscussionService> _logger;

    public DiscussionService(
        DiscussionDbContext context,
        IContentModerationService contentModerationService,
        ILogger<DiscussionService> logger)
    {
        _context = context;
        _contentModerationService = contentModerationService;
        _logger = logger;
    }

    // ─── Public ─────────────────────────────────────────────────────

    public async Task<QuestionListDto> GetProductQuestionsAsync(string productId, int page, int pageSize, string? currentUserId, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 0);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.Questions
            .AsNoTracking()
            .Include(q => q.Answer)
            .Where(q => q.ProductId == productId && !q.IsArchived);

        // Moderasyon Koruması: Küfür veya spam içeren sorular yalnızca soruyu soran kullanıcıya görünür, herkese açık listede filtrelenir.
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            query = query.Where(q => !q.HasProfanity && !q.IsSpamSuspected);
        }
        else
        {
            query = query.Where(q => (!q.HasProfanity && !q.IsSpamSuspected) || q.UserId == currentUserId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var questions = await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Kullanıcının verdiği oyları tek sorguda çek
        Dictionary<string, bool> userVotes = new();
        if (!string.IsNullOrWhiteSpace(currentUserId))
        {
            var answerIds = questions
                .Where(q => q.Answer != null)
                .Select(q => q.Answer!.Id)
                .ToList();

            if (answerIds.Any())
            {
                userVotes = await _context.AnswerVotes
                    .AsNoTracking()
                    .Where(v => v.UserId == currentUserId && answerIds.Contains(v.AnswerId))
                    .ToDictionaryAsync(v => v.AnswerId, v => v.IsHelpful, cancellationToken);
            }
        }

        return new QuestionListDto
        {
            Items = questions.Select(q => MapQuestion(q, userVotes)).ToList(),
            TotalCount = totalCount
        };
    }

    public async Task<List<QuestionDto>> GetMyQuestionsAsync(string userId, CancellationToken cancellationToken)
    {
        var questions = await _context.Questions
            .AsNoTracking()
            .Include(q => q.Answer)
            .Where(q => q.UserId == userId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken);

        return questions.Select(q => MapQuestion(q, null)).ToList();
    }

    public async Task<DiscussionMutationResultDto> CreateQuestionAsync(CreateQuestionDto dto, UserContext user, CancellationToken cancellationToken)
    {
        // Validation
        var validationError = ValidateQuestionText(dto.QuestionText);
        if (!string.IsNullOrEmpty(validationError))
        {
            return new DiscussionMutationResultDto { Succeeded = false, Message = validationError };
        }

        if (string.IsNullOrWhiteSpace(dto.ProductId))
        {
            return new DiscussionMutationResultDto { Succeeded = false, Message = "Ürün bilgisi gereklidir." };
        }

        // Idempotency check
        if (!string.IsNullOrWhiteSpace(dto.IdempotencyKey))
        {
            var exists = await _context.Questions
                .IgnoreQueryFilters()
                .AnyAsync(q => q.IdempotencyKey == dto.IdempotencyKey, cancellationToken);

            if (exists)
            {
                return new DiscussionMutationResultDto { Succeeded = true, Message = "Sorunuz daha önce oluşturuldu." };
            }
        }

        // Content moderation
        var recentTexts = await _context.Questions
            .AsNoTracking()
            .Where(q => q.UserId == user.UserId && q.CreatedAt >= DateTime.UtcNow.AddHours(-24))
            .OrderByDescending(q => q.CreatedAt)
            .Take(10)
            .Select(q => q.QuestionText)
            .ToListAsync(cancellationToken);

        var analysis = _contentModerationService.Analyze(dto.QuestionText, recentTexts);

        var question = new Question
        {
            Id = Guid.NewGuid().ToString(),
            ProductId = dto.ProductId,
            ProductName = dto.ProductName,
            ProductImageUrl = dto.ProductImageUrl,
            UserId = user.UserId,
            UserName = user.UserName,
            QuestionText = dto.QuestionText.Trim(),
            Status = QuestionStatus.Pending,
            IdempotencyKey = dto.IdempotencyKey,
            HasProfanity = analysis.HasProfanity,
            IsSpamSuspected = analysis.IsSpamSuspected,
            CreatedAt = DateTime.UtcNow
        };

        _context.Questions.Add(question);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Question {QuestionId} created by user {UserId} for product {ProductId}",
            question.Id, user.UserId, dto.ProductId);

        return new DiscussionMutationResultDto
        {
            Succeeded = true,
            Id = question.Id,
            HasProfanity = analysis.HasProfanity,
            IsSpamSuspected = analysis.IsSpamSuspected,
            Message = "Sorunuz başarıyla gönderildi. Cevaplandığında bilgilendirileceksiniz."
        };
    }

    public async Task<DiscussionMutationResultDto> DeleteQuestionAsync(string questionId, string userId, CancellationToken cancellationToken)
    {
        var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken);
        if (question == null)
        {
            return new DiscussionMutationResultDto { Succeeded = false, Message = "Soru bulunamadı." };
        }

        if (question.UserId != userId)
        {
            return new DiscussionMutationResultDto { Succeeded = false, Message = "Yalnızca kendi sorunuzu silebilirsiniz." };
        }

        if (question.Status == QuestionStatus.Answered)
        {
            return new DiscussionMutationResultDto { Succeeded = false, Message = "Cevaplanmış sorular silinemez." };
        }

        question.IsDeleted = true;
        question.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new DiscussionMutationResultDto { Succeeded = true, Message = "Sorunuz silindi." };
    }

    public async Task<VoteResultDto> VoteAnswerAsync(string answerId, bool isHelpful, string userId, CancellationToken cancellationToken)
    {
        var answer = await _context.Answers
            .FirstOrDefaultAsync(a => a.Id == answerId, cancellationToken);

        if (answer == null)
        {
            return new VoteResultDto { Succeeded = false, Message = "Cevap bulunamadı." };
        }

        var existingVote = await _context.AnswerVotes
            .FirstOrDefaultAsync(v => v.AnswerId == answerId && v.UserId == userId, cancellationToken);

        string? userVote = null;

        if (existingVote == null)
        {
            // 1. Yeni Oy Veriliyor
            var newVote = new AnswerVote
            {
                Id = Guid.NewGuid().ToString(),
                AnswerId = answerId,
                UserId = userId,
                IsHelpful = isHelpful,
                CreatedAt = DateTime.UtcNow
            };
            _context.AnswerVotes.Add(newVote);

            if (isHelpful)
                answer.HelpfulCount++;
            else
                answer.DislikeCount++;

            userVote = isHelpful ? "helpful" : "dislike";
        }
        else if (existingVote.IsHelpful == isHelpful)
        {
            // 2. Aynı oya tekrar tıklandı -> Oyu kaldır (Toggle Off)
            _context.AnswerVotes.Remove(existingVote);

            if (isHelpful)
                answer.HelpfulCount = Math.Max(0, answer.HelpfulCount - 1);
            else
                answer.DislikeCount = Math.Max(0, answer.DislikeCount - 1);

            userVote = null;
        }
        else
        {
            // 3. Zıt oya tıklandı -> Oyu değiştir (Switch)
            existingVote.IsHelpful = isHelpful;
            existingVote.CreatedAt = DateTime.UtcNow;

            if (isHelpful)
            {
                answer.HelpfulCount++;
                answer.DislikeCount = Math.Max(0, answer.DislikeCount - 1);
            }
            else
            {
                answer.DislikeCount++;
                answer.HelpfulCount = Math.Max(0, answer.HelpfulCount - 1);
            }

            userVote = isHelpful ? "helpful" : "dislike";
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Answer {AnswerId} voted by user {UserId}. Helpful: {HelpfulCount}, Dislike: {DislikeCount}, UserVote: {UserVote}",
            answerId, userId, answer.HelpfulCount, answer.DislikeCount, userVote);

        return new VoteResultDto
        {
            Succeeded = true,
            AnswerId = answerId,
            HelpfulCount = answer.HelpfulCount,
            DislikeCount = answer.DislikeCount,
            UserVote = userVote,
            Message = userVote != null ? "Geri bildiriminiz kaydedildi." : "Oyunuz geri alındı."
        };
    }

    // ─── Admin ──────────────────────────────────────────────────────

    public async Task<AdminQuestionListDto> GetAdminQuestionsAsync(int? status, bool? archived, string? query, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var baseQuery = _context.Questions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(q => q.Answer)
            .Where(q => !q.IsDeleted)
            .AsQueryable();

        var pendingCount = await baseQuery.LongCountAsync(q => !q.IsArchived && q.Status == QuestionStatus.Pending, cancellationToken);
        var answeredCount = await baseQuery.LongCountAsync(q => !q.IsArchived && q.Status == QuestionStatus.Answered, cancellationToken);
        var archivedCount = await baseQuery.LongCountAsync(q => q.IsArchived, cancellationToken);

        var questionsQuery = baseQuery;

        if (archived.HasValue && archived.Value)
        {
            questionsQuery = questionsQuery.Where(q => q.IsArchived);
        }
        else if (!archived.HasValue || !archived.Value)
        {
            // Varsayılan: arşivlenmemiş soruları göster
            questionsQuery = questionsQuery.Where(q => !q.IsArchived);

            if (status.HasValue)
            {
                questionsQuery = questionsQuery.Where(q => q.Status == (QuestionStatus)status.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = query.Trim().ToLower();
            questionsQuery = questionsQuery.Where(q =>
                q.ProductName.ToLower().Contains(normalized) ||
                q.UserName.ToLower().Contains(normalized) ||
                q.QuestionText.ToLower().Contains(normalized));
        }

        var totalCount = await questionsQuery.LongCountAsync(cancellationToken);

        var questionEntities = await questionsQuery
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AdminQuestionListDto
        {
            Items = questionEntities.Select(q => MapQuestion(q, null)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
            PendingCount = pendingCount,
            AnsweredCount = answeredCount,
            ArchivedCount = archivedCount
        };
    }

    public async Task<DiscussionMutationResultDto> AnswerQuestionAsync(string questionId, CreateAnswerDto dto, UserContext admin, CancellationToken cancellationToken)
    {
        // Validation
        var validationError = ValidateAnswerText(dto.AnswerText);
        if (!string.IsNullOrEmpty(validationError))
        {
            return new DiscussionMutationResultDto { Succeeded = false, Message = validationError };
        }

        // Idempotency check
        if (!string.IsNullOrWhiteSpace(dto.IdempotencyKey))
        {
            var existingAnswer = await _context.Answers
                .AnyAsync(a => a.IdempotencyKey == dto.IdempotencyKey, cancellationToken);

            if (existingAnswer)
            {
                return new DiscussionMutationResultDto { Succeeded = true, Message = "Bu cevap daha önce kaydedildi." };
            }
        }

        var question = await _context.Questions
            .Include(q => q.Answer)
            .FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken);

        if (question == null)
        {
            return new DiscussionMutationResultDto { Succeeded = false, Message = "Soru bulunamadı." };
        }

        if (question.IsArchived)
        {
            return new DiscussionMutationResultDto { Succeeded = false, Message = "Arşivlenmiş sorulara cevap verilemez." };
        }

        // Eğer soru daha önce cevaplanmışsa cevabı güncelle (Edit/Update desteği)
        if (question.Answer != null)
        {
            question.Answer.AnswerText = dto.AnswerText.Trim();
            question.Answer.ResponderId = admin.UserId;
            question.Answer.ResponderName = admin.UserName;
            question.Answer.CreatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Question {QuestionId} answer updated by admin {AdminId}", questionId, admin.UserId);

            return new DiscussionMutationResultDto
            {
                Succeeded = true,
                Id = question.Answer.Id,
                ProductId = question.ProductId,
                ProductName = question.ProductName,
                UserId = question.UserId,
                IsEdit = true,
                Message = "Cevap başarıyla güncellendi."
            };
        }

        var answer = new Answer
        {
            Id = Guid.NewGuid().ToString(),
            QuestionId = questionId,
            ResponderId = admin.UserId,
            ResponderName = admin.UserName,
            AnswerText = dto.AnswerText.Trim(),
            IdempotencyKey = dto.IdempotencyKey,
            CreatedAt = DateTime.UtcNow
        };

        question.Status = QuestionStatus.Answered;
        _context.Answers.Add(answer);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Concurrency durumunda: başka bir admin aynı anda cevap verdiyse (Madde 16)
            _logger.LogWarning("Concurrent answer attempt for question {QuestionId}", questionId);
            return new DiscussionMutationResultDto { Succeeded = false, Message = "Bu soru başka bir yönetici tarafından cevaplanmış." };
        }

        _logger.LogInformation("Question {QuestionId} answered by admin {AdminId}", questionId, admin.UserId);

        return new DiscussionMutationResultDto
        {
            Succeeded = true,
            Id = answer.Id,
            ProductId = question.ProductId,
            ProductName = question.ProductName,
            UserId = question.UserId,
            IsEdit = false,
            Message = "Cevap başarıyla kaydedildi."
        };
    }

    public async Task<DiscussionMutationResultDto> DeleteQuestionAsAdminAsync(string questionId, CancellationToken cancellationToken)
    {
        var question = await _context.Questions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(q => q.Id == questionId && !q.IsDeleted, cancellationToken);

        if (question == null)
        {
            return new DiscussionMutationResultDto { Succeeded = false, Message = "Soru bulunamadı." };
        }

        question.IsDeleted = true;
        question.DeletedAt = DateTime.UtcNow;
        question.AdminNote = "Admin tarafından silindi.";
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Question {QuestionId} deleted by admin", questionId);

        return new DiscussionMutationResultDto { Succeeded = true, Id = question.Id, ProductId = question.ProductId, Message = "Soru silindi." };
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private static string ValidateQuestionText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Soru metni boş olamaz.";
        }

        var trimmed = text.Trim();
        if (trimmed.Length < 10)
        {
            return "Soru en az 10 karakter olmalıdır.";
        }

        if (trimmed.Length > 500)
        {
            return "Soru en fazla 500 karakter olabilir.";
        }

        return string.Empty;
    }

    private static string ValidateAnswerText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Cevap metni boş olamaz.";
        }

        var trimmed = text.Trim();
        if (trimmed.Length < 5)
        {
            return "Cevap en az 5 karakter olmalıdır.";
        }

        if (trimmed.Length > 1000)
        {
            return "Cevap en fazla 1000 karakter olabilir.";
        }

        return string.Empty;
    }

    private static QuestionDto MapQuestion(Question question, Dictionary<string, bool>? userVotes = null)
    {
        string? userVote = null;
        if (question.Answer != null && userVotes != null && userVotes.TryGetValue(question.Answer.Id, out var isHelpful))
        {
            userVote = isHelpful ? "helpful" : "dislike";
        }

        return new QuestionDto
        {
            Id = question.Id,
            ProductId = question.ProductId,
            ProductName = question.ProductName,
            ProductImageUrl = question.ProductImageUrl,
            UserId = question.UserId,
            UserName = string.IsNullOrWhiteSpace(question.UserName) ? "Anonim" : question.UserName,
            QuestionText = question.QuestionText,
            Status = (int)question.Status,
            IsArchived = question.IsArchived,
            HasProfanity = question.HasProfanity,
            IsSpamSuspected = question.IsSpamSuspected,
            AdminNote = question.AdminNote,
            CreatedAt = question.CreatedAt,
            Answer = question.Answer == null ? null : new AnswerDto
            {
                Id = question.Answer.Id,
                ResponderId = question.Answer.ResponderId,
                ResponderName = question.Answer.ResponderName,
                AnswerText = question.Answer.AnswerText,
                HelpfulCount = question.Answer.HelpfulCount,
                DislikeCount = question.Answer.DislikeCount,
                UserVote = userVote,
                CreatedAt = question.Answer.CreatedAt
            }
        };
    }
}
