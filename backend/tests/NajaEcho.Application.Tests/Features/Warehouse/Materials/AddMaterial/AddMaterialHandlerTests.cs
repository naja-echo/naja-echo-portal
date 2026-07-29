using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Warehouse.Materials.AddMaterial;
using NajaEcho.Application.Features.Warehouse.Materials.GetMaterialFilters;
using NajaEcho.Application.Features.Warehouse.Materials.GetMaterials;
using NajaEcho.Application.Features.Warehouse.Materials.SearchCommodities;
using NajaEcho.Domain.Commodities;
using Xunit;
using NajaEcho.Domain.Organizations;

namespace NajaEcho.Application.Tests.Features.Warehouse.Materials.AddMaterial;

public sealed class AddMaterialHandlerTests
{
    private static readonly Guid KnownCommodityId = Guid.NewGuid();
    private static readonly Guid KnownOwnerId = Guid.NewGuid();
    private static readonly Guid KnownRowId = Guid.NewGuid();

    private sealed class FakeMaterialRepo : IMaterialInventoryRepository
    {
        public bool NextIsNew { get; set; } = true;
        public decimal? CapturedQuantity { get; private set; }
        public int? CapturedQuality { get; private set; }
        private readonly MaterialRowDto _row;
        public FakeMaterialRepo() => _row = new(KnownRowId, KnownCommodityId, "Titanium", "TTAM", 1m, 500, KnownOwnerId, "Alice", "Bay 1");

        public Task<(MaterialRowDto Row, bool IsNew)> AddOrIncrementAsync(
            Guid commodityId, Guid ownerUserId, string location, decimal quantity, int quality, Guid? locationId, string? locationType, CancellationToken ct)
        {
            CapturedQuantity = quantity;
            CapturedQuality = quality;
            return Task.FromResult((_row with
            {
                CommodityId = commodityId,
                OwnerUserId = ownerUserId,
                Location = location,
                Quantity = quantity,
                Quality = quality,
            }, NextIsNew));
        }

