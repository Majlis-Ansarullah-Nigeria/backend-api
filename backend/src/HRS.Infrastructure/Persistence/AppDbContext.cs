using HRS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HRS.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
}
