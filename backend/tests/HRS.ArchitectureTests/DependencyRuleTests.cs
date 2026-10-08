using System.Reflection;
using HRS.Api.Common;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Infrastructure;
using NetArchTest.Rules;

namespace HRS.ArchitectureTests;

public class DependencyRuleTests
{
    private static readonly Assembly DomainAssembly = typeof(Result).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IClock).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(DependencyInjection).Assembly;
    private static readonly Assembly ApiAssembly = typeof(ResultExtensions).Assembly;
    private static readonly Assembly WorkerAssembly = typeof(HRS.Worker.WindowJobsRecurringJob).Assembly;

    [Fact]
    public void DomainShouldNotDependOnOuterLayers()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny("HRS.Application", "HRS.Infrastructure", "HRS.Api", "HRS.Worker")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void DomainShouldNotDependOnFrameworks()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "MediatR")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void ApplicationShouldNotDependOnOuterLayers()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny("HRS.Infrastructure", "HRS.Api", "HRS.Worker")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void InfrastructureShouldNotDependOnCompositionRoots()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .Should()
            .NotHaveDependencyOnAny("HRS.Api", "HRS.Worker")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void ApiShouldNotDependOnWorker()
    {
        var result = Types.InAssembly(ApiAssembly)
            .Should()
            .NotHaveDependencyOn("HRS.Worker")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void WorkerShouldNotDependOnApi()
    {
        var result = Types.InAssembly(WorkerAssembly)
            .Should()
            .NotHaveDependencyOn("HRS.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) => result.IsSuccessful
        ? string.Empty
        : "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
