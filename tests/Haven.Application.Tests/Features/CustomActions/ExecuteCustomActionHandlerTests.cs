using Haven.Application.Common;
using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Features.CustomActions.Abstractions;
using Haven.Application.Features.CustomActions.Commands.ExecuteCustomAction;
using Haven.Domain.Entities;
using Haven.Domain.Exceptions;
using Haven.Domain.ValueObjects;

using NSubstitute;

using Shouldly;

namespace Haven.Application.Tests.Features.CustomActions;

[Category("Unit")]
public sealed class ExecuteCustomActionHandlerTests
{
    private const string Shell = "projects.manage_shell";

    private ICustomActionRepository _repository;
    private IActionRunner _runner;
    private ICurrentUserService _currentUser;
    private IPermissionRepository _permissions;
    private ExecuteCustomActionHandler _sut;
    private Guid _userId;
    private Guid _serviceId;

    [SetUp]
    public void Setup()
    {
        _userId = Guid.NewGuid();
        _serviceId = Guid.NewGuid();
        _repository = Substitute.For<ICustomActionRepository>();
        _runner = Substitute.For<IActionRunner>();
        _runner.RunActionAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<CancellationToken>()).Returns(Result.Success());
        _currentUser = Substitute.For<ICurrentUserService>();
        _currentUser.UserId.Returns(_userId);
        _currentUser.Scopes.Returns([]);
        _permissions = Substitute.For<IPermissionRepository>();
        _permissions.UserHasPermissionAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _sut = new ExecuteCustomActionHandler(_repository, _runner, _currentUser, _permissions);
    }

    private CustomAction GivenAction(params string[] requiredPermissions)
    {
        var action = CustomAction.Create(_serviceId, "restart", "restart", "", "icon",
            new ExecActionConfig(["echo"], null, null, null), requiredPermissions, ActionRisk.Safe,
            TimeSpan.FromSeconds(30));
        _repository.GetByIdAsync(action.Id, Arg.Any<CancellationToken>()).Returns(action);
        _repository.GetByTokenAsync(action.Token, Arg.Any<CancellationToken>()).Returns(action);
        return action;
    }

    [Test]
    public async Task Handle_ShouldRun_WhenFoundByTokenWithoutServiceId()
    {
        var action = GivenAction();

        var result = await _sut.Handle(new ExecuteCustomActionCommand { Token = action.Token }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _runner.Received(1).RunActionAsync(action.Id, Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldRun_WhenFoundByIdForMatchingService()
    {
        var action = GivenAction();

        var result = await _sut.Handle(
            new ExecuteCustomActionCommand { ActionId = action.Id, ServiceId = _serviceId }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task Handle_ShouldReturnNotFound_WhenFoundByIdForAnotherService()
    {
        var action = GivenAction();

        var result = await _sut.Handle(
            new ExecuteCustomActionCommand { ActionId = action.Id, ServiceId = Guid.NewGuid() },
            CancellationToken.None);

        result.Error.Code.ShouldBe(Error.NotFound.Code);
        await _runner.DidNotReceiveWithAnyArgs().RunActionAsync(default, default, default);
    }

    [Test]
    public async Task Handle_ShouldReturnNotFound_WhenTokenUnknown()
    {
        var result = await _sut.Handle(new ExecuteCustomActionCommand { Token = "nope" }, CancellationToken.None);

        result.Error.Code.ShouldBe(Error.NotFound.Code);
    }

    [Test]
    public async Task Handle_ShouldThrowForbidden_WhenUserLacksActionRequiredPermission()
    {
        var action = GivenAction(Shell);
        _permissions.UserHasPermissionAsync(_userId, Shell, Arg.Any<CancellationToken>()).Returns(false);

        var act = async () => await _sut.Handle(new ExecuteCustomActionCommand { Token = action.Token },
            CancellationToken.None);

        await act.ShouldThrowAsync<ForbiddenException>();
        await _runner.DidNotReceiveWithAnyArgs().RunActionAsync(default, default, default);
    }

    [Test]
    public async Task Handle_ShouldRun_WhenUserHoldsActionRequiredPermission()
    {
        var action = GivenAction(Shell);

        var result = await _sut.Handle(new ExecuteCustomActionCommand { Token = action.Token }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task Handle_ShouldThrowForbidden_WhenApiTokenLacksActionRequiredScope()
    {
        var action = GivenAction(Shell);
        _currentUser.IsApiToken.Returns(true);
        _currentUser.Scopes.Returns(["projects.manage_deploys"]);

        var act = async () => await _sut.Handle(new ExecuteCustomActionCommand { Token = action.Token },
            CancellationToken.None);

        await act.ShouldThrowAsync<ForbiddenException>();
    }

    [Test]
    public async Task Handle_ShouldRun_WhenApiTokenHasActionRequiredScope()
    {
        var action = GivenAction(Shell);
        _currentUser.IsApiToken.Returns(true);
        _currentUser.Scopes.Returns(["projects.manage_deploys", Shell]);

        var result = await _sut.Handle(new ExecuteCustomActionCommand { Token = action.Token }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task Handle_ShouldSkipActionPermissions_WhenBackgroundContext()
    {
        var action = GivenAction(Shell);
        _currentUser.IsBackgroundContext.Returns(true);
        _permissions.UserHasPermissionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _sut.Handle(new ExecuteCustomActionCommand { Token = action.Token }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }
}
