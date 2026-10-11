using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Features.ApiTokens.Abstractions;
using Haven.Application.Features.ApiTokens.Commands;
using Haven.Domain.Entities;

using NSubstitute;

using Shouldly;

namespace Haven.Application.Tests.Features.ApiTokens;

[Category("Unit")]
public sealed class GenerateApiTokenHandlerTests
{
    private IApiTokenService _service;
    private ICurrentUserService _currentUser;
    private GenerateApiTokenHandler _sut;

    [SetUp]
    public void Setup()
    {
        _service = Substitute.For<IApiTokenService>();
        _service.CreateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(),
                Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns((new ApiToken { DisplayName = "ci" }, "hvn_plain"));
        _currentUser = Substitute.For<ICurrentUserService>();
        _currentUser.UserId.Returns(Guid.NewGuid());
        _sut = new GenerateApiTokenHandler(_service, _currentUser);
    }

    private static GenerateApiTokenCommand Command(params string[] scopes)
        => new() { Name = "ci", Scopes = scopes };

    [Test]
    public async Task Handle_ShouldIssueAdminScope_WhenUserIsAdmin()
    {
        _currentUser.IsAdmin.Returns(true);

        var result = await _sut.Handle(Command(Permissions.AdminScope), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PlainText.ShouldBe("hvn_plain");
    }

    [Test]
    public async Task Handle_ShouldReturnForbidden_WhenNonAdminRequestsAdminScope()
    {
        _currentUser.IsAdmin.Returns(false);

        var result = await _sut.Handle(Command(Permissions.AdminScope), CancellationToken.None);

        result.Error.ShouldBe(Error.Forbidden);
        await _service.DidNotReceive().CreateAsync(Arg.Any<Guid>(), Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Validator_ShouldAcceptAdminScope()
    {
        var result = await new GenerateApiTokenValidator().ValidateAsync(Command(Permissions.AdminScope));

        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public async Task Validator_ShouldRejectUnknownScope()
    {
        var result = await new GenerateApiTokenValidator().ValidateAsync(Command("nope"));

        result.IsValid.ShouldBeFalse();
    }
}