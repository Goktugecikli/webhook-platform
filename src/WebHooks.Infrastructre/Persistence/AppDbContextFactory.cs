using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WebHooks.Infrastructre.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // EF Tools için: appsettings'e bağlı kalmadan sabit cs alıyoruz.
        // İstersen env var ile de aldırırız; şimdilik local için yeterli.
        var cs = "Host=localhost;Port=5432;Database=webhooks_db;Username=webhooks;Password=webhooks";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(cs);

        return new AppDbContext(optionsBuilder.Options);
    }
}
