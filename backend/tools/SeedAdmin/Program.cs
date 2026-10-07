// One-off dev tool: creates the first Secretariat login for manual testing, since every
// account in the app is otherwise created by an invite from an existing admin (there's no
// public registration). Not wired into HRS.sln or any deploy path — run it directly:
//
//   dotnet run --project backend/tools/SeedAdmin -- <email> <password> <name>
//
// Defaults to the docker-compose dev Postgres connection string; pass a 4th arg to override.
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Infrastructure.Identity;
using HRS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

if (args.Length < 3)
{
    Console.WriteLine("Usage: dotnet run --project backend/tools/SeedAdmin -- <email> <password> <name> [connectionString]");
    return 1;
}

var email = args[0];
var password = args[1];
var name = args[2];
var connectionString = args.Length > 3
    ? args[3]
    : "Host=localhost;Port=5433;Database=hrs;Username=hrs;Password=hrs_dev_password";

var services = new ServiceCollection();

services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention());

services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // Matches HRS.Infrastructure/DependencyInjection.cs's policy (FR-IAM-002).
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

var existing = await userManager.FindByEmailAsync(email);
if (existing is not null)
{
    Console.WriteLine($"A user with email {email} already exists (id {existing.Id}). Nothing to do.");
    return 1;
}

var user = new ApplicationUser
{
    Id = Guid.CreateVersion7(),
    Name = name,
    UserName = email,
    Email = email,
    EmailConfirmed = true,
};

var createResult = await userManager.CreateAsync(user, password);
if (!createResult.Succeeded)
{
    Console.WriteLine("Failed to create user:");
    foreach (var error in createResult.Errors)
    {
        Console.WriteLine($"  {error.Code}: {error.Description}");
    }

    return 1;
}

db.RoleAssignments.Add(new RoleAssignment(
    Guid.CreateVersion7(),
    user.Id,
    OrganizationSeedIds.NationalUnitId,
    RoleName.Secretariat,
    delegateScope: null,
    startsAt: DateTimeOffset.UtcNow,
    endsAt: null,
    appointedBy: null));
await db.SaveChangesAsync();

Console.WriteLine($"Created Secretariat user '{email}' (id {user.Id}) on the National unit.");
Console.WriteLine("Sign in at http://localhost:3000/login with that email and password.");

return 0;
