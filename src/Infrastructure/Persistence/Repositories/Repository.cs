using Ardalis.Specification.EntityFrameworkCore;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Domain.Common;

namespace ManagementApi.Infrastructure.Persistence.Repositories;

public class Repository<T> : RepositoryBase<T>, IRepository<T> where T : class, IAggregateRoot
{
    public Repository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }
}

public class ReadRepository<T> : RepositoryBase<T>, IReadRepository<T> where T : class, IAggregateRoot
{
    public ReadRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }
}
