using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Common.Interfaces.Services;
using Haven.Application.Features.ServiceRegistry.Commands.AttachDomainCertificate;
using Haven.Domain.Aggregates;
using Haven.Domain.Entities;
using Haven.Domain.Enums;

using NSubstitute;

using Shouldly;

namespace Haven.Application.Tests.Features.ServiceRegistry.Commands.AttachDomainCertificate;

[Category("Unit")]
public sealed class AttachDomainCertificateHandlerTests
{
    private const string ValidCertPem = """
                                         -----BEGIN CERTIFICATE-----
                                         MIIDODCCAiCgAwIBAgIUUEv0Z4KmwfeTFKar9xYbE6RO91IwDQYJKoZIhvcNAQEL
                                         BQAwFjEUMBIGA1UEAwwLZXhhbXBsZS5jb20wIBcNMjYxMDI4MTMzMTUyWhgPMjEy
                                         NjEwMDQxMzMxNTJaMBYxFDASBgNVBAMMC2V4YW1wbGUuY29tMIIBIjANBgkqhkiG
                                         9w0BAQEFAAOCAQ8AMIIBCgKCAQEAqy7pqIoydexXqJSYsTuIPqJUFXxlg1fgyhZ5
                                         hG7kuio1m7bvQugWbxzOkouuRvrPrpIVmpu/TrbWo+0wfCAqxqgdIzRk2xj1aRDF
                                         ohYz6GO/eVCA94G/lSwhu3rliqTGuXUnLyIprmEfvbpKx8y1vWY7ovHyOFtJ/gAr
                                         syGejD1Hx8Tb/Ov9c5xEAb5jPnvEolATkcz3Dg51HqetkhZ2SCcOoltT/rCkbMRC
                                         5Catk9zNad0g/NFq86ogf8zPlc2Ae6E4gEmHK6sLttiJv2cBlHXCQA4/9qixxTXD
                                         Otcaj9fAQi3xmE3qDfc6FKnJ41a+0orTJyaVG+5KyZMrpRHxtQIDAQABo3wwejAd
                                         BgNVHQ4EFgQUz0f0YfSdP/w2RLDerzBt6MXxmqYwHwYDVR0jBBgwFoAUz0f0YfSd
                                         P/w2RLDerzBt6MXxmqYwDwYDVR0TAQH/BAUwAwEB/zAnBgNVHREEIDAeggtleGFt
                                         cGxlLmNvbYIPd3d3LmV4YW1wbGUuY29tMA0GCSqGSIb3DQEBCwUAA4IBAQAhpA1E
                                         jLtHeR/Rin2A2Xz162JALRcl7Ua8VdPCxQKyv5kUn3BZafYqacDBHNjL7PI7c97Y
                                         d7KD4SV4SJxtVdzUptiQLW/FsG8PEfQQQfThRNWs8sIqLdSGuIy/NjIEUbOgoLhh
                                         Ms+Ic5RtxLY90E6rZc9HQ1mYYuXpxiVmZAxhZ/S83wiuAv4eL1MhN1W3o5LgRV0W
                                         ZCTdshljf8ioaQEq54KKf4v8urL9Mqz45paFU+YKK3UK/+Z9aNopHWIqGgPmOQgP
                                         Q/TibN265F3e2OjEWykEqHZ+RoF66jbVo2PTvnIVP+vgJohKQvketPl6hVC14RRy
                                         lxFIDfQQ5S0bRXe1
                                         -----END CERTIFICATE-----
                                         """;

