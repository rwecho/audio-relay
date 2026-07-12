using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AudioRelay.App;

/// <summary>
/// Finds the machine's likely LAN IPv4 address for the QR payload, skipping common virtual
/// adapters (Hyper-V / Docker / WSL). Returns loopback if nothing suitable is found.
/// </summary>
public static class Network
{
    [ExcludeFromCodeCoverage]
    public static string GetLanIpAddress()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (IsVirtualAdapterName(ni.Name)) continue;

            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(addr.Address))
                {
                    return addr.Address.ToString();
                }
            }
        }
        return "127.0.0.1";
    }

    /// <summary>True for common virtual adapter names (Hyper-V / Docker / WSL / loopback).</summary>
    public static bool IsVirtualAdapterName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Loopback", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Docker", StringComparison.OrdinalIgnoreCase)
            || name.Contains("WSL", StringComparison.OrdinalIgnoreCase);
    }
}
