using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Text;

public sealed class FakeWebhookReceiver : IAsyncDisposable
{
    private readonly WebApplication _app;

    public string Url { get; }

    public string? LastBody { get; private set; }
    public string? LastSignature { get; private set; }
    public string? LastTimestamp { get; private set; }

    public FakeWebhookReceiver(int port)
    {
        Url = $"http://127.0.0.1:{port}/hook";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(FakeWebhookReceiver).Assembly.FullName,
            EnvironmentName = Environments.Development
        });

        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

        _app = builder.Build();

        _app.MapPost("/hook", async (HttpContext ctx) =>
        {
            LastSignature = ctx.Request.Headers["X-Webhook-Signature"].ToString();
            LastTimestamp = ctx.Request.Headers["X-Webhook-Timestamp"].ToString();

            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            LastBody = await reader.ReadToEndAsync();

            return Results.Ok("OK");
        });
    }

    public Task StartAsync() => _app.StartAsync();

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}