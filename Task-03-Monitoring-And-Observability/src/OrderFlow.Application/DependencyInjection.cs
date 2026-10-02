using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Observability;

namespace OrderFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(TracingBehavior<,>));
        });

        services.AddSingleton<OrderFlowMetrics>();

        return services;
    }
}
