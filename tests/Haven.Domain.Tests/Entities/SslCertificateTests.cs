using Haven.Domain.Entities;
using Haven.Domain.Exceptions;

using Shouldly;

namespace Haven.Domain.Tests.Entities;

[TestFixture]
[Category("Unit")]
public sealed class SslCertificateTests
{
    // Self-signed test certificate for CN=example.com, SAN=example.com,www.example.com, valid
    // 2026-10-28 through 2126-10-04 (100 years) so the tests don't expire.
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

    // Self-signed wildcard test certificate for CN=*.example.org, SAN=*.example.org.
    private const string WildcardCertPem = """
                                            -----BEGIN CERTIFICATE-----
                                            MIIDKzCCAhOgAwIBAgIUKpudQksbN5NyEMf6f4QWIMWKkMswDQYJKoZIhvcNAQEL
                                            BQAwGDEWMBQGA1UEAwwNKi5leGFtcGxlLm9yZzAeFw0yNjA4MjQxMjAxMjdaFw0z
                                            NjA4MjExMjAxMjdaMBgxFjAUBgNVBAMMDSouZXhhbXBsZS5vcmcwggEiMA0GCSqG
                                            SIb3DQEBAQUAA4IBDwAwggEKAoIBAQDcJ7vpCnt5ZmdWqWwnDaPGKh8m/O1zIaIu
                                            +O4DTi4n9LmbbjWVDZHatIwLAfpAGVRfJ/gPNn0H3sASmaY8ZTW26Zw8BeOsu+8R
                                            CoYQVZDErHzgjnwjMQgSZFGsnKQe2m716OWuaUSMsr86Y0jJBr388rZyz81qnYzM
                                            Yuj07OSV4D/8P7fSrnw+rKTnFD5ewnAxKy+qSEHTl0+le4VpT19Km9Eiu2SVjhrE
                                            6x3ZQx1+JDel08o1+YFJdHkrdAQL7fG9HkjJiq6FqvH9bzRhb/ZNL1tzPCugiJfR
                                            FH7aL1aDFVBHon3TrsubuQuJA4HUPdioPZADtDUGOA34elQZHiD9AgMBAAGjbTBr
                                            MB0GA1UdDgQWBBQxxPl5By8Ek0YEpOQfC/LfkLs4LzAfBgNVHSMEGDAWgBQxxPl5
                                            By8Ek0YEpOQfC/LfkLs4LzAPBgNVHRMBAf8EBTADAQH/MBgGA1UdEQQRMA+CDSou
                                            ZXhhbXBsZS5vcmcwDQYJKoZIhvcNAQELBQADggEBAB5hJCEAEtJ9bkW85I9vpIKm
                                            3nbD+USbKreffAYN2dAn24hvIOKG2fl9lsaShi+dVF6SoO7KiY0WMAXnptxOYjOl
                                            akM8FgOJ9w6RXSMxZUlSxaI8qlPlQ4cvOl+mEvgKQwsCXoJ4q5BLjEsTo0mOagPs
                                            v5ZteBl9/TujwcAAY+wRcKaEs/dPU5v/CBncCa62xOn4XgVPE9oRpbeP5apU3OYN
                                            jpgooJpSPO9QARQ+QAZcoma67m9krwLJdOLBa0M2Xwz4iSYvC5YzkZY8sTrELDld
                                            VOEgjVuGr+3f1io1PaLMQJR8GOv5+H6IKfZEwVGwGwpOqpswAL1xy7Qa4jlaZsA=
                                            -----END CERTIFICATE-----
                                            """;

