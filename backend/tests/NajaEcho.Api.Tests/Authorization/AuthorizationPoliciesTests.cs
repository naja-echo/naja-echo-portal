using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NajaEcho.Api.Authorization;
using NajaEcho.Domain.Users;
using Xunit;

namespace NajaEcho.Api.Tests.Authorization;

public sealed class AuthorizationPoliciesTests
{
    [Fact]
    public async Task EveryRoleGetsAPolicy()
    {
        var provider = BuildProvider();

        foreach (var role in Roles.All)
        {
            var policy = await provider.GetPolicyAsync(role);
            policy.Should().NotBeNull($"'{role}' is in Roles.All so registration should pick it up");
        }
    }

    [Theory]
    [InlineData(Roles.Quartermaster)]
    [InlineData(Roles.CrewResourceOfficer)]
    public async Task AdminSatisfiesEveryNonAdminPolicy(string role)
    {
        var authorized = await Evaluate(role, Roles.Admin);

        authorized.Should().BeTrue("Admin is an implicit superset of every other role");
    }

    [Theory]
    [InlineData(Roles.Quartermaster)]
    [InlineData(Roles.CrewResourceOfficer)]
    public async Task HolderSatisfiesTheirOwnPolicy(string role)
    {
        var authorized = await Evaluate(role, role);

        authorized.Should().BeTrue();
    }

    [Fact]
    public async Task QuartermasterDoesNotSatisfyTheAdminPolicy()
    {
        var authorized = await Evaluate(Roles.Admin, Roles.Quartermaster);

        authorized.Should().BeFalse();
    }

    [Fact]
    public async Task CrewResourceOfficerDoesNotSatisfyTheQuartermasterPolicy()
    {
        var authorized = await Evaluate(Roles.Quartermaster, Roles.CrewResourceOfficer);

        authorized.Should().BeFalse();
    }

    private static async Task<bool> Evaluate(string policyName, params string[] heldRoles)
    {
        var services = BuildServices();
        var policy = await services.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync(policyName);
        policy.Should().NotBeNull();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            heldRoles.Select(r => new Claim(ClaimTypes.Role, r)), "TestScheme"));

        var result = await services.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, resource: null, policy!);

        return result.Succeeded;
    }

    private static IAuthorizationPolicyProvider BuildProvider() =>
        BuildServices().GetRequiredService<IAuthorizationPolicyProvider>();

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(opts => opts.AddPolicies());
        return services.BuildServiceProvider();
    }
}
