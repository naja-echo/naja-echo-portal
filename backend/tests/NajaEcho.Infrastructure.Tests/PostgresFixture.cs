using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NajaEcho.Infrastructure.Identity;
using NajaEcho.Infrastructure.Persistence;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;
using Xunit;

namespace NajaEcho.Infrastructure.Tests;

/// <summary>
/// One Postgres container shared by every test class in the <see cref="PostgresCollection"/>.
/// The full migration chain is applied once; Respawn resets row data between tests so each test
/// still starts from a clean database without paying for a container per test method.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // The model spans two schemas: ASP.NET Identity / character / hangar / warehouse / loot tables
    // live in "public", while the catalog + crafting tables (items, ships, commodities, …) live in
    // "sc". Respawn must be told about BOTH — scoping it to "public" alone silently leaves the "sc"
    // tables un-truncated and leaks data between tests.
    private static readonly string[] Schemas = ["public", "sc"];

    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithDatabase("najaecho_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private NpgsqlConnection _connection = null!;
    private Respawner _respawner = null!;

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        ConnectionString = _pg.GetConnectionString();

        // Apply the real EF migration history once against the shared container.
        await using (var db = CreateContext())
            await db.Database.MigrateAsync();

        // Respawn resets row data between tests while preserving the schema and the
        // __EFMigrationsHistory table (so migrations are never re-applied).
        _connection = new NpgsqlConnection(ConnectionString);
        await _connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = Schemas,
            TablesToIgnore = [new Table("__EFMigrationsHistory")],
        });
    }

    /// <summary>A fresh <see cref="AppDbContext"/> pointed at the shared container, configured like production.</summary>
    public AppDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(opts);
    }

    /// <summary>Reset all row data (across every schema above). Call at the start of each test.</summary>
    public Task ResetAsync() => _respawner.ResetAsync(_connection);

    /// <summary>
    /// Builds a service provider wired with EF + ASP.NET Identity against the shared container, for
    /// the tests that need a real <see cref="UserManager{T}"/> or <see cref="RoleManager{T}"/>. Pass
    /// <paramref name="configure"/> to register additional services (e.g. an external login service).
    /// The caller owns the returned provider and must dispose it.
    /// </summary>
    public ServiceProvider BuildIdentityProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(ConnectionString)
                .UseSnakeCaseNamingConvention());
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _pg.DisposeAsync();
    }
}

/// <summary>
/// Test classes that share the single Postgres container join this collection.
/// The collection runs its classes serially, which is required because they share one database.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
