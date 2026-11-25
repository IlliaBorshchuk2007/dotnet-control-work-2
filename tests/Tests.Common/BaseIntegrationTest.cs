using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Tests.Common;

public abstract class BaseIntegrationTest : IClassFixture<IntegrationTestWebFactory>, IDisposable
{
    private readonly WebApplicationFactory<Program> _authenticatedFactory;
    private readonly IServiceScope _scope;
    protected ApplicationDbContext Context { get; }
    protected HttpClient Client { get; }
    protected static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    protected BaseIntegrationTest(IntegrationTestWebFactory factory)
    {
        _authenticatedFactory = factory.WithWebHostBuilderMock();
        Client = _authenticatedFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName);
        _scope = _authenticatedFactory.Services.CreateScope();
        Context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    public void Dispose()
    {
        Client.Dispose();
        _scope.Dispose();
        _authenticatedFactory.Dispose();
        GC.SuppressFinalize(this);
    }
}