    private const string ValidKeyPem = """
                                        -----BEGIN PRIVATE KEY-----
                                        MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQCrLumoijJ17Feo
                                        lJixO4g+olQVfGWDV+DKFnmEbuS6KjWbtu9C6BZvHM6Si65G+s+ukhWam79Ottaj
                                        7TB8ICrGqB0jNGTbGPVpEMWiFjPoY795UID3gb+VLCG7euWKpMa5dScvIimuYR+9
                                        ukrHzLW9Zjui8fI4W0n+ACuzIZ6MPUfHxNv86/1znEQBvmM+e8SiUBORzPcODnUe
                                        p62SFnZIJw6iW1P+sKRsxELkJq2T3M1p3SD80WrzqiB/zM+VzYB7oTiASYcrqwu2
                                        2Im/ZwGUdcJADj/2qLHFNcM61xqP18BCLfGYTeoN9zoUqcnjVr7SitMnJpUb7krJ
                                        kyulEfG1AgMBAAECggEAFyLzUkQBb3ZG38Jbw+KVts92WWQTjVXAjFfOy0M4t1+y
                                        mK34Nx0KwTBNzDlEUBQgo0nb0SZ5TCCCsKvDeJdZaLm6nHI9qm48Zxus9owVLnTi
                                        4XjrwWggNn99PKtu4FcGg/TtW528r/SxF1HZPxlwO93YNB0K3aqa/NFVlrO1y+LY
                                        3MCZsuGyc+SnCILOc0q3tKo572dI50K23q6ezXXYcx0tzSxuiXCGFmo1bLxAiHSg
                                        /hpq1VI70khGmO/GvVN4+wUIQtwQ2ZLyOQ73lv0ujeSrV+i9RKzYa4ezNOeGG/ss
                                        jzdSObf8KX08xZR9T0bzg6ebw2+LCoxq6O31Dw4q0QKBgQDSoKitJDXi4M/aLIW4
                                        6aSuxQmgkVCYNvCh08ja/B1flNr8ooBmUZzjLrDbugYvmByjAX1lmOpC8KueFTm+
                                        EC42ZKmfluSZlThv39GfjO2wxkjtmlbwozQjdpEtJVqeMIbGFX/BYMOGzo+jc8tF
                                        X/mNfkOWfXRcM4keD7odY8Y1sQKBgQDQDwstD/k5eTXrZlFExt18ssj8bG7Okjob
                                        0riSfre5UHy/6rxwUhZKR43XLEZjasqxxkFJT2VFHWKrkMdCTebXDGzR4usLpBSd
                                        AqQGYmYUo1ZGgjzFS87BAOqxn7FNbekVVJbaLxwanp2Pp23xeGj/ZjU73eeW0AD1
                                        +hs9aDZJRQKBgBXGJAnaH3P316TvS1elV6/1kJw3yYQi0dd45F+3BnZWFhld1zY8
                                        o6Jctk3Qukj7SaKUSdlR6I3JAYTTHugGFjDrURm9aJ5z7aSrF6mN0RigNvPRK8Gu
                                        rXopDLMFFIwb6iLaIB3mme/6tKCqHM5sMIkvD1+kUn4hmcAw0gWtxdTRAoGBAJSn
                                        evu6Yv5OfVjwpPY4BD45W2MHcnBDiCMi/t8kjM5qFIfnBrZV4Dk4QL7RkoUr4yD7
                                        nt/rS1cDdOFSMxxLnrhd105rDDcoB9Za8ajiGz95qIX+Pb9Vt6asqgM5Lyw8cIHe
                                        C15etHzMgZUHMoWJ27NHapeYqARYH6egCGBqmgblAoGAHvfk8GNwF1OZnUOtIx4t
                                        BysRSpJquBP1Hpaxwwcq2RqsAelYPXSKUTtIApbCw+A8thu2ovI215ifGlyt2xuL
                                        SHQ7wkZi5PEy2niFtWljUp+5J9BvOALCBbxpiFPX+8+gZlEhMAOjJzh9dJbTWe9f
                                        MGfq0mxAvvk7QJQMaygQEyU=
                                        -----END PRIVATE KEY-----
                                        """;

    private IServiceRegistryEntryRepository _serviceRegistryEntryRepository;
    private ISslCertificateRepository _sslCertificateRepository;
    private ITraefikDynamicConfigWriter _traefikDynamicConfigWriter;
    private AttachDomainCertificateHandler _sut;

