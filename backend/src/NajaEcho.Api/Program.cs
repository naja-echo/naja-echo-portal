using System.Security.Claims;
using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NajaEcho.Api.Authorization;
using NajaEcho.Api.Common;
using NajaEcho.Api.Features.Auth;
using NajaEcho.Api.Features.Admin.Commodities;
using NajaEcho.Api.Features.Admin.Items;
using NajaEcho.Api.Features.Admin.Locations;
using NajaEcho.Api.Features.Admin.Ships;
using NajaEcho.Api.Features.Admin.Users;
using NajaEcho.Api.Features.Characters;
using NajaEcho.Api.Features.Hangar;
using NajaEcho.Api.Features.Loot;
using NajaEcho.Api.Features.Warehouse;
using NajaEcho.Application.Features.Auth.SignInWithDiscord;
using NajaEcho.Domain.Users;
using NajaEcho.Infrastructure;
using NajaEcho.Infrastructure.Identity;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) =>
    {
        lc.MinimumLevel.Information()
          .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
          .Enrich.FromLogContext()
          .Enrich.WithProperty("Application", "NajaEchoPortal");

        if (ctx.HostingEnvironment.IsDevelopment())
        {
            lc.WriteTo.Console();
        }
        else
        {
            lc.WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
        }
    });

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.Configure<ForwardedHeadersOptions>(opts =>
    {
        opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        opts.KnownIPNetworks.Clear();
        opts.KnownProxies.Clear();
    });

    var frontendOrigin = builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173";

    builder.Services.AddCors(opts =>
        opts.AddPolicy("Frontend", policy => policy
            .WithOrigins(frontendOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

    var isProduction = builder.Environment.IsProduction();

    builder.Services
        .AddAuthentication(opts =>
        {
            opts.DefaultScheme = IdentityConstants.ApplicationScheme;
            opts.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
        })
        .AddCookie(IdentityConstants.ApplicationScheme, opts =>
        {
            opts.Cookie.Name = isProduction ? "__Host-najaecho.auth" : "najaecho.auth";
            opts.Cookie.HttpOnly = true;
            opts.Cookie.SameSite = SameSiteMode.Lax;
            opts.Cookie.SecurePolicy = isProduction
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.None;
            opts.SlidingExpiration = true;
            opts.ExpireTimeSpan = TimeSpan.FromHours(24);

            opts.Events.OnValidatePrincipal = async ctx =>
            {
                // 7-day absolute cap — reject sessions older than 7 days regardless of sliding renewal
                if (ctx.Properties.IssuedUtc.HasValue &&
                    (DateTimeOffset.UtcNow - ctx.Properties.IssuedUtc.Value).TotalDays >= 7)
                {
                    ctx.RejectPrincipal();
                    await ctx.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            };

            opts.Events.OnRedirectToLogin = async ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api"))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    var body = System.Text.Json.JsonSerializer.Serialize(new ProblemDetails
                    {
                        Title = "Authentication required.",
                        Status = StatusCodes.Status401Unauthorized,
                        Instance = ctx.Request.Path,
                    });
                    ctx.Response.ContentType = "application/problem+json";
                    await ctx.Response.WriteAsync(body);
                    return;
                }
                ctx.Response.Redirect(ctx.RedirectUri);
            };
        })
        .AddDiscord(opts =>
        {
            opts.ClientId = builder.Configuration["Discord:ClientId"]
                ?? throw new InvalidOperationException("Discord:ClientId not configured.");
            opts.ClientSecret = builder.Configuration["Discord:ClientSecret"]
                ?? throw new InvalidOperationException("Discord:ClientSecret not configured.");

            // Minimized scope — only identify (no email)
            opts.Scope.Clear();
            opts.Scope.Add("identify");
            opts.SaveTokens = false;
            opts.CallbackPath = "/api/auth/discord/callback";
            opts.SignInScheme = IdentityConstants.ApplicationScheme;

            opts.Events.OnTicketReceived = async ctx =>
            {
                var cfg = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var origin = cfg["Frontend:Origin"] ?? string.Empty;

                var claims = ctx.Principal?.Claims.ToList() ?? [];
                var discordId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                var username = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

                if (discordId is null || username is null)
                {
                    // Safe milestone — no sensitive values logged
                    Log.Warning("Discord OAuth callback: missing required identity claims");
                    ctx.Response.Redirect($"{origin}/auth/error?reason=oauth_error");
                    ctx.HandleResponse();
                    return;
                }

                // Safe milestone: provider key only (Discord user ID is not sensitive)
                Log.Information("Discord external login succeeded {ProviderKey}", discordId);

                var profile = new DiscordProfile
                {
                    Id = discordId,
                    Username = username,
                    GlobalName = claims.FirstOrDefault(c => c.Type == "urn:discord:user:global_name")?.Value,
                };

                var handler = ctx.HttpContext.RequestServices.GetRequiredService<SignInWithDiscordHandler>();

                SignInWithDiscordResult result;
                try
                {
                    result = await handler.HandleAsync(
                        new SignInWithDiscordCommand(profile), ctx.HttpContext.RequestAborted);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error during Discord login callback — user not created");
                    ctx.Response.Redirect($"{origin}/auth/error?reason=server_error");
                    ctx.HandleResponse();
                    return;
                }

                Log.Information("Local user linked {UserId}", result.UserId);

                var userManager = ctx.HttpContext.RequestServices
                    .GetRequiredService<UserManager<ApplicationUser>>();
                var appUser = await userManager.FindByIdAsync(result.UserId.ToString());
                var userRoles = appUser is not null
                    ? await userManager.GetRolesAsync(appUser)
                    : [];

                var roleClaims = userRoles.Select(r => new Claim(ClaimTypes.Role, r));

                var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, result.UserId.ToString()),
                    new Claim(ClaimTypes.Name, result.DisplayName),
                    ..roleClaims,
                ], IdentityConstants.ApplicationScheme);

                ctx.Principal = new ClaimsPrincipal(identity);
                ctx.Properties!.IsPersistent = true;
                ctx.Properties.IssuedUtc = DateTimeOffset.UtcNow;
                ctx.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24);

                Log.Information("Local sign-in succeeded {UserId}", result.UserId);
            };

            opts.Events.OnRemoteFailure = ctx =>
            {
                var cfg = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var origin = cfg["Frontend:Origin"] ?? string.Empty;

                var reason = ctx.Failure?.Message?.Contains("Correlation",
                    StringComparison.OrdinalIgnoreCase) == true
                    ? "state_mismatch"
                    : "oauth_error";

                // Safe milestone — failure message is generic, no tokens
                Log.Warning("Discord OAuth remote failure {Reason}", reason);
                ctx.Response.Redirect($"{origin}/auth/error?reason={reason}");
                ctx.HandleResponse();
                return Task.CompletedTask;
            };
        });

    builder.Services.AddAuthorization(opts => opts.AddPolicies());

    var app = builder.Build();

    // Seed roles at startup (non-fatal if it fails in test environments)
    try
    {
        using var scope = app.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<RoleSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Role seeding failed (non-fatal)");
    }

    app.UseForwardedHeaders();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.EnrichDiagnosticContext = (diag, ctx) =>
        {
            diag.Set("RequestPath", ctx.Request.Path);
        };
        // Scrub sensitive auth paths from request logging
        opts.GetLevel = (ctx, _, _) =>
            ctx.Request.Path.StartsWithSegments("/api/auth/discord/callback")
                ? LogEventLevel.Debug  // suppress detailed logging for callback path
                : LogEventLevel.Information;
    });

    app.UseExceptionHandler();
    app.UseCors("Frontend");
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
        .AllowAnonymous();

    app.MapAuthEndpoints();
    app.MapShipAdminEndpoints();
    app.MapUserAdminEndpoints();
    app.MapLocationAdminEndpoints();
    app.MapItemAdminEndpoints();
    app.MapCommodityAdminEndpoints();
    app.MapCharacterEndpoints();
    app.MapHangarEndpoints();
    app.MapWarehouseEndpoints();
    app.MapLootEndpoints();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application startup failed.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
