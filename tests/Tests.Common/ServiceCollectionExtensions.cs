using Microsoft.Extensions.DependencyInjection;

namespace Tests.Common;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RemoveServiceByType(this IServiceCollection services, Type serviceType)
    {
        foreach (var descriptor in services.Where(x => x.ServiceType == serviceType).ToArray())
            services.Remove(descriptor);
        return services;
    }
}
