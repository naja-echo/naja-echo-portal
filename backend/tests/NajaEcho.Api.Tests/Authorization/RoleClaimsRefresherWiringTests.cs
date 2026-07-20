using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NajaEcho.Api.Authorization;
using NajaEcho.Application.Abstractions;
using Xunit;

namespace NajaEcho.Api.Tests.Authorization;

/// <summary>
/// The cookie <c>OnValidatePrincipal</c> event resolves <see cref="RoleClaimsRefresher"/> lazily
/// from request services, and every endpoint test replaces the cookie scheme outright — so a
/// missing registration would not surface anywhere else until runtime.
/// </summary>
[Collection("ApiTests")]
public sealed class RoleClaimsRefresherWiringTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RoleClaimsRefresherWiringTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Discord:ClientId"] = "test-client-id",
                    ["Discord:ClientSecret"] = "test-client-secret",
                    ["ConnectionStrings:Default"] = "Host=localhost;Database=test;Username=test;Password=test",
                });
            });

            b.ConfigureTestServices(services => services.StubDatabase());
        });
    }

    [Fact]
    public void RoleClaimsRefresher_ResolvesFromTheRealContainer()
    {
        using var scope = _factory.Services.CreateScope();

        var refresher = scope.ServiceProvider.GetService<RoleClaimsRefresher>();

        refresher.Should().NotBeNull("OnValidatePrincipal resolves it on every authenticated request");
    }

    [Fact]
    public void SessionInvalidator_IsASingletonSoTheFlagIsVisibleAcrossRequests()
    {
        using var scopeA = _factory.Services.CreateScope();
        using var scopeB = _factory.Services.CreateScope();

        var a = scopeA.ServiceProvider.GetRequiredService<IUserSessionInvalidator>();
        var b = scopeB.ServiceProvider.GetRequiredService<IUserSessionInvalidator>();

        a.Should().BeSameAs(b, "a per-scope instance would lose the invalidation signal");
    }
}
