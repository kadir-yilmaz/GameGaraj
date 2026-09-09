namespace GameGaraj.Shared.Events;

public class QuestionCreated
{
    public string QuestionId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class QuestionAnswered
{
    public string QuestionId { get; set; } = string.Empty;
    public string AnswerId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ResponderName { get; set; } = string.Empty;
    public string AnswerText { get; set; } = string.Empty;
    public bool IsEdit { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class QuestionDeleted
{
    public string QuestionId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public DateTime DeletedAt { get; set; }
}

public class ProductDeleted
{
    public string ProductId { get; set; } = string.Empty;
    public DateTime DeletedAt { get; set; }
}

