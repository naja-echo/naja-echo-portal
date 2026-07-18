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
using NajaEcho.Domain.Ships;
using NajaEcho.Domain.Loot;
using NajaEcho.Domain.Warehouse;
using NajaEcho.Infrastructure.Identity;
using NajaEcho.Infrastructure.Persistence.Configurations;

namespace NajaEcho.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
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
    }
}
