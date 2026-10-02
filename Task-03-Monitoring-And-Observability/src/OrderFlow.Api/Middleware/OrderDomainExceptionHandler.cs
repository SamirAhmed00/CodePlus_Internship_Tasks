using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Api.Middleware;

public class OrderDomainExceptionHandler(ILogger<OrderDomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not OrderDomainException domainException)
            return false;

        logger.LogWarning("Order domain rule violated: {Message}", domainException.Message);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid order",
            Detail = domainException.Message
        }, cancellationToken);

        return true;
    }
}
