namespace Haven.Application.Features.ApiTokens.Queries;

public class ApiTokenResult
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
