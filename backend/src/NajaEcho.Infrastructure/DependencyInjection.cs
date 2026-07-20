using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Auth.GetCurrentUser;
using NajaEcho.Application.Features.Blueprints.GetBlueprints;
using NajaEcho.Application.Features.Blueprints.ImportBlueprints;
using NajaEcho.Application.Features.Auth.SignInWithDiscord;
using NajaEcho.Application.Features.Hangar.AddShipToHangar;
using NajaEcho.Application.Features.Hangar.GetMyHangar;
using NajaEcho.Application.Features.Hangar.GetOrgHangar;
using NajaEcho.Application.Features.Hangar.GetOwningMembers;
using NajaEcho.Application.Features.Hangar.ImportHangar;
using NajaEcho.Application.Features.Hangar.RemoveShipFromHangar;
using NajaEcho.Application.Features.Hangar.SearchCatalogShips;
using NajaEcho.Application.Features.Ships.GetShipById;
using NajaEcho.Application.Features.Ships.GetShips;
using NajaEcho.Application.Features.Ships.ImportShips;
using NajaEcho.Application.Features.Commodities.GetCommodities;
using NajaEcho.Application.Features.Commodities.ImportCommodities;
using NajaEcho.Application.Features.ItemCategories.GetCategories;
using NajaEcho.Application.Features.ItemCategories.RefreshCategories;
using NajaEcho.Application.Features.Items.ImportItems;
using NajaEcho.Application.Features.Warehouse.GetInventory;
using NajaEcho.Application.Features.Warehouse.GetInventoryFilters;
using NajaEcho.Application.Features.Warehouse.Materials.AddMaterial;
using NajaEcho.Application.Features.Warehouse.Materials.ChangeMaterialQuantity;
using NajaEcho.Application.Features.Warehouse.Materials.GetMaterialFilters;
using NajaEcho.Application.Features.Warehouse.Materials.GetMaterials;
using NajaEcho.Application.Features.Warehouse.Materials.RemoveMaterial;
using NajaEcho.Application.Features.Warehouse.Materials.SearchCommodities;
using NajaEcho.Application.Features.Warehouse.SearchCatalogItems;
using NajaEcho.Application.Features.Warehouse.AddInventoryItem;
using NajaEcho.Application.Features.Warehouse.ChangeInventoryQuantity;
using NajaEcho.Application.Features.Warehouse.RemoveInventoryItem;
using NajaEcho.Application.Features.Warehouse.UpdateInventoryItem;
using NajaEcho.Application.Features.Warehouse.ShipComponents.GetShipComponentFilters;
using NajaEcho.Application.Features.Warehouse.ShipComponents.GetShipComponents;
using NajaEcho.Application.Features.Warehouse.ShipComponents.SearchSystemsCatalog;
using NajaEcho.Application.Features.Admin.Organizations.AssignOrganization;
using NajaEcho.Application.Features.Admin.Organizations.GetOrganizations;
using NajaEcho.Application.Features.Admin.Users.AddCharacterForUser;
using NajaEcho.Application.Features.Admin.Users.AssignRoles;
using NajaEcho.Application.Features.Admin.Users.GetUsers;
using NajaEcho.Application.Features.Characters.GetCharacters;
using NajaEcho.Application.Features.Characters.GetRegistration;
using NajaEcho.Application.Features.Characters.StartRegistration;
using NajaEcho.Application.Features.Characters.VerifyCharacter;
using NajaEcho.Application.Features.Locations.ImportLocations;
using NajaEcho.Application.Features.Loot.AddLootPoints;
using NajaEcho.Application.Features.Loot.AddOrgPoints;
using NajaEcho.Application.Features.Loot.GetDistribution;
using NajaEcho.Application.Features.Loot.GetMemberLedger;
using NajaEcho.Application.Features.Warehouse.GetLocations;
using NajaEcho.Application.Features.Warehouse.TransferInventoryItem;
using NajaEcho.Application.Features.Warehouse.Materials.TransferMaterial;
using NajaEcho.Application.Features.Warehouse.Materials.UpdateMaterial;
using NajaEcho.Infrastructure.Blueprints;
using NajaEcho.Infrastructure.Characters;
using NajaEcho.Infrastructure.Commodities;
using NajaEcho.Infrastructure.Hangar;
using NajaEcho.Infrastructure.Organizations;
using NajaEcho.Infrastructure.Loot;
using NajaEcho.Infrastructure.Loot;
using NajaEcho.Infrastructure.Identity;
using NajaEcho.Infrastructure.Imports;
using NajaEcho.Infrastructure.ItemCategories;
using NajaEcho.Infrastructure.Items;
using NajaEcho.Infrastructure.Locations;
using NajaEcho.Infrastructure.Persistence;
using NajaEcho.Infrastructure.Ships;
using NajaEcho.Infrastructure.Warehouse;

