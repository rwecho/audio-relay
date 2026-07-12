namespace AudioRelay.App;

/// <summary>Builds the QR payload (URL + PIN) scanned by the phone client. Pure, testable.</summary>
public static class QrPayload
{
    /// <summary>e.g. http://192.168.1.20:8080?pin=4271</summary>
    public static string Build(string url, string pin) => $"{url}?pin={pin}";

    public static (string url, string pin) Parse(string payload)
    {
        var q = payload.IndexOf('?');
        string url = q >= 0 ? payload[..q] : payload;
        string pin = "";
        if (q >= 0)
        {
            var query = payload[(q + 1)..];
            foreach (var part in query.Split('&'))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0] == "pin")
                    pin = Uri.UnescapeDataString(kv[1]);
            }
        }
        return (url, pin);
    }
}
