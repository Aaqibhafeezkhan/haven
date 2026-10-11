using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Features.ApiTokens.Abstractions;
using Haven.Application.Features.ApiTokens.Commands;
using Haven.Domain.Entities;

using NSubstitute;

using Shouldly;

namespace Haven.Application.Tests.Features.ApiTokens;

[Category("Unit")]
public sealed class RevokeApiTokenHandlerTests
{
    private IApiTokenRepository _repository;
    private IApiTokenService _service;
    private ICurrentUserService _currentUser;
    private RevokeApiTokenHandler _sut;
    private Guid _userId;

    [SetUp]
    public void Setup()
    {
        _userId = Guid.NewGuid();
        _repository = Substitute.For<IApiTokenRepository>();
        _service = Substitute.For<IApiTokenService>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _currentUser.UserId.Returns(_userId);
        _sut = new RevokeApiTokenHandler(_repository, _service, _currentUser);
    }

    private ApiToken GivenToken(Guid ownerId, DateTime? revokedAt = null)
    {
        var token = new ApiToken { DisplayName = "ci", OwnerId = ownerId, RevokedAt = revokedAt };
        _repository.GetByIdAsync(token.Id, Arg.Any<CancellationToken>()).Returns(token);
        return token;
    }

    [Test]
    public async Task Handle_ShouldRevoke_WhenTokenBelongsToCurrentUser()
    {
        var token = GivenToken(_userId);

        var result = await _sut.Handle(new RevokeApiTokenCommand { Id = token.Id }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _service.Received(1).RevokeAsync(token.Id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldReturnNotFound_WhenTokenBelongsToAnotherUser()
    {
        var token = GivenToken(Guid.NewGuid());

        var result = await _sut.Handle(new RevokeApiTokenCommand { Id = token.Id }, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe(Error.NotFound.Code);
        await _service.DidNotReceive().RevokeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldReturnNotFound_WhenTokenDoesNotExist()
    {
        var result = await _sut.Handle(new RevokeApiTokenCommand { Id = Guid.NewGuid() }, CancellationToken.None);

        result.Error.Code.ShouldBe(Error.NotFound.Code);
    }

    [Test]
    public async Task Handle_ShouldSucceedWithoutRevokingAgain_WhenAlreadyRevoked()
    {
        var token = GivenToken(_userId, DateTime.UtcNow.AddDays(-1));

        var result = await _sut.Handle(new RevokeApiTokenCommand { Id = token.Id }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _service.DidNotReceive().RevokeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldReturnUnauthorized_WhenNoCurrentUser()
    {
        _currentUser.UserId.Returns((Guid?)null);

        var result = await _sut.Handle(new RevokeApiTokenCommand { Id = Guid.NewGuid() }, CancellationToken.None);

        result.Error.ShouldBe(Error.Unauthorized);
    }
}