namespace Haven.Domain.Entities;

public class ApiToken : Entity
{
    public string DisplayName { get; set; }
    public Guid OwnerId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    
    public ICollection<ApiTokenScope> Scopes { get; set; } = new List<ApiTokenScope>();
    
    public bool IsActive(DateTime now) => RevokedAt == null && (ExpiresAt == null || ExpiresAt > now);
    
    public static ApiToken Create(string displayName, Guid ownerId, string tokenHash, string prefix, DateTime createdAt, DateTime? expiresAt = null)
    {
        return new ApiToken
        {
            DisplayName = displayName,
            OwnerId = ownerId,
            TokenHash = tokenHash,
            Prefix = prefix,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt
        };
    }
    
    public void Revoke(DateTime revokedAt)
    {
        RevokedAt = revokedAt;
    }
    
    public void Regenerate(string tokenHash, string prefix, DateTime now)
    {
        if (ExpiresAt.HasValue)
            ExpiresAt = now + (ExpiresAt.Value - CreatedAt);

        TokenHash = tokenHash;
        Prefix = prefix;
        LastUsedAt = null;
    }

    public void MarkUsed(DateTime lastUsedAt)
    {
        LastUsedAt = lastUsedAt;
    }
}