using Haven.Domain.ValueObjects;

using Shouldly;

namespace Haven.Domain.Tests.ValueObjects;

[Category("Unit")]
public sealed class RepositoryBindingTests
{
    private static RepositoryBinding Binding(string repository, string? branch = "main", string filePath = "a.yaml", Guid? credential = null) =>
        new() { Repository = repository, Branch = branch, FilePath = filePath, GitCredentialId = credential };

    [TestCase("https://h/o/r.git")]
    [TestCase("https://h/o/r/")]
    [TestCase("  https://h/o/r  ")]
    [TestCase("https://H/O/R")]
    public void SharesCloneWith_WhenRepositoryDiffersOnlyCosmetically_ShouldBeTrueButNotEqual(string variant)
    {
        var plain = Binding("https://h/o/r");
        var other = Binding(variant);

        plain.SharesCloneWith(other).ShouldBeTrue();
        plain.ShouldNotBe(other);
    }

    [Test]
    public void SharesCloneWith_WhenBranchIsEmpty_ShouldMatchMain() =>
        Binding("https://h/o/r", branch: null).SharesCloneWith(Binding("https://h/o/r", branch: " main ")).ShouldBeTrue();

    [Test]
    public void SharesCloneWith_WhenOnlyFilePathDiffers_ShouldBeTrue() =>
        Binding("https://h/o/r", filePath: "a.yaml").SharesCloneWith(Binding("https://h/o/r", filePath: "b.yaml")).ShouldBeTrue();

    [Test]
    public void SharesCloneWith_WhenBranchOrCredentialDiffers_ShouldBeFalse()
    {
        var baseline = Binding("https://h/o/r");

        baseline.SharesCloneWith(Binding("https://h/o/r", branch: "dev")).ShouldBeFalse();
        baseline.SharesCloneWith(Binding("https://h/o/r", credential: Guid.NewGuid())).ShouldBeFalse();
        baseline.SharesCloneWith(Binding("https://h/o/other")).ShouldBeFalse();
    }
}