    [SetUp]
    public void Setup()
    {
        _serviceRegistryEntryRepository = Substitute.For<IServiceRegistryEntryRepository>();
        _sslCertificateRepository = Substitute.For<ISslCertificateRepository>();
        _traefikDynamicConfigWriter = Substitute.For<ITraefikDynamicConfigWriter>();
        _traefikDynamicConfigWriter.WriteDomainCertificateAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        _sut = new AttachDomainCertificateHandler(
            _serviceRegistryEntryRepository, _sslCertificateRepository, _traefikDynamicConfigWriter);
    }

    [Test]
    public async Task Handle_EntryNotFound_ReturnsFailure()
    {
        var command = new AttachDomainCertificateCommand
        {
            DomainId = Guid.NewGuid(),
            CertificateId = Guid.NewGuid()
        };
        _serviceRegistryEntryRepository.GetByDomainIdAsync(command.DomainId, Arg.Any<CancellationToken>())
            .Returns((ServiceRegistryEntry?)null);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
    }

    [Test]
    public async Task Handle_DomainNotCustomMode_ReturnsFailure()
    {
        var entry = ServiceRegistryEntry.Create(Guid.NewGuid());
        var domain = entry.AddDomain("example.com", 80, TlsMode.Acme);
        var command = new AttachDomainCertificateCommand
        {
            DomainId = domain.Id,
            CertificateId = Guid.NewGuid()
        };
        _serviceRegistryEntryRepository.GetByDomainIdAsync(domain.Id, Arg.Any<CancellationToken>())
            .Returns(entry);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("INVALID_OPERATION");
    }

    [Test]
    public async Task Handle_CertificateNotFound_ReturnsFailure()
    {
        var entry = ServiceRegistryEntry.Create(Guid.NewGuid());
        var domain = entry.AddDomain("example.com", 80, TlsMode.Custom);
        var command = new AttachDomainCertificateCommand
        {
            DomainId = domain.Id,
            CertificateId = Guid.NewGuid()
        };
        _serviceRegistryEntryRepository.GetByDomainIdAsync(domain.Id, Arg.Any<CancellationToken>())
            .Returns(entry);
        _sslCertificateRepository.GetByIdAsync(command.CertificateId, Arg.Any<CancellationToken>())
            .Returns((SslCertificate?)null);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
    }

    [Test]
    public async Task Handle_ValidAttach_SetsCertificateAndWritesDynamicConfig()
    {
        var entry = ServiceRegistryEntry.Create(Guid.NewGuid());
        var domain = entry.AddDomain("other.example.com", 80, TlsMode.Custom);
        var certificate = SslCertificate.Create("wildcard", ValidCertPem, ValidKeyPem);
        var command = new AttachDomainCertificateCommand
        {
            DomainId = domain.Id,
            CertificateId = certificate.Id
        };
        _serviceRegistryEntryRepository.GetByDomainIdAsync(domain.Id, Arg.Any<CancellationToken>())
            .Returns(entry);
        _sslCertificateRepository.GetByIdAsync(certificate.Id, Arg.Any<CancellationToken>())
            .Returns(certificate);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Warnings.ShouldContain(w => w.Contains("do not include"));
        domain.SslCertificateId.ShouldBe(certificate.Id);
        await _traefikDynamicConfigWriter.Received(1).WriteDomainCertificateAsync(
            domain.Id, ValidCertPem, ValidKeyPem, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_SidecarOwnedDomain_SetsCertificateAndWritesDynamicConfig()
    {
        var entry = ServiceRegistryEntry.CreateForSidecar(Guid.NewGuid());
        var domain = entry.AddDomain("dashboard.example.com", 80, TlsMode.Custom);
        var certificate = SslCertificate.Create("wildcard", ValidCertPem, ValidKeyPem);
        var command = new AttachDomainCertificateCommand
        {
            DomainId = domain.Id,
            CertificateId = certificate.Id
        };
        _serviceRegistryEntryRepository.GetByDomainIdAsync(domain.Id, Arg.Any<CancellationToken>())
            .Returns(entry);
        _sslCertificateRepository.GetByIdAsync(certificate.Id, Arg.Any<CancellationToken>())
            .Returns(certificate);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        domain.SslCertificateId.ShouldBe(certificate.Id);
    }
}