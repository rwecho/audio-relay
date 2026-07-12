using System.Diagnostics.CodeAnalysis;
using AudioRelay.Signaling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace AudioRelay.App;

/// <summary>
/// Hosts the signaling endpoint over Kestrel (no URL ACL needed on Windows). Thin transport:
/// every request is delegated to <see cref="SignalingEndpoint"/>. Excluded from coverage.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class SignalingKestrelHost : IDisposable
{
    private readonly SignalingEndpoint _endpoint;
    private readonly string _url;
    private IHost? _host;

    public SignalingKestrelHost(string url, SignalingEndpoint endpoint)
    {
        _url = url;
        _endpoint = endpoint;
    }

    public void Start()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(web =>
            {
                web.UseUrls(_url);
                web.Configure(app => app.Run(async ctx =>
                {
                    string? body = null;
                    if (ctx.Request.ContentLength is > 0)
                        body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();

                    var (status, response) = _endpoint.HandleRequest(
                        ctx.Request.Method, ctx.Request.Path.Value ?? "/", body);

                    ctx.Response.StatusCode = status;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync(response);
                }));
            })
            .Build();
        _host.Start();
    }

    public async Task StopAsync()
    {
        if (_host is { } h)
            await h.StopAsync(TimeSpan.FromSeconds(2));
    }

    public void Dispose() => _host?.Dispose();
}
