using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AudioRelay.App;

/// <summary>One pickable network address: adapter name, its IPv4, and whether it has a default gateway.</summary>
public sealed record AdapterCandidate(string Alias, string Ip, bool HasGateway)
{
    public string Display => $"{Alias} — {Ip}";
}

/// <summary>
/// Finds the machine's pickable LAN IPv4 addresses for the adapter dropdown / QR payload,
/// skipping common virtual adapters (Hyper-V / Docker / WSL / loopback). Gateway + private-LAN
/// adapters rank first so the default selection is the real LAN a phone on the same network can
/// reach — but every other real adapter (e.g. Tailscale) stays pickable for power users.
/// </summary>
public static class Network
{
    [ExcludeFromCodeCoverage]
    public static IReadOnlyList<AdapterCandidate> GetCandidateAddresses()
    {
        var list = new List<AdapterCandidate>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (IsVirtualAdapterName(ni.Name)) continue;

            var props = ni.GetIPProperties();
            bool hasGateway = props.GatewayAddresses.Count > 0;
            foreach (var addr in props.UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(addr.Address)) continue;
                list.Add(new AdapterCandidate(ni.Name, addr.Address.ToString(), hasGateway));
            }
        }
        return RankCandidates(list);
    }

    /// <summary>Pure ranking: gateway adapters first, then RFC1918 private-LAN, then alias. Deterministic & testable.</summary>
    public static IReadOnlyList<AdapterCandidate> RankCandidates(IEnumerable<AdapterCandidate> candidates)
        => candidates
            .OrderByDescending(c => c.HasGateway)
            .ThenByDescending(c => IsPrivateLan(c.Ip))
            .ThenBy(c => c.Alias, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Best-effort LAN IP (top-ranked candidate), or loopback if nothing suitable.</summary>
    [ExcludeFromCodeCoverage]
    public static string GetLanIpAddress()
        => GetCandidateAddresses().FirstOrDefault()?.Ip ?? "127.0.0.1";

    /// <summary>True for common virtual adapter names (Hyper-V / Docker / WSL / loopback).</summary>
    public static bool IsVirtualAdapterName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Loopback", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Docker", StringComparison.OrdinalIgnoreCase)
            || name.Contains("WSL", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True for RFC1918 private ranges (10/8, 172.16/12, 192.168/16). Pure, testable.</summary>
    public static bool IsPrivateLan(string ip)
    {
        if (!IPAddress.TryParse(ip, out var addr) || addr.AddressFamily != AddressFamily.InterNetwork)
            return false;
        var b = addr.GetAddressBytes();
        if (b.Length != 4) return false;
        if (b[0] == 10) return true;
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
        if (b[0] == 192 && b[1] == 168) return true;
        return false;
    }
}
