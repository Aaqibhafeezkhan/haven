namespace Haven.Domain.ValueObjects;

public class RepositoryBinding : ValueObject
{
    public Guid? GitCredentialId { get; set; }
    public string? Repository { get; set; }
    public string? Branch { get; set; }
    public string? FilePath { get; set; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return GitCredentialId;
        yield return Repository;
        yield return Branch;
        yield return FilePath;
    }

    public override string ToString()
    {
        return $"{GitCredentialId}:{Repository}:{Branch}:{FilePath}";
    }
}