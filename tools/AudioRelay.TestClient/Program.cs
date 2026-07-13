using Microsoft.Playwright;

// Drives a real headless Chromium against the running server's browser test page.
string url = args.Length > 0 ? args[0] : "http://localhost:8080/?pin=9255";

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });

var page = await browser.NewPageAsync();
page.Console += (_, m) => Console.WriteLine($"[console:{m.Type}] {m.Text}");
page.PageError += (_, e) => Console.WriteLine($"[PAGE-ERROR] {e}");

await page.GotoAsync(url);
await page.ClickAsync("#go");

string conn = "new";
bool gotTrack = false;
for (int i = 0; i < 40; i++) // up to ~20s
{
    await Task.Delay(500);
    conn = await page.EvaluateAsync<string>("window.__ar_conn || 'new'");
    gotTrack = await page.EvaluateAsync<bool>("!!window.__ar_track");
    if (i % 4 == 0 || gotTrack || conn == "connected")
        Console.WriteLine($"  [{i}] conn={conn} gotTrack={gotTrack} status={await page.InnerTextAsync("#status")}");
    if (gotTrack && conn == "connected") break;
}

await page.ScreenshotAsync(new() { Path = "result.png" });
Console.WriteLine($"=== conn={conn} gotTrack={gotTrack} ===");
if (conn == "connected")
    Console.WriteLine(gotTrack
        ? "RESULT: CONNECTED + AUDIO TRACK RECEIVED ✅✅ (full media path works)"
        : "RESULT: CONNECTED ✅ (SDP/ICE/DTLS ok); track not yet signaled");
else
    Console.WriteLine("RESULT: did not connect");
