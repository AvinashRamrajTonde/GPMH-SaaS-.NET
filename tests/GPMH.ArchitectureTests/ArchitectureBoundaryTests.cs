using System.Reflection;
using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Common;
using GPMH.Infrastructure.Persistence;
using GPMH.WebAPI.Controllers;
using NetArchTest.Rules;
using Xunit;

namespace GPMH.ArchitectureTests;

public sealed class ArchitectureBoundaryTests
{
    private static readonly Assembly DomainAssembly = typeof(BaseEntity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ITenantContext).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AppDbContext).Assembly;
    private static readonly Assembly WebApiAssembly = typeof(HealthController).Assembly;

    [Fact]
    public void Domain_Should_Not_Depend_On_Outer_Layers_Or_Frameworks()
    {
        TestResult result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "GPMH.Application",
                "GPMH.Infrastructure",
                "GPMH.WebAPI",
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "MediatR",
                "FluentValidation")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_WebApi_Or_EfCore()
    {
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "GPMH.Infrastructure",
                "GPMH.WebAPI",
                "Microsoft.EntityFrameworkCore",
                "Npgsql")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_WebAPI()
    {
        TestResult result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn("GPMH.WebAPI")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void WebApi_Controllers_And_Middleware_Should_Not_Reference_Persistence_Directly()
    {
        TestResult result = Types.InAssembly(WebApiAssembly)
            .That()
            .ResideInNamespace("GPMH.WebAPI.Controllers")
            .Or()
            .ResideInNamespace("GPMH.WebAPI.Middleware")
            .ShouldNot()
            .HaveDependencyOn("GPMH.Infrastructure.Persistence")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void All_Domain_Entities_Must_Implement_ITenantEntity_Or_IGlobalEntity()
    {
        IEnumerable<Type> entityTypes = DomainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(BaseEntity).IsAssignableFrom(t));

        foreach (Type entityType in entityTypes)
        {
            bool hasScopeMarker = typeof(ITenantEntity).IsAssignableFrom(entityType)
                || typeof(IGlobalEntity).IsAssignableFrom(entityType);

            Assert.True(
                hasScopeMarker,
                $"Entity '{entityType.FullName}' must explicitly implement ITenantEntity or IGlobalEntity.");
        }
    }
}

