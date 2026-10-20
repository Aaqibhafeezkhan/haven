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

    /// <summary>
    /// The repository URL in the form used to identify a clone: trimmed, without a trailing slash or
    /// <c>.git</c> suffix, lower-cased. Stored values are left as the user typed them.
    /// </summary>
    public string NormalizedRepository()
    {
        var value = (Repository ?? string.Empty).Trim().TrimEnd('/').ToLowerInvariant();
        return value.EndsWith(".git", StringComparison.Ordinal) ? value[..^4] : value;
    }

    /// <summary>The branch that is cloned: trimmed, defaulting to <c>main</c> when none is set.</summary>
    public string NormalizedBranch() => string.IsNullOrWhiteSpace(Branch) ? "main" : Branch.Trim();

    /// <summary>
    /// Identifies the local clone this binding resolves to. Unlike equality, it ignores <see cref="FilePath"/>
    /// and cosmetic differences in <see cref="Repository"/> and <see cref="Branch"/>.
    /// </summary>
    public string CloneKey() => $"{NormalizedRepository()}\n{NormalizedBranch()}\n{GitCredentialId?.ToString() ?? "none"}";

    /// <summary>True when both bindings resolve to the same local clone.</summary>
    public bool SharesCloneWith(RepositoryBinding other) =>
        string.Equals(CloneKey(), other.CloneKey(), StringComparison.Ordinal);

    public override string ToString()
    {
        return $"{GitCredentialId}:{Repository}:{Branch}:{FilePath}";
    }
}