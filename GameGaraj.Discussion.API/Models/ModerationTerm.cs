namespace GameGaraj.Discussion.API.Models;

public class ModerationTerm
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Term { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
