using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Abstractions;
using OrderFlow.Infrastructure.BackgroundServices;
using OrderFlow.Infrastructure.Caching;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrderFlowDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("OrderFlowDb")));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OrderFlowDbContext>());

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
            options.InstanceName = configuration["Redis:InstanceName"] ?? "OrderFlow:";
        });

        services.AddScoped<IOrderCache, RedisOrderCache>();

        services.AddHostedService<OrderProcessingWorker>();

        return services;
    }
}
