using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class AccessPolicyTests(ApiFactory factory)
{
    private IAccessPolicy CreatePolicy(out IServiceScope scope)
    {
        scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IAccessPolicy>();
    }

    [Fact]
    public async Task HasRoleAsyncIsTrueForAnActiveAssignment()
    {
        var userId = await factory.CreateUserAsync($"{Guid.NewGuid():N}@example.org", "Correct-Horse-Ap1");
        await factory.CreateRoleAssignmentAsync(userId, Guid.NewGuid(), RoleName.Secretariat);

        var policy = CreatePolicy(out var scope);
        using (scope)
        {
            Assert.True(await policy.HasRoleAsync(userId, RoleName.Secretariat, CancellationToken.None));
            Assert.False(await policy.HasRoleAsync(userId, RoleName.SysAdmin, CancellationToken.None));
        }
    }

    [Fact]
    public async Task HasRoleAsyncIsFalseForAnExpiredAssignment()
    {
        var userId = await factory.CreateUserAsync($"{Guid.NewGuid():N}@example.org", "Correct-Horse-Ap2");
        var now = DateTimeOffset.UtcNow;
        await factory.CreateRoleAssignmentAsync(
            userId, Guid.NewGuid(), RoleName.UnitHead, startsAt: now.AddDays(-10), endsAt: now.AddDays(-1));

        var policy = CreatePolicy(out var scope);
        using (scope)
        {
            Assert.False(await policy.HasRoleAsync(userId, RoleName.UnitHead, CancellationToken.None));
        }
    }

    [Fact]
    public async Task HasRoleForUnitAsyncOnlyMatchesTheSpecificUnit()
    {
        var userId = await factory.CreateUserAsync($"{Guid.NewGuid():N}@example.org", "Correct-Horse-Ap3");
        var unitId = Guid.NewGuid();
        await factory.CreateRoleAssignmentAsync(userId, unitId, RoleName.UnitHead);

        var policy = CreatePolicy(out var scope);
        using (scope)
        {
            Assert.True(await policy.HasRoleForUnitAsync(userId, unitId, RoleName.UnitHead, CancellationToken.None));
            Assert.False(await policy.HasRoleForUnitAsync(userId, Guid.NewGuid(), RoleName.UnitHead, CancellationToken.None));
        }
    }
}
