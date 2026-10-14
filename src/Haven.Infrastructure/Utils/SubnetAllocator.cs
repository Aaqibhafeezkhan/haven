using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Haven.Infrastructure.Utils;

/// <summary>
/// Picks free /24 subnets out of a dedicated pool (see <c>NetworkOptions.SubnetPool</c>) by real CIDR
/// overlap checks. Docker's own auto-allocated pools (<c>172.17-31.0.0/16</c>, <c>192.168.0.0/16</c>)
/// live elsewhere, so Haven's subnets can never collide with a network Docker hands out on its own.
/// </summary>
public static class SubnetAllocator
{
    public const string DefaultPool = "10.240.0.0/12";
    public const int SubnetPrefixLength = 24;

    public static bool TryParseCidr(string? cidr, out uint network, out int prefix)
    {
        network = 0;
        prefix = 0;

        if (string.IsNullOrWhiteSpace(cidr))
            return false;

        var parts = cidr.Trim().Split('/');
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var address)
            || address.AddressFamily != AddressFamily.InterNetwork
            || !int.TryParse(parts[1], out prefix)
            || prefix is < 0 or > 32)
            return false;

        network = ToUInt(address) & MaskFor(prefix);
        return true;
    }

    public static bool Overlaps(string cidrA, string cidrB)
    {
        if (!TryParseCidr(cidrA, out var netA, out var prefixA)
            || !TryParseCidr(cidrB, out var netB, out var prefixB))
            return false;

        return Overlaps(netA, prefixA, netB, prefixB);
    }

    /// <summary>True when <paramref name="cidr"/> lies entirely inside <paramref name="pool"/>.</summary>
    public static bool IsWithin(string cidr, string pool)
    {
        if (!TryParseCidr(cidr, out var net, out var prefix)
            || !TryParseCidr(pool, out var poolNet, out var poolPrefix))
            return false;

        return prefix >= poolPrefix && (net & MaskFor(poolPrefix)) == poolNet;
    }

    /// <summary>
    /// Returns the first /24 inside <paramref name="pool"/> that overlaps none of <paramref name="taken"/>,
    /// or null when the pool is exhausted. Unparseable entries in <paramref name="taken"/> are ignored.
    /// </summary>
    public static string? FindFreeSubnet(string pool, IEnumerable<string> taken)
    {
        if (!TryParseCidr(pool, out var poolNetwork, out var poolPrefix) || poolPrefix > SubnetPrefixLength)
            throw new ArgumentException($"Subnet pool '{pool}' must be an IPv4 CIDR no smaller than a /{SubnetPrefixLength}.",
                nameof(pool));

        var takenRanges = taken
            .Select(t => TryParseCidr(t, out var n, out var p) ? ((uint Network, int Prefix)?)(n, p) : null)
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();

        var count = 1UL << (SubnetPrefixLength - poolPrefix);
        for (ulong i = 0; i < count; i++)
        {
            var candidate = poolNetwork + (uint)(i << (32 - SubnetPrefixLength));
            if (takenRanges.Any(t => Overlaps(candidate, SubnetPrefixLength, t.Network, t.Prefix)))
                continue;

            return $"{ToAddress(candidate)}/{SubnetPrefixLength}";
        }

        return null;
    }

    /// <summary>First usable host address of a CIDR block, e.g. "10.240.5.0/24" -> "10.240.5.1".</summary>
    public static string GatewayFor(string cidr)
    {
        if (!TryParseCidr(cidr, out var network, out _))
            throw new ArgumentException($"'{cidr}' is not a valid IPv4 CIDR.", nameof(cidr));

        return ToAddress(network + 1).ToString();
    }

    /// <summary>
    /// Subnets of the host's own IPv4 interfaces (LAN, VPN, ...) which Docker cannot see. Best-effort:
    /// any failure just yields fewer entries.
    /// </summary>
    public static IReadOnlyList<string> GetHostSubnets()
    {
        var subnets = new List<string>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || unicast.PrefixLength <= 0)
                        continue;

                    var network = ToUInt(unicast.Address) & MaskFor(unicast.PrefixLength);
                    subnets.Add($"{ToAddress(network)}/{unicast.PrefixLength}");
                }
            }
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException)
        {
            // Best-effort only.
        }

        return subnets;
    }

    private static bool Overlaps(uint netA, int prefixA, uint netB, int prefixB)
    {
        var mask = MaskFor(Math.Min(prefixA, prefixB));
        return (netA & mask) == (netB & mask);
    }

    private static uint MaskFor(int prefix) => prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);

    private static uint ToUInt(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
    }

    private static IPAddress ToAddress(uint value) =>
        new([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);
}
