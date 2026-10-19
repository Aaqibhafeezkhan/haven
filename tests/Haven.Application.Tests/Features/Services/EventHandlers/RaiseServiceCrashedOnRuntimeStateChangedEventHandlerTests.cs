using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Features.Services.EventHandlers;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;
using Haven.Domain.Events;

using Mediator;

using NSubstitute;

namespace Haven.Application.Tests.Features.Services.EventHandlers;

[Category("Unit")]
public sealed class RaiseServiceCrashedOnRuntimeStateChangedEventHandlerTests
{
    private IServiceRepository _repository = null!;
    private IMediator _mediator = null!;
    private RaiseServiceCrashedOnRuntimeStateChangedEventHandler _sut = null!;
    private Service _service = null!;

    [SetUp]
    public void Setup()
    {
        _repository = Substitute.For<IServiceRepository>();
        _mediator = Substitute.For<IMediator>();
        _sut = new RaiseServiceCrashedOnRuntimeStateChangedEventHandler(_repository, _mediator);

        _service = Service.Create(Guid.NewGuid(), "svc", ServiceType.DockerImage, ExposureMode.None);
        _repository.GetByIdAsync(_service.Id, Arg.Any<CancellationToken>()).Returns(_service);
    }

    private ServiceRuntimeStateChangedEvent Transition(ServiceRuntimeState next, OwnerKind kind = OwnerKind.Service) =>
        new(Guid.NewGuid(), kind, _service.Id, ServiceRuntimeState.Running, next);

    [Test]
    public async Task Crashed_WhenDesiredRunning_RaisesServiceCrashed()
    {
        _service.SetDesiredState(DesiredState.Running);

        await _sut.Handle(Transition(ServiceRuntimeState.Crashed), CancellationToken.None);

        await _mediator.Received(1).Publish(
            Arg.Is<ServiceCrashedEvent>(e => e.ServiceId == _service.Id), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Crashed_WhenDesiredStopped_StaysQuiet()
    {
        _service.SetDesiredState(DesiredState.Stopped);

        await _sut.Handle(Transition(ServiceRuntimeState.Crashed), CancellationToken.None);

        await _mediator.DidNotReceiveWithAnyArgs().Publish<ServiceCrashedEvent>(default!, default);
    }

    [TestCase(ServiceRuntimeState.Stopped)]
    [TestCase(ServiceRuntimeState.Running)]
    [TestCase(ServiceRuntimeState.Restarting)]
    public async Task OtherTransitions_StayQuiet(ServiceRuntimeState next)
    {
        _service.SetDesiredState(DesiredState.Running);

        await _sut.Handle(Transition(next), CancellationToken.None);

        await _mediator.DidNotReceiveWithAnyArgs().Publish<ServiceCrashedEvent>(default!, default);
    }

    [Test]
    public async Task SidecarCrash_StaysQuiet()
    {
        await _sut.Handle(Transition(ServiceRuntimeState.Crashed, OwnerKind.Sidecar), CancellationToken.None);

        await _mediator.DidNotReceiveWithAnyArgs().Publish<ServiceCrashedEvent>(default!, default);
    }
}