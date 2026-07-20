using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Domain.Blueprints;
using NajaEcho.Domain.Characters;
using NajaEcho.Domain.Commodities;
using NajaEcho.Domain.Hangar;
using NajaEcho.Domain.ItemCategories;
using NajaEcho.Domain.Items;
using NajaEcho.Domain.Locations;
using NajaEcho.Domain.Organizations;
using NajaEcho.Domain.Ships;
using NajaEcho.Domain.Loot;
using NajaEcho.Domain.Warehouse;
using NajaEcho.Application.Abstractions;
using NajaEcho.Infrastructure.Identity;
using NajaEcho.Infrastructure.Persistence.Configurations;

namespace NajaEcho.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IOrganizationContext organizationContext)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    /// <summary>
    /// The organization every <see cref="IOrganizationScoped"/> entity is restricted to.
    /// </summary>
    /// <remarks>
    /// Read as an instance member by the global query filter, never captured as a local. EF Core
    /// parameterizes instance-member access, so the compiled model stays cacheable while the value
    /// varies per request. Capturing it at model-build time would bake the first request's
    /// organization into the cached model and serve it to every tenant — see OrganizationScopeTests.
    /// </remarks>
    public Guid? CurrentOrganizationId => organizationContext.CurrentOrganizationId;

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    public DbSet<Ship> Ships => Set<Ship>();
    public DbSet<HangarEntry> HangarEntries => Set<HangarEntry>();
    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Commodity> Commodities => Set<Commodity>();
    public DbSet<WarehouseInventoryEntry> WarehouseInventory => Set<WarehouseInventoryEntry>();
    public DbSet<WarehouseMaterialEntry> WarehouseMaterialInventory => Set<WarehouseMaterialEntry>();
    public DbSet<ItemAttribute> ItemAttributes => Set<ItemAttribute>();
    public DbSet<ShipComponentAttributes> ShipComponentAttributes => Set<ShipComponentAttributes>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<PendingCharacterRegistration> PendingCharacterRegistrations => Set<PendingCharacterRegistration>();
    public DbSet<StarSystem> StarSystems => Set<StarSystem>();
    public DbSet<SpaceStation> SpaceStations => Set<SpaceStation>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<LootLedgerEntry> LootLedger => Set<LootLedgerEntry>();
    public DbSet<LootMemberStanding> LootMemberStandings => Set<LootMemberStanding>();
    public DbSet<CraftingBlueprint> Blueprints => Set<CraftingBlueprint>();
    public DbSet<CraftingBlueprintTier> BlueprintTiers => Set<CraftingBlueprintTier>();
    public DbSet<CraftingBlueprintSlotOption> BlueprintSlotOptions => Set<CraftingBlueprintSlotOption>();
    public DbSet<CraftingMaterial> CraftingMaterials => Set<CraftingMaterial>();
    public DbSet<CraftingProperty> CraftingProperties => Set<CraftingProperty>();
    public DbSet<CraftingDataset> CraftingDatasets => Set<CraftingDataset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfiguration(new ApplicationUserConfiguration());
        modelBuilder.ApplyConfiguration(new ShipConfiguration());
        modelBuilder.ApplyConfiguration(new HangarEntryConfiguration());
        modelBuilder.ApplyConfiguration(new ItemCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new ItemConfiguration());
        modelBuilder.ApplyConfiguration(new CommodityConfiguration());
        modelBuilder.ApplyConfiguration(new WarehouseInventoryEntryConfiguration());
        modelBuilder.ApplyConfiguration(new WarehouseMaterialEntryConfiguration());
        modelBuilder.ApplyConfiguration(new ItemAttributeConfiguration());
        modelBuilder.ApplyConfiguration(new ShipComponentAttributesConfiguration());
        modelBuilder.ApplyConfiguration(new CharacterConfiguration());
        modelBuilder.ApplyConfiguration(new PendingCharacterRegistrationConfiguration());
        modelBuilder.ApplyConfiguration(new StarSystemConfiguration());
        modelBuilder.ApplyConfiguration(new SpaceStationConfiguration());
        modelBuilder.ApplyConfiguration(new CityConfiguration());
        modelBuilder.ApplyConfiguration(new LootLedgerEntryConfiguration());
        modelBuilder.ApplyConfiguration(new LootMemberStandingConfiguration());
        modelBuilder.ApplyConfiguration(new CraftingBlueprintConfiguration());
        modelBuilder.ApplyConfiguration(new CraftingBlueprintTierConfiguration());
        modelBuilder.ApplyConfiguration(new CraftingBlueprintSlotOptionConfiguration());
        modelBuilder.ApplyConfiguration(new CraftingMaterialConfiguration());
        modelBuilder.ApplyConfiguration(new CraftingPropertyConfiguration());
        modelBuilder.ApplyConfiguration(new CraftingDatasetConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationMembershipConfiguration());

        // Last, so it sees every configured entity. No entity implements IOrganizationScoped yet —
        // this is the seam #32/#33/#34 opt into. Passing the instance member (not a captured local)
        // is what keeps the filter per-request rather than baked into the cached model.
        modelBuilder.ApplyOrganizationFilters(() => CurrentOrganizationId);
    }
}
