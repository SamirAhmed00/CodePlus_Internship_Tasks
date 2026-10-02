namespace OrderFlow.Application.Abstractions;

public interface IOrderCache
{
    Task<string?> GetAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task SetAsync(Guid orderId, string json, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid orderId, CancellationToken cancellationToken = default);
}
