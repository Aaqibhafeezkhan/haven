using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Features.Services.Commands.DeleteService;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;

using NSubstitute;

using Shouldly;

namespace Haven.Application.Tests.Features.Services.Commands.DeleteService;

[Category("Unit")]
public sealed class DeleteServiceHandlerTests
{
    private IServiceRepository _repository = null!;
    private IServiceRegistryEntryRepository _registryRepository = null!;
    private DeleteServiceHandler _sut = null!;
    private Service _service = null!;

    [SetUp]
    public void Setup()
    {
        _repository = Substitute.For<IServiceRepository>();
        _registryRepository = Substitute.For<IServiceRegistryEntryRepository>();
        _sut = new DeleteServiceHandler(_repository, _registryRepository);

        _service = Service.Create(Guid.NewGuid(), "svc", ServiceType.DockerImage, ExposureMode.None);
        _repository.GetByIdAsync(_service.Id, Arg.Any<CancellationToken>()).Returns(_service);
    }

    private void GivenEntryInState(ServiceRuntimeState state)
    {
        var entry = ServiceRegistryEntry.Create(_service.Id);
        entry.ApplyRuntimeState(state, DateTime.UtcNow);
        _registryRepository.GetForServiceAsync(_service.Id, Arg.Any<CancellationToken>()).Returns(entry);
    }

    [TestCase(ServiceRuntimeState.Running)]
    [TestCase(ServiceRuntimeState.Restarting)]
    public async Task Handle_RunningOrRestarting_IsRejected(ServiceRuntimeState state)
    {
        GivenEntryInState(state);

        var result = await _sut.Handle(new DeleteServiceCommand { ServiceId = _service.Id }, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        await _repository.DidNotReceive().RemoveAsync(Arg.Any<Service>(), Arg.Any<CancellationToken>());
    }

    [TestCase(ServiceRuntimeState.Stopped)]
    [TestCase(ServiceRuntimeState.Crashed)]
    [TestCase(ServiceRuntimeState.Unknown)]
    public async Task Handle_NotRunning_RemovesServiceAndEntry(ServiceRuntimeState state)
    {
        GivenEntryInState(state);

        var result = await _sut.Handle(new DeleteServiceCommand { ServiceId = _service.Id }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _registryRepository.Received(1).DeleteAsync(Arg.Any<ServiceRegistryEntry>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).RemoveAsync(_service, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_MissingEntry_IsTreatedAsNotDeployedAndRemoved()
    {
        _registryRepository.GetForServiceAsync(_service.Id, Arg.Any<CancellationToken>()).Returns((ServiceRegistryEntry?)null);

        var result = await _sut.Handle(new DeleteServiceCommand { ServiceId = _service.Id }, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).RemoveAsync(_service, Arg.Any<CancellationToken>());
    }
}
