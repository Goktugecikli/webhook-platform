namespace WebHooks.Api.Security;

public sealed class AdminSecurityOptions
{
    public string HeaderName { get; set; } = "X-Admin-Key";
    public string? AdminKey { get; set; }
    public string AdminPathPrefix { get; set; } = "/admin";
    public bool Enabled { get; set; } = true;
}