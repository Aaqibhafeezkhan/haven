using Haven.Domain.Entities;
using Haven.Domain.Enums;
using Haven.Domain.Exceptions;

using Shouldly;

namespace Haven.Domain.Tests.Entities;

[TestFixture]
[Category("Unit")]
public sealed class DeploymentTests
{
    private static Deployment NewDeployment() => Deployment.Create(Guid.NewGuid(), "log.txt");

    [Test]
    public void SetIdentifier_StoresTypeIdentifierAndMessage()
    {
        var deployment = NewDeployment();

        deployment.SetIdentifier(DeploymentIdentifierType.Commit, "abc123", "fix: thing");

        deployment.IdentifierType.ShouldBe(DeploymentIdentifierType.Commit);
        deployment.Identifier.ShouldBe("abc123");
        deployment.CommitMessage.ShouldBe("fix: thing");
    }

    [Test]
    public void SetIdentifier_WithoutMessage_LeavesMessageNull()
    {
        var deployment = NewDeployment();

        deployment.SetIdentifier(DeploymentIdentifierType.ImageDigest, "sha256:abc");

        deployment.CommitMessage.ShouldBeNull();
    }

    [TestCase("")]
    [TestCase("   ")]
    public void SetIdentifier_WithBlankIdentifier_Throws(string identifier)
    {
        Should.Throw<ValidationException>(() =>
            NewDeployment().SetIdentifier(DeploymentIdentifierType.Commit, identifier));
    }

    [Test]
    public void SetIdentifier_AfterFailure_Throws()
    {
        var deployment = NewDeployment();
        deployment.SetIdentifier(DeploymentIdentifierType.Commit, "abc123");
        deployment.Fail();

        Should.Throw<ValidationException>(() =>
            deployment.SetIdentifier(DeploymentIdentifierType.Commit, "def456"));
        deployment.Identifier.ShouldBe("abc123");
    }

    [Test]
    public void Fail_KeepsPreviouslyRecordedIdentifier()
    {
        var deployment = NewDeployment();
        deployment.StartBuilding();
        deployment.SetIdentifier(DeploymentIdentifierType.Commit, "abc123", "msg");

        deployment.Fail();

        deployment.Identifier.ShouldBe("abc123");
        deployment.Status.ShouldBe(DeploymentStatus.Failed);
    }
}