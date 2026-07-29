using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NajaEcho.Infrastructure.Identity;
using Xunit;

namespace NajaEcho.Api.Tests;

// Runs once before any test in this assembly. Each WebApplicationFactory creates OS file
// watchers via PhysicalFilesWatcher; on WSL/Linux the inotify limit (128) is exhausted when
// many factories exist in the same process. Polling never creates inotify descriptors.
internal static class TestAssemblySetup
{
    [ModuleInitializer]
    public static void Initialize() =>
        Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
}

[CollectionDefinition("ApiTests", DisableParallelization = true)]
public sealed class ApiTestCollection;

internal static class TestServiceCollectionExtensions
{
    // These endpoint tests fake every repository, so AppDbContext is never queried by the code
    // under test. The only component that touches the database is the RoleSeeder that runs at host
    // startup (Program.cs, already guarded as non-fatal). Removing it keeps AppDbContext from ever
    // being resolved, so no database — real or in-memory — is needed to exercise the endpoints.
    internal static IServiceCollection StubDatabase(this IServiceCollection services)
    {
        services.RemoveAll<RoleSeeder>();
        return services;
    }
}
