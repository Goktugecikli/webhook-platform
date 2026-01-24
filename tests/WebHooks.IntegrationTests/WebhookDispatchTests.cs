using DotNet.Testcontainers.Builders;
using Testcontainers.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WebHooks.Domain;
using WebHooks.Infrastructre.Persistence;
using WebHooks.Infrastructre.Security;
using System.IO;


public class WebhookDispatchTests : IAsyncLifetime
{
    private PostgreSqlContainer? _pg;

    public async Task InitializeAsync()
    {
        if (OperatingSystem.IsWindows())
        {
            var pipePath = @"\\.\pipe\docker_engine";
            if (!File.Exists(pipePath))
                return;
        }

        _pg = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("webhooks_test")
            .WithUsername("webhooks")
            .WithPassword("webhooks")
            .Build();

        try
        {
            await _pg.StartAsync();
        }
        catch
        {
            await _pg.DisposeAsync();
            _pg = null;
        }
    }



    public async Task DisposeAsync()
    {
        if (_pg is not null)
            await _pg.DisposeAsync();
    }

    [Fact]
    public async Task Dispatch_SendsSignedRequest_AndMarksSucceeded()
    {
        if (_pg is null)
            return;

        await using var receiver = new FakeWebhookReceiver(port: 5127);
        await receiver.StartAsync();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_pg.GetConnectionString()));
        services.AddHttpClient("webhooks");
        services.AddLogging();

        var sp = services.BuildServiceProvider();

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();

            var secret = "test-secret";

            var sub = WebhookSubscription.Create(
                tenantId: "t1",
                provider: "p1",
                eventPrefix: "*",
                targetUrl: receiver.Url,
                secret: secret);

            db.WebhookSubscriptions.Add(sub);

            var delivery = WebhookDelivery.Create(
                tenantId: "t1",
                provider: "p1",
                eventType: "order.created",
                payload: "{\"hello\":\"world\"}",
                idempotencyKey: "k1",
                targetUrl: receiver.Url);

            delivery.MarkPublished();

            db.WebhookDeliveries.Add(delivery);
            await db.SaveChangesAsync();
        }

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

            var dispatcher = new WebhookDispatcher(db, httpFactory, NullLogger<WebhookDispatcher>.Instance);
            var processed = await dispatcher.RunOnce(batchSize: 10, maxRetries: 5, ct: CancellationToken.None);
            Assert.True(processed > 0);
        }

        Assert.False(string.IsNullOrWhiteSpace(receiver.LastBody));
        Assert.False(string.IsNullOrWhiteSpace(receiver.LastTimestamp));
        Assert.False(string.IsNullOrWhiteSpace(receiver.LastSignature));

        var ts = receiver.LastTimestamp!;
        var body = receiver.LastBody!;
        var sig = receiver.LastSignature!;

        Assert.StartsWith("sha256=", sig);
        var sigHex = sig.Substring("sha256=".Length);

        var signedPayload = $"{ts}.{body}";
        var expectedHex = WebhookSignature.ComputeSha256Hex("test-secret", signedPayload);

        Assert.Equal(expectedHex, sigHex);

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.WebhookDeliveries.OrderByDescending(x => x.CreatedAt).FirstAsync();
            Assert.Equal(WebhookDeliveryStatus.Succeeded, saved.Status);
        }
    }
}
