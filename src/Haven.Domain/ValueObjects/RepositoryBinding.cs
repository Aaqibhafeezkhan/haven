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

    public static RepositoryBinding FromString(string value)
    {
        if (!TryParse(value, out var repositoryBinding))
            throw new ArgumentException($"Invalid repository binding string: {value}");

        return repositoryBinding!;
    }

    public static bool TryParse(string value, out RepositoryBinding? repositoryBinding)
    {
        repositoryBinding = null;
        if (string.IsNullOrEmpty(value))
            return false;

        var parts = value.Split(':');
        if (parts.Length != 4)
            return false;

        repositoryBinding = new RepositoryBinding
        {
            GitCredentialId = Guid.TryParse(parts[0], out var guid) ? guid : null,
            Repository = parts[1],
            Branch = parts[2],
            FilePath = parts[3]
        };

        return true;
    }

    public override string ToString()
    {
        return $"{GitCredentialId}:{Repository}:{Branch}:{FilePath}";
    }
}