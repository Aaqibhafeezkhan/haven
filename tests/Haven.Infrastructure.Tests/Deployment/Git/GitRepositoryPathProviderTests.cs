using Haven.Domain.ValueObjects;
using Haven.Infrastructure.Deployment.Git;

using Shouldly;

namespace Haven.Infrastructure.Tests.Deployment.Git;

[Category("Unit")]
public sealed class GitRepositoryPathProviderTests
{
    private readonly GitRepositoryPathProvider _sut = new("/data/git");

    [Test]
    public void GetRepositoryPath_ForSameRepositoryBranchAndCredential_ShouldBeShared()
    {
        var a = _sut.GetRepositoryPath(Binding("https://github.com/org/repo", "main", filePath: "a.yaml"));
        var b = _sut.GetRepositoryPath(Binding("https://github.com/org/repo", "main", filePath: "b.yaml"));

        a.ShouldBe(b);
    }

    [Test]
    public void GetRepositoryPath_ShouldNormalizeUrlVariants()
    {
        var plain = _sut.GetRepositoryPath(Binding("https://github.com/org/repo"));

        _sut.GetRepositoryPath(Binding("https://github.com/org/repo.git")).ShouldBe(plain);
        _sut.GetRepositoryPath(Binding("https://github.com/org/repo/")).ShouldBe(plain);
        _sut.GetRepositoryPath(Binding("HTTPS://GitHub.com/Org/Repo")).ShouldBe(plain);
    }

    [Test]
    public void GetRepositoryPath_ForDifferentBranch_ShouldNotShare()
    {
        _sut.GetRepositoryPath(Binding("https://github.com/org/repo", "main"))
            .ShouldNotBe(_sut.GetRepositoryPath(Binding("https://github.com/org/repo", "dev")));
    }

    [Test]
    public void GetRepositoryPath_ForDifferentCredential_ShouldNotShare()
    {
        _sut.GetRepositoryPath(Binding("https://github.com/org/repo", credential: Guid.NewGuid()))
            .ShouldNotBe(_sut.GetRepositoryPath(Binding("https://github.com/org/repo", credential: Guid.NewGuid())));
        _sut.GetRepositoryPath(Binding("https://github.com/org/repo", credential: Guid.NewGuid()))
            .ShouldNotBe(_sut.GetRepositoryPath(Binding("https://github.com/org/repo")));
    }

    [Test]
    public void GetRepositoryPath_WithBlankBranch_ShouldDefaultToMain()
    {
        _sut.GetRepositoryPath(Binding("https://github.com/org/repo", null))
            .ShouldBe(_sut.GetRepositoryPath(Binding("https://github.com/org/repo", "main")));
    }

    [Test]
    public void GetRepositoryPath_ShouldLiveUnderRepositoriesWithReadableName()
    {
        var path = _sut.GetRepositoryPath(Binding("git@github.com:org/My Repo.git"));

        Path.GetDirectoryName(path).ShouldBe(Path.Combine("/data/git", "repositories"));
        Path.GetFileName(path).ShouldStartWith("my-repo-");
    }

    private static RepositoryBinding Binding(string repository, string? branch = "main", Guid? credential = null, string filePath = "haven.yaml") =>
        new() { Repository = repository, Branch = branch, GitCredentialId = credential, FilePath = filePath };

    [Test]
    public void RepositoryDirectoryExists_ShouldReflectTheClonePath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"haven-path-tests-{Guid.NewGuid():N}");
        var sut = new GitRepositoryPathProvider(root);
        var binding = Binding("https://github.com/org/repo");

        try
        {
            sut.RepositoryDirectoryExists(binding).ShouldBeFalse();

            Directory.CreateDirectory(sut.GetRepositoryPath(binding));

            sut.RepositoryDirectoryExists(Binding("https://github.com/org/repo.git", filePath: "other.yaml")).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}