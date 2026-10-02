using System.Diagnostics;
using MediatR;
using OrderFlow.Application.Features.Dashboard.GetDashboardOrders;
using OrderFlow.Application.Features.Orders.CreateOrder;
using OrderFlow.Application.Features.Orders.GetOrderById;
using OrderFlow.Application.Features.Orders.ListOrders;

namespace OrderFlow.Application.Observability;

public sealed class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        using var activity = OrderFlowDiagnostics.ActivitySource.StartActivity(ActivityName());

        activity?.SetTag("mediatr.request.type", typeof(TRequest).FullName);

        if (request is GetOrderByIdQuery query)
            activity?.SetTag("order.id", query.Id);

        try
        {
            return await next();
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            throw;
        }
    }

    private static string ActivityName() => typeof(TRequest) switch
    {
        var type when type == typeof(CreateOrderCommand) => "Create Order",
        var type when type == typeof(GetOrderByIdQuery) => "Get Order By ID",
        var type when type == typeof(ListOrdersQuery) => "Get Orders",
        var type when type == typeof(GetDashboardOrdersQuery) => "Get Dashboard Orders",
        _ => typeof(TRequest).Name
    };
}
