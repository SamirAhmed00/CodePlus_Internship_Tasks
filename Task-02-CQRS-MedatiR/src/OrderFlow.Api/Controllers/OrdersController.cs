using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Application.Features.Orders.CreateOrder;
using OrderFlow.Application.Features.Orders.GetOrderById;
using OrderFlow.Application.Features.Orders.ListOrders;

namespace OrderFlow.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return Created($"/api/orders/{result.Id}", result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrderById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetOrderByIdQuery(id), cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> ListOrders(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListOrdersQuery(), cancellationToken);

        return Ok(result);
    }
}
