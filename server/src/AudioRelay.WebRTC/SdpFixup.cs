using System.Text.RegularExpressions;

namespace AudioRelay.WebRTC;

/// <summary>
/// Normalizes libdatachannel SDP for browser compatibility. libdatachannel emits bare
/// "a=ssrc:NNN" lines without a cname attribute, which browsers reject
/// ("Failed to parse SessionDescription: a=ssrc:NNN Expects 2 fields"). This appends the
/// cname so Chromium/Firefox/Safari accept the SDP. Pure, fully unit-testable.
/// </summary>
public static class SdpFixup
{
    private static readonly Regex BareSsrc = new(@"^a=ssrc:(\d+)\s*$", RegexOptions.Compiled);

    public static string EnsureSsrcCname(string sdp, string cname)
    {
        if (string.IsNullOrEmpty(sdp)) return sdp;

        var lines = sdp.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.TrimEnd('\r');
            var match = BareSsrc.Match(trimmed);
            if (match.Success)
                lines[i] = $"a=ssrc:{match.Groups[1].Value} cname:{cname}\r";
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Strips IPv6 ICE candidate lines, keeping only IPv4. Broken IPv6 candidate pairs cause
    /// libdatachannel/libjuice "Consent expired for candidate pair" → media stalls + the
    /// connection drops after ~30-60s (paullouisageneau/libdatachannel#1006, #1229). The phone
    /// is on the LAN IPv4 subnet, so advertising only IPv4 forces the reliable path. Pure.
    /// </summary>
    public static string EnsureIpv4Only(string sdp)
    {
        if (string.IsNullOrEmpty(sdp)) return sdp;

        var lines = sdp.Split('\n');
        var kept = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            string trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("a=candidate:", StringComparison.Ordinal))
            {
                // a=candidate:foundation component proto priority <addr> port typ ...
                var fields = trimmed.Substring("a=candidate:".Length).Split(' ');
                if (fields.Length > 4 && fields[4].Contains(':'))
                    continue; // IPv6 connection address — drop
            }
            kept.Add(line);
        }
        return string.Join("\n", kept);
    }
}
