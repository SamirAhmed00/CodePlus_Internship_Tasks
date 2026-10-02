using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Application.Features.Dashboard.GetDashboardOrders;

namespace OrderFlow.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(ISender sender) : ControllerBase
{
    [HttpGet("orders")]
    public async Task<IActionResult> GetDashboardOrders(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDashboardOrdersQuery(), cancellationToken);

        return Ok(result);
    }
}
