using System.Diagnostics;
using OrderFlow.Application.Observability;

namespace OrderFlow.Api.Observability;

public sealed class HttpMetricsMiddleware(RequestDelegate next)
{
    private static readonly string[] ExcludedPaths = ["/metrics", "/health"];

    public async Task InvokeAsync(HttpContext context, OrderFlowMetrics metrics)
    {
        if (ExcludedPaths.Any(path => context.Request.Path.StartsWithSegments(path)))
        {
            await next(context);
            return;
        }

        var method = context.Request.Method;
        var startedAt = Stopwatch.GetTimestamp();

        // The exception handler clears the matched endpoint while handling an unhandled
        // exception, so the route is read before the rest of the pipeline runs.
        var route = RoutePattern(context);

        try
        {
            await next(context);
        }
        finally
        {
            route ??= RoutePattern(context);

            metrics.RecordHttpRequest(
                method,
                route ?? "unmatched",
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt));
        }
    }

    private static string? RoutePattern(HttpContext context) =>
        (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
}
