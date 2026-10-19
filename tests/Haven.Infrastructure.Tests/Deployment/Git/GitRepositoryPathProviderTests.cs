using Haven.Domain.ValueObjects;
using Haven.Infrastructure.Deployment.Git;

using Shouldly;

namespace Haven.Infrastructure.Tests.Deployment.Git;

[Category("Unit")]
public sealed class GitRepositoryPathProviderTests
{
    private readonly GitRepositoryPathProvider _sut = new("/data/git");

    [Test]
    public void GetManifestRepositoryPath_ForSameRepositoryBranchAndCredential_ShouldBeShared()
    {
        var a = _sut.GetManifestRepositoryPath(Binding("https://github.com/org/repo", "main", filePath: "a.yaml"));
        var b = _sut.GetManifestRepositoryPath(Binding("https://github.com/org/repo", "main", filePath: "b.yaml"));

        a.ShouldBe(b);
    }

    [Test]
    public void GetManifestRepositoryPath_ShouldNormalizeUrlVariants()
    {
        var plain = _sut.GetManifestRepositoryPath(Binding("https://github.com/org/repo"));

        _sut.GetManifestRepositoryPath(Binding("https://github.com/org/repo.git")).ShouldBe(plain);
        _sut.GetManifestRepositoryPath(Binding("https://github.com/org/repo/")).ShouldBe(plain);
        _sut.GetManifestRepositoryPath(Binding("HTTPS://GitHub.com/Org/Repo")).ShouldBe(plain);
    }

    [Test]
    public void GetManifestRepositoryPath_ForDifferentBranch_ShouldNotShare()
    {
        _sut.GetManifestRepositoryPath(Binding("https://github.com/org/repo", "main"))
            .ShouldNotBe(_sut.GetManifestRepositoryPath(Binding("https://github.com/org/repo", "dev")));
    }

    [Test]
    public void GetManifestRepositoryPath_ForDifferentCredential_ShouldNotShare()
    {
        _sut.GetManifestRepositoryPath(Binding("https://github.com/org/repo", credential: Guid.NewGuid()))
            .ShouldNotBe(_sut.GetManifestRepositoryPath(Binding("https://github.com/org/repo", credential: Guid.NewGuid())));
        _sut.GetManifestRepositoryPath(Binding("https://github.com/org/repo", credential: Guid.NewGuid()))
            .ShouldNotBe(_sut.GetManifestRepositoryPath(Binding("https://github.com/org/repo")));
    }

    [Test]
    public void GetManifestRepositoryPath_WithBlankBranch_ShouldDefaultToMain()
    {
        _sut.GetManifestRepositoryPath(Binding("https://github.com/org/repo", null))
            .ShouldBe(_sut.GetManifestRepositoryPath(Binding("https://github.com/org/repo", "main")));
    }

    [Test]
    public void GetManifestRepositoryPath_ShouldLiveUnderManifestsWithReadableName()
    {
        var path = _sut.GetManifestRepositoryPath(Binding("git@github.com:org/My Repo.git"));

        Path.GetDirectoryName(path).ShouldBe(Path.Combine("/data/git", "manifests"));
        Path.GetFileName(path).ShouldStartWith("my-repo-");
    }

    private static RepositoryBinding Binding(string repository, string? branch = "main", Guid? credential = null, string filePath = "haven.yaml") =>
        new() { Repository = repository, Branch = branch, GitCredentialId = credential, FilePath = filePath };
}