    private const string WildcardKeyPem = """
                                           -----BEGIN PRIVATE KEY-----
                                           MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDcJ7vpCnt5ZmdW
                                           qWwnDaPGKh8m/O1zIaIu+O4DTi4n9LmbbjWVDZHatIwLAfpAGVRfJ/gPNn0H3sAS
                                           maY8ZTW26Zw8BeOsu+8RCoYQVZDErHzgjnwjMQgSZFGsnKQe2m716OWuaUSMsr86
                                           Y0jJBr388rZyz81qnYzMYuj07OSV4D/8P7fSrnw+rKTnFD5ewnAxKy+qSEHTl0+l
                                           e4VpT19Km9Eiu2SVjhrE6x3ZQx1+JDel08o1+YFJdHkrdAQL7fG9HkjJiq6FqvH9
                                           bzRhb/ZNL1tzPCugiJfRFH7aL1aDFVBHon3TrsubuQuJA4HUPdioPZADtDUGOA34
                                           elQZHiD9AgMBAAECggEAC+Rb5rucTgNi0iApOYEnsgDc+WzAq88q36mD0SsO3GAr
                                           CtMSHbjOV+IYTXuVcmhxMbRVlobINrMI14hesypOvjsSEij0R9nypjjrYyRSw1ju
                                           OZgLaxWPMiUinKBtJMXXqBIPnr33crfbaLKWatYh0EZ7u1RvwWenbx6UUa9JLVzx
                                           GEbTr9CHk+ydUsWvvlqGDhAPMING0PCEBh0nHZ0mWf0NOnIAEH+htVteeIvzt8Nc
                                           cF6pWYBiFJh/Zeos1dLa7UsT4cSDO/Ur3Ra2exNTWrUfeR6bk7BE+O340Y/LDNhH
                                           FVDU45SbzuE7Jj2xu5DMtKEKB+fDjBjL10pKgEJtaQKBgQDzBKJTVYAgp0AY+h3V
                                           nkVn7xjK3VgdOBtUpVspjzIoWCHYv63Acw09g6EXRcKgeRTWHlTrJ5P/KWjXU/wq
                                           GfuDkLIVcOHYglCk3j75zAL9vNGxMfoZAM/ViyRk096JVCX7asnN5FWpu1nGwe7z
                                           Fh/mCOubx7t7D0xdHuW1XNeH+QKBgQDn6nDtfCWiMSnFKjTSVyq28i+dYCMVPjFD
                                           k7U4VQ3R9KvEcOvSP13qUI9erfyY7gKVsZNSDISAUyqQtBIkCBXrM25KgqCzUPWR
                                           WZngJTgmcG20sus7kyTgMnP/bCgg4jWbxcQhCRME7WRYvptP1xflWIAV1BUQrg6O
                                           mL2q71XKJQKBgQCbzqLMTwsg5FpiKSorpZfWNSNuHU+7HBfZw1KZaKe92hOJRgt+
                                           UcVxZQ1JQH6yKC9FwJitU+i9Na10MPKBg7sP9RtYR9Fk4NgXfC5gNX7Nc9v1gZdZ
                                           pH2b6ePhiT0qSvs3IJZWHUkW03mRxxEOZWb6M0nrzLjVA0/wfDjGeMnu8QKBgBa5
                                           vHMpFS79jlBJwH9UF1VyCgRr5UQxofYzRTDN9Nq8FRDc1970YqmRV1s5xWTe/dXZ
                                           XsxNebZxb9xaKOTq/ercUVRv1Ht91XJ2y0NRolzx624nkjF2S8jEaOWAnbYLNKGd
                                           EYkDMJ/s+0ZO9z0toKPStkptS9skkzyZ7wwPA+MZAoGAfqQCizTMYhgZWEEvBXbo
                                           RtJLZ068gus1a9uFGXjbBrH1sPRawfXE2iO73O7UYjILKK8KR7H4A3XgC81nhHl9
                                           d4jNwLCrczSKX9fUML8XSkrASKsdosTq1LDjRhFsF/gmsDUQIsna68H5PfIyYJfG
                                           G+MS3nJR09/s0I2SIJCvxJ8=
                                           -----END PRIVATE KEY-----
                                           """;

