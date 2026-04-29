using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CommunicationService.Infrastructure.Data;

public sealed class CommunicationDbContextFactory : IDesignTimeDbContextFactory<CommunicationDbContext>
{
    public CommunicationDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var apiPath = Path.Combine(basePath, "src", "CommunicationService.Api");
        var settingsBasePath = File.Exists(Path.Combine(basePath, "appsettings.json"))
            ? basePath
            : apiPath;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(settingsBasePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("CommunicationDb");
        var optionsBuilder = new DbContextOptionsBuilder<CommunicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new CommunicationDbContext(optionsBuilder.Options);
    }
}
