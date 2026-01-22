using Microsoft.Extensions.Options;

namespace WebHooks.Api.Security;

public sealed class AdminKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AdminSecurityOptions _opts;

    public AdminKeyMiddleware(RequestDelegate next, IOptions<AdminSecurityOptions> opts)
    {
        _next = next;
        _opts = opts.Value;
    }

    public async Task Invoke(HttpContext context)
    {
        if (!_opts.Enabled)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith(_opts.AdminPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrWhiteSpace(_opts.AdminKey))
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Admin key is not configured"
            });
            return;
        }

        if (!context.Request.Headers.TryGetValue(_opts.HeaderName, out var provided) ||
            string.IsNullOrWhiteSpace(provided))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Missing admin key"
            });
            return;
        }

        if (!ConstantTimeEquals(provided.ToString(), _opts.AdminKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Invalid admin key"
            });
            return;
        }

        await _next(context);
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;

        var result = 0;
        for (var i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];

        return result == 0;
    }
}