    [Test]
    public void Create_ValidPair_ParsesMetadata()
    {
        var certificate = SslCertificate.Create("test", ValidCertPem, ValidKeyPem);

        certificate.SubjectCommonName.ShouldBe("example.com");
        certificate.NotBefore.ShouldBeLessThan(DateTimeOffset.UtcNow);
        certificate.NotAfter.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(300));
        certificate.Fingerprint.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public void Create_ValidPair_DoesNotThrowEvenThoughNotYetExpired()
    {
        Should.NotThrow(() => SslCertificate.Create("test", ValidCertPem, ValidKeyPem));
    }

    [Test]
    public void Create_EmptyName_Throws()
    {
        Should.Throw<ValidationException>(() => SslCertificate.Create("", ValidCertPem, ValidKeyPem));
    }

    [Test]
    public void Create_EmptyCertificatePem_Throws()
    {
        Should.Throw<ValidationException>(() => SslCertificate.Create("test", "", ValidKeyPem));
    }

    [Test]
    public void Create_EmptyPrivateKeyPem_Throws()
    {
        Should.Throw<ValidationException>(() => SslCertificate.Create("test", ValidCertPem, ""));
    }

    [Test]
    public void Create_UnparseablePem_Throws()
    {
        Should.Throw<ValidationException>(() =>
            SslCertificate.Create("test", "not a certificate", "not a key"));
    }

    [Test]
    public void Create_MismatchedKey_Throws()
    {
        const string otherKeyPem = """
                                    -----BEGIN PRIVATE KEY-----
                                    MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC7VJTUt9Us8cKj
                                    MzEfYyjiWA4R4/M2bS1GB4t7NXp98C3SC6dVMvDuictGeurT8jNbvJZHtCSuYEvu
                                    NMoSfm76oqFvAp8Gy0iz5sxjZmSnXyCdPEovGhLa0VzMaQ8s+CLOyS56YyCFGeJZ
                                    qgtzJ6GR3eqoYSW9b9UMvkBpZODSctWSNGj3P7jRFDO5VoTwCQAWbFnOjDfH5Ulg
                                    p2PKSQnSJP3AJLQNFNe7br1XbrhV//eO+t51mIpGSDCUv3E0DDFcWDTH9cXDTTlR
                                    ZVEiR2BwpZOOkE/Z0/BVnhZYL71oZV34bKfWjQIt6V/isSMahdsAASACp4ZTGtwi
                                    VuNd9tybAgMBAAECggEBAKTmjaS6tkK8BlPXClTQ2vpz/N6uxDeS35mXpqasqskV
                                    laAidgg/sWqpjXDbXr93otIMLlWsM+X0CqMDgSXKejLS2jx4GDjI1ZTXg++0AMJ8
                                    sJ74pWzVDOfmCEQ/7wXs3+cbnXhKriO8Z036q92Qc1+N87SI38nkGa0ABH9CN83H
                                    mQqt4fB7UdHzuIRe/me2PGhIq5ZBzj6h3BpoPGzEP+x3l9YmK8t/1cN0pqI+dQwY
                                    dgfGjackLu/2qRnninJ8gJKYDlJz9SIrf7BAf7B3Yhd8n5rgB8k+ZlBWNr71+f2r
                                    dUnGqxMY4iw7T2gGkAgIB1sMPzYo3JU1IK3PpLwYcuECgYEA8dQXaGRmtsgqLpV6
                                    E/tZRz1EDYW0aScilRAJIkG3MLKfhL3jhTLR7RXVJ4KAf2MvBORAxRJhOCqz2LTk
                                    l7KZKPXNAVDdWQeM70DLKdEEV3ZLGGjRTmqp7dxaZJqTa8gAYT8UBpqCS4orJ3d/
                                    ff8Btdk+kt4CGdfMBcqm0X4ATdECgYEAxevGWvVJqOoc+8LNwzWfMxdfr/8Iq5Ie
                                    ANsAeNKuNyJnYzoBGmqAWCPKuAt6MjLmfEXhP1sHXQnZE4LWfXwDNhZPZi7dXvV9
                                    HgS5xQAHt3ekasqmvz7oGaLU2WEuZDl15QeCiKGpUx5oPqCS9xQEfgGH2rmVzoGz
                                    Uo+9DUzOfUcCgYEAjq4jUUieRUuz4pWH4NBFsK+SjB2NRfhOhTaLM0AeqmSPPKgi
                                    xzR6HVYA0aY9AqPZeydtSYh/rNVeQR6yhKPnRElB5x7azQOoiI+wKfIRXWANz19N
                                    4o9KpiKcpqZ0LlOVwUuv/38xk9CedC9uCP4a5D69B0iWzGvhK8xdrM8XxvECgYAe
                                    +iqrbA9UT9DjHFDaP+jn+SF63pxgz53pAA0HXn/mnDBmpv2CClJ8FooZLuIQAo5Q
                                    aeafhKvduPRAKZ8IVwoRA4KDdInBWzCr6C1fBcNZ8yTXHNMKfz8H+m3AtNfyzTv1
                                    lWQ1nzyeaLDQ+PMDpqRIT1z0FhwZ2XwqA6bQeJgUqQKBgQDsMd3TIB6DK5jTVDZP
                                    /Ivg6uEEyq5ZOK7c5o7lYy9v/Aqk3/gcJ0FQeYs05L2FLE1qgh6XKgcwbYtI6vzl
                                    IiBH7Q26H6dGItAKcNGPWiWGVYK6MkQXwANI7Isq83NoyowaG8+MVw2K/rlF+FQq
                                    U8OY2AaeE+jjLKtdrKUxLLTwpQ==
                                    -----END PRIVATE KEY-----
                                    """;

        Should.Throw<ValidationException>(() =>
            SslCertificate.Create("test", ValidCertPem, otherKeyPem));
    }