        public Task<IReadOnlyList<MaterialRowDto>> GetMaterialsAsync(
            string? material, Guid? ownerUserId, string? location, int? qualityMin, int? qualityMax, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MaterialRowDto>>([]);

        public Task<MaterialFiltersDto> GetMaterialFiltersAsync(CancellationToken ct) =>
            Task.FromResult(new MaterialFiltersDto([], []));

        public Task<IReadOnlyList<CommodityResultDto>> SearchCommoditiesAsync(string? search, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CommodityResultDto>>([]);

        public Task<MaterialRowDto> UpdateQuantityAsync(Guid id, decimal quantity, CancellationToken ct) =>
            throw new NotImplementedException();


        public Task<MaterialRowDto> UpdateMaterialAsync(Guid id, Guid ownerUserId, Guid locationId, string locationType, decimal quantity, CancellationToken ct) => throw new NotImplementedException();
        public Task UpdateLocationAsync(Guid id, Guid locationId, string locationType, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct) => Task.FromResult(true);
        public Task RemoveAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeCommodityRepo : ICommodityRepository
    {
        public bool CommodityExists { get; set; } = true;

        public Task<Commodity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(CommodityExists ? (Commodity?)new Commodity
            {
                Id = id,
                Name = "Titanium",
                Code = "TTAM",
                Status = CommodityStatus.Active,
                RawData = JsonDocument.Parse("{}"),
                ImportedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            } : null);

        public Task<(IReadOnlyList<NajaEcho.Application.Features.Commodities.GetCommodities.CommodityListItem> Items, int TotalCount)> GetPagedAsync(
            int page, int pageSize, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<(int Inserted, int Updated, int Unchanged, int Restored, int SoftDeleted)> BulkUpsertAsync(
            IReadOnlyList<Commodity> incoming, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeUserRepo : IUserRepository
    {
        public bool UserExists { get; set; } = true;
        public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) => Task.FromResult(UserExists);
        public Task<IReadOnlyList<(Guid Id, string DisplayName)>> GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<(Guid, string)>>([]);
        public Task<IReadOnlyList<NajaEcho.Application.Features.Admin.Users.GetUsers.AdminUserDto>> GetUsersWithRolesAndCharactersAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<NajaEcho.Application.Features.Admin.Users.GetUsers.AdminUserDto>>([]);
        public Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct) => Task.CompletedTask;


        public Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class FakeStationRepo : ISpaceStationRepository
    {
        public Task<(int, int, int, int, int)> BulkUpsertAsync(IReadOnlyList<JsonDocument> records, IReadOnlyDictionary<int, Guid> starSystemMap, CancellationToken ct = default) =>
            Task.FromResult((0, 0, 0, 0, 0));
        public Task<IReadOnlyList<StationDto>> SearchActiveStationsAsync(string? search, int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StationDto>>([]);
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    private static AddMaterialHandler MakeHandler(
        FakeMaterialRepo? repo = null,
        FakeCommodityRepo? commodityRepo = null,
        FakeUserRepo? userRepo = null) =>
        new(
            repo ?? new FakeMaterialRepo(),
            commodityRepo ?? new FakeCommodityRepo(),
            userRepo ?? new FakeUserRepo(),
            NullLogger<AddMaterialHandler>.Instance);

    [Fact]
    public async Task HandleAsync_UnknownCommodity_ThrowsCommodityNotFoundException()
    {
        var commodityRepo = new FakeCommodityRepo { CommodityExists = false };
        var act = () => MakeHandler(commodityRepo: commodityRepo).HandleAsync(
            new AddMaterialCommand(KnownCommodityId, KnownOwnerId, "Bay 1", 1m), default);
        await act.Should().ThrowAsync<CommodityNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_UnknownOwner_ThrowsOwnerNotFoundException()
    {
        var userRepo = new FakeUserRepo { UserExists = false };
        var act = () => MakeHandler(userRepo: userRepo).HandleAsync(
            new AddMaterialCommand(KnownCommodityId, KnownOwnerId, "Bay 1", 1m), default);
        await act.Should().ThrowAsync<OwnerNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_QuantityRoundedHalfUp_BeforeValidation()
    {
        var repo = new FakeMaterialRepo();
        await MakeHandler(repo).HandleAsync(
            new AddMaterialCommand(KnownCommodityId, KnownOwnerId, "Bay 1", 1.0005m), default);
        repo.CapturedQuantity.Should().Be(1.001m);
    }

    [Fact]
    public async Task HandleAsync_QuantityRoundsToZero_RejectedAsZero()
    {
        var act = () => MakeHandler().HandleAsync(
            new AddMaterialCommand(KnownCommodityId, KnownOwnerId, "Bay 1", 0.0004m), default);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task HandleAsync_QuantityZeroOrLess_ThrowsArgumentOutOfRangeException()
    {
        var act = () => MakeHandler().HandleAsync(
            new AddMaterialCommand(KnownCommodityId, KnownOwnerId, "Bay 1", 0m), default);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task HandleAsync_QualityOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var act = () => MakeHandler().HandleAsync(
            new AddMaterialCommand(KnownCommodityId, KnownOwnerId, "Bay 1", 1m, 1001), default);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task HandleAsync_QualityDefaultsTo500_WhenOmitted()
    {
        var repo = new FakeMaterialRepo();
        await MakeHandler(repo).HandleAsync(
            new AddMaterialCommand(KnownCommodityId, KnownOwnerId, "Bay 1", 1m), default);
        repo.CapturedQuality.Should().Be(500);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_CallsAddOrIncrementAsync()
    {
        var repo = new FakeMaterialRepo();
        await MakeHandler(repo).HandleAsync(
            new AddMaterialCommand(KnownCommodityId, KnownOwnerId, "Bay 1", 2.5m, 700), default);
        repo.CapturedQuantity.Should().Be(2.50m);
        repo.CapturedQuality.Should().Be(700);
    }

    [Fact]
    public async Task HandleAsync_EmptyLocation_ThrowsArgumentException()
    {
        var act = () => MakeHandler().HandleAsync(
            new AddMaterialCommand(KnownCommodityId, KnownOwnerId, "", 1m), default);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
