using Microsoft.Extensions.Options;

namespace WebHooks.Api.Security;

public static class AdminSecurityExtensions
{
    public static IServiceCollection AddAdminSecurity(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<AdminSecurityOptions>(config.GetSection("AdminSecurity"));
        services.PostConfigure<AdminSecurityOptions>(o =>
        {
            if (string.IsNullOrWhiteSpace(o.AdminKey))
            {
                o.AdminKey = Environment.GetEnvironmentVariable("ADMIN_KEY");
            }
        });

        return services;
    }

    public static IApplicationBuilder UseAdminSecurity(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AdminKeyMiddleware>();
    }
}