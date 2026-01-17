using Ardalis.Specification;
using ManagementApi.Domain.Common;

namespace ManagementApi.Application.Common.Interfaces;

public interface IRepository<T> : IRepositoryBase<T> where T : class, IAggregateRoot
{
}

public interface IReadRepository<T> : IReadRepositoryBase<T> where T : class, IAggregateRoot
{
}
