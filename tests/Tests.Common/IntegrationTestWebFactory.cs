using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Tests.Common;

public sealed class IntegrationTestWebFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("hotel_integration_tests")
        .WithUsername("hotel_test")
        .WithPassword("hotel_test_password")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddJsonFile("appsettings.Test.json", optional: false).AddEnvironmentVariables());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveServiceByType(typeof(ApplicationDbContext));
            services.RemoveServiceByType(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveServiceByType(typeof(IDbContextOptionsConfiguration<ApplicationDbContext>));
            services.AddDbContext<ApplicationDbContext>(options => options
                .UseNpgsql(_postgres.GetConnectionString(), postgres =>
                    postgres.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention());
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