    [Test]
    public void MatchesHostname_HostnameInSan_ReturnsTrue()
    {
        var certificate = SslCertificate.Create("test", ValidCertPem, ValidKeyPem);

        certificate.MatchesHostname("www.example.com").ShouldBeTrue();
    }

    [Test]
    public void MatchesHostname_HostnameNotCovered_ReturnsFalse()
    {
        var certificate = SslCertificate.Create("test", ValidCertPem, ValidKeyPem);

        certificate.MatchesHostname("other.example.com").ShouldBeFalse();
    }

    [Test]
    public void MatchesHostname_WildcardCoversSingleLevelSubdomain_ReturnsTrue()
    {
        var certificate = SslCertificate.Create("test", WildcardCertPem, WildcardKeyPem);

        certificate.MatchesHostname("app.example.org").ShouldBeTrue();
    }

    [Test]
    public void MatchesHostname_WildcardDoesNotCoverBareApex_ReturnsFalse()
    {
        var certificate = SslCertificate.Create("test", WildcardCertPem, WildcardKeyPem);

        certificate.MatchesHostname("example.org").ShouldBeFalse();
    }

    [Test]
    public void MatchesHostname_WildcardDoesNotCoverNestedSubdomain_ReturnsFalse()
    {
        var certificate = SslCertificate.Create("test", WildcardCertPem, WildcardKeyPem);

        certificate.MatchesHostname("a.b.example.org").ShouldBeFalse();
    }

    [Test]
    public void Rotate_ReplacesFieldsAndBumpsUpdatedAt()
    {
        var certificate = SslCertificate.Create("test", ValidCertPem, ValidKeyPem);
        var originalUpdatedAt = certificate.UpdatedAt;

        certificate.Rotate(ValidCertPem, ValidKeyPem);

        certificate.UpdatedAt.ShouldBeGreaterThanOrEqualTo(originalUpdatedAt);
        certificate.CertificatePem.ShouldBe(ValidCertPem);
    }

    [Test]
    public void IsExpired_FutureExpiry_ReturnsFalse()
    {
        var certificate = SslCertificate.Create("test", ValidCertPem, ValidKeyPem);

        certificate.IsExpired.ShouldBeFalse();
    }
}