using Haven.Infrastructure.Utils;

using Shouldly;

namespace Haven.Infrastructure.Tests.Utils;

[Category("Unit")]
public sealed class SubnetAllocatorTests
{
    [TestCase("172.21.5.0/24", "172.21.0.0/16", true)]
    [TestCase("172.21.0.0/16", "172.21.5.0/24", true)]
    [TestCase("10.240.1.0/24", "10.240.2.0/24", false)]
    [TestCase("10.240.1.0/24", "10.240.1.0/24", true)]
    [TestCase("10.0.0.0/8", "10.240.1.0/24", true)]
    public void Overlaps_DetectsContainmentInEitherDirection(string a, string b, bool expected) =>
        SubnetAllocator.Overlaps(a, b).ShouldBe(expected);

    [Test]
    public void FindFreeSubnet_ReturnsFirstSubnetOfPool_WhenNothingTaken() =>
        SubnetAllocator.FindFreeSubnet("10.240.0.0/12", []).ShouldBe("10.240.0.0/24");

    [Test]
    public void FindFreeSubnet_SkipsSubnetsCoveredByWiderNetworks() =>
        SubnetAllocator.FindFreeSubnet("10.240.0.0/12", ["10.240.0.0/24", "10.240.1.0/24", "10.240.2.0/23"])
            .ShouldBe("10.240.4.0/24");

    [Test]
    public void FindFreeSubnet_IgnoresUnparseableEntries() =>
        SubnetAllocator.FindFreeSubnet("10.240.0.0/12", ["garbage", "", "fe80::/64"]).ShouldBe("10.240.0.0/24");

    [Test]
    public void FindFreeSubnet_ReturnsNull_WhenPoolExhausted() =>
        SubnetAllocator.FindFreeSubnet("10.250.0.0/24", ["10.250.0.0/16"]).ShouldBeNull();

    [Test]
    public void FindFreeSubnet_Throws_ForPoolSmallerThanSubnet() =>
        Should.Throw<ArgumentException>(() => SubnetAllocator.FindFreeSubnet("10.250.0.0/25", []));

    [Test]
    public void GatewayFor_ReturnsFirstHost() =>
        SubnetAllocator.GatewayFor("10.240.5.0/24").ShouldBe("10.240.5.1");
}
