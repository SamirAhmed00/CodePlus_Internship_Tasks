using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.ReadModels;

namespace OrderFlow.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<Order> Orders { get; }

    DbSet<OrderDashboard> OrderDashboards { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
