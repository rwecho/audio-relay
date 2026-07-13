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
}
