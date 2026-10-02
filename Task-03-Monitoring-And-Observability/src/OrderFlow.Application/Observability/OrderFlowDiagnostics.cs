using System.Diagnostics;

namespace OrderFlow.Application.Observability;

public static class OrderFlowDiagnostics
{
    public const string MeterName = "OrderFlow";

    public static readonly ActivitySource ActivitySource = new(MeterName);
}