namespace NajaEcho.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("Default"))
                .UseSnakeCaseNamingConvention());

        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IExternalLoginService, DiscordExternalLoginService>();

        services.AddScoped<SignInWithDiscordHandler>();
        services.AddScoped<GetCurrentUserHandler>();

        // Ships
        services.AddScoped<IShipRepository, ShipRepository>();
        services.AddHttpClient<IUexVehicleClient, UexVehicleClient>(client =>
        {
            var baseUrl = configuration["UexVehicleClient:BaseUrl"] ?? "https://api.uexcorp.uk/2.0/";
            client.BaseAddress = new Uri(baseUrl);
        });
        services.AddSingleton<IImportCoordinator, ImportCoordinator>();
        services.AddScoped<ImportShipsHandler>();
        services.AddScoped<GetShipsHandler>();
        services.AddScoped<GetShipByIdHandler>();

        // Locations (Star Systems, Space Stations & Cities)
        services.AddHttpClient<IUexLocationClient, UexLocationClient>(client =>
        {
            var baseUrl = configuration["UexVehicleClient:BaseUrl"] ?? "https://api.uexcorp.uk/2.0/";
            client.BaseAddress = new Uri(baseUrl);
        });
        services.AddScoped<IStarSystemRepository, StarSystemRepository>();
        services.AddScoped<ISpaceStationRepository, SpaceStationRepository>();
        services.AddScoped<ICityRepository, CityRepository>();
        services.AddScoped<ImportLocationsHandler>();
        services.AddScoped<GetLocationsHandler>();

        // Hangar
        services.AddScoped<IHangarRepository, HangarRepository>();
        services.AddScoped<GetMyHangarHandler>();
        services.AddScoped<GetOrgHangarHandler>();
        services.AddScoped<GetOwningMembersHandler>();
        services.AddScoped<SearchCatalogShipsHandler>();
        services.AddScoped<AddShipToHangarHandler>();
        services.AddScoped<RemoveShipFromHangarHandler>();
        services.AddScoped<ImportHangarHandler>();

        // Item Categories & Items
        services.AddHttpClient<IUexCategoryClient, UexCategoryClient>(client =>
        {
            var baseUrl = configuration["UexVehicleClient:BaseUrl"] ?? "https://api.uexcorp.uk/2.0/";
            client.BaseAddress = new Uri(baseUrl);
        });
        services.AddHttpClient<IUexItemClient, UexItemClient>(client =>
        {
            var baseUrl = configuration["UexVehicleClient:BaseUrl"] ?? "https://api.uexcorp.uk/2.0/";
            client.BaseAddress = new Uri(baseUrl);
        });
        services.AddScoped<IItemCategoryRepository, ItemCategoryRepository>();
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<RefreshCategoriesHandler>();
        services.AddScoped<GetCategoriesHandler>();
        services.AddScoped<ImportItemsHandler>();

        // Commodities
        services.AddHttpClient<IUexCommodityClient, UexCommodityClient>(client =>
        {
            var baseUrl = configuration["UexVehicleClient:BaseUrl"] ?? "https://api.uexcorp.uk/2.0/";
            client.BaseAddress = new Uri(baseUrl);
        });
        services.AddScoped<ICommodityRepository, CommodityRepository>();
        services.AddScoped<GetCommoditiesHandler>();
        services.AddScoped<ImportCommoditiesHandler>();

        // Users
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<GetUsersHandler>();
        services.AddScoped<AddCharacterForUserHandler>();
        services.AddScoped<AssignRolesHandler>();

        // Organizations
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<GetOrganizationsHandler>();
        services.AddScoped<AssignOrganizationHandler>();

        // Warehouse
        services.AddScoped<IWarehouseInventoryRepository, WarehouseInventoryRepository>();
        services.AddScoped<GetInventoryHandler>();
        services.AddScoped<GetInventoryFiltersHandler>();
        services.AddScoped<SearchCatalogItemsHandler>();
        services.AddScoped<AddInventoryItemHandler>();
        services.AddScoped<ChangeInventoryQuantityHandler>();
        services.AddScoped<UpdateInventoryItemHandler>();
        services.AddScoped<RemoveInventoryItemHandler>();

        services.AddScoped<TransferInventoryItemHandler>();

        // Warehouse Materials
        services.AddScoped<IMaterialInventoryRepository, MaterialInventoryRepository>();
        services.AddScoped<GetMaterialsQueryHandler>();
        services.AddScoped<GetMaterialFiltersQueryHandler>();
        services.AddScoped<SearchCommoditiesQueryHandler>();
        services.AddScoped<AddMaterialHandler>();
        services.AddScoped<ChangeMaterialQuantityHandler>();
        services.AddScoped<UpdateMaterialHandler>();
        services.AddScoped<RemoveMaterialHandler>();
        services.AddScoped<TransferMaterialHandler>();

        // Ship Components
        services.AddScoped<IShipComponentRepository, ShipComponentRepository>();
        services.AddScoped<GetShipComponentsQueryHandler>();
        services.AddScoped<GetShipComponentFiltersQueryHandler>();
        services.AddScoped<SearchSystemsCatalogQueryHandler>();
        services.AddHttpClient<IUexItemAttributeClient, UexItemAttributeClient>(client =>
        {
            var baseUrl = configuration["UexVehicleClient:BaseUrl"] ?? "https://api.uexcorp.uk/2.0/";
            client.BaseAddress = new Uri(baseUrl);
        });

        // Characters
        services.AddScoped<ICharacterRepository, CharacterRepository>();
        services.AddScoped<IPendingRegistrationRepository, PendingRegistrationRepository>();
        services.AddHttpClient<IRsiCitizenClient, RsiCitizenClient>(client =>
        {
            client.BaseAddress = new Uri("https://robertsspaceindustries.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<StartRegistrationHandler>();
        services.AddScoped<GetRegistrationHandler>();
        services.AddScoped<VerifyCharacterHandler>();
        services.AddScoped<GetCharactersHandler>();

        // Role seeder — seeds every role in Domain.Users.Roles.All
        services.AddScoped<RoleSeeder>();

        // Singleton so the invalidation flag is visible to every request in the process.
        services.AddSingleton<IUserSessionInvalidator, InMemoryUserSessionInvalidator>();

        // Loot Ledger
        services.AddScoped<ILootLedgerRepository, LootLedgerRepository>();
        services.AddScoped<GetMemberLedgerHandler>();
        services.AddScoped<GetDistributionHandler>();
        services.AddScoped<AddOrgPointsHandler>();
        services.AddScoped<AddLootPointsHandler>();

        // Crafting Blueprints
        services.AddScoped<IBlueprintRepository, BlueprintRepository>();
        services.AddScoped<ImportBlueprintsHandler>();
        services.AddScoped<GetBlueprintsHandler>();

        return services;
    }
}
