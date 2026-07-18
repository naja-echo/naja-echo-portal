using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Blueprints.GetBlueprints;
using NajaEcho.Application.Features.Blueprints.ImportBlueprints;
using NajaEcho.Application.Features.Ships.ImportShips;
using NajaEcho.Domain.Blueprints;
using Xunit;

namespace NajaEcho.Application.Tests.Features.Blueprints;

public class ImportBlueprintsHandlerTests
{
    private static ImportBlueprintsHandler Handler(
        FakeBlueprintRepository repo, FakeCoordinator coordinator) =>
        new(repo, coordinator, NullLogger<ImportBlueprintsHandler>.Instance);

    private static ImportBlueprintsCommand Command(string json) =>
        new(BlueprintFixtures.Doc(json));

    [Fact]
    public async Task Handle_CoordinatorBusy_ThrowsAndDoesNotImport()
    {
        var repo = new FakeBlueprintRepository();
        var coordinator = new FakeCoordinator { Held = true };

        var act = () => Handler(repo, coordinator).HandleAsync(Command(BlueprintFixtures.ValidDataset()));

        await act.Should().ThrowAsync<ImportAlreadyInProgressException>();
        repo.ImportCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_InvalidDocument_ThrowsBeforeAnyRepositoryWrite()
    {
        var repo = new FakeBlueprintRepository();
        var coordinator = new FakeCoordinator();

        var act = () => Handler(repo, coordinator).HandleAsync(Command("{ \"version\": \"1.0.0\" }"));

        await act.Should().ThrowAsync<InvalidBlueprintDocumentException>();
        repo.ImportCalled.Should().BeFalse();
        coordinator.Held.Should().BeFalse("the lock must be released after a failed import");
    }

    [Fact]
    public async Task Handle_ValidDataset_ImportsAndReportsCounts()
    {
        var repo = new FakeBlueprintRepository { Counts = new BlueprintImportCounts(1, 0) };
        var result = await Handler(repo, new FakeCoordinator()).HandleAsync(Command(BlueprintFixtures.ValidDataset()));

        repo.ImportCalled.Should().BeTrue();
        result.Version.Should().Be("1.4.0");
        result.Blueprints.Read.Should().Be(1);
        result.Blueprints.Inserted.Should().Be(1);
        result.Blueprints.Rejected.Should().Be(0);
        result.Resources.Read.Should().Be(2);
        result.Items.Read.Should().Be(1);
        result.ReferenceDataReplaced.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UnmatchedMaterial_ProducesWarningAndNullLink()
    {
        var repo = new FakeBlueprintRepository(); // resolves nothing
        var result = await Handler(repo, new FakeCoordinator()).HandleAsync(Command(BlueprintFixtures.ValidDataset()));

        result.Warnings.Should().Contain(w => w.Contains("Steel") && w.Contains("unlinked"));
        repo.LastDataset!.Materials.Should().OnlyContain(m => m.MatchedUexId == null);
    }

    [Fact]
    public async Task Handle_MatchedMaterial_AppliesResolutionToMaterialsAndOptions()
    {
        var repo = new FakeBlueprintRepository();
        repo.Map["steel"] = new MaterialMatch(4242);

        var result = await Handler(repo, new FakeCoordinator()).HandleAsync(Command(BlueprintFixtures.ValidDataset()));

        repo.LastDataset!.Materials.Should().Contain(m => m.Name == "Steel" && m.MatchedUexId == 4242);

        var steelOption = repo.LastDataset.Blueprints
            .SelectMany(b => b.Tiers)
            .SelectMany(t => t.Options)
            .Single(o => o.MaterialName == "Steel");
        steelOption.MatchedUexId.Should().Be(4242);

        result.Warnings.Should().NotContain(w => w.Contains("Steel"));
    }

    [Fact]
    public async Task Handle_EmptyBlueprintList_StillRefreshesReferenceData()
    {
        const string json = """
        {
          "version": "1.4.0",
          "meta": { "totalBlueprints": 0, "totalProducts": 0, "totalResources": 2, "totalItems": 0 },
          "dismantle": { "efficiency": 0.5, "dismantleTimeSeconds": 60, "blacklistedResources": [], "blacklistedEntityClasses": [] },
          "properties": {},
          "resources": ["Steel", "Titanium"],
          "items": [],
          "blueprints": []
        }
        """;

        var repo = new FakeBlueprintRepository();
        var result = await Handler(repo, new FakeCoordinator()).HandleAsync(Command(json));

        repo.ImportCalled.Should().BeTrue();
        result.Blueprints.Read.Should().Be(0);
        result.Resources.Inserted.Should().Be(2);
        result.ReferenceDataReplaced.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MetaMismatch_ProducesWarningNotFailure()
    {
        var repo = new FakeBlueprintRepository();
        var result = await Handler(repo, new FakeCoordinator())
            .HandleAsync(Command(BlueprintFixtures.ValidDataset(metaTotalBlueprints: 99)));

        repo.ImportCalled.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("meta.totalBlueprints"));
    }

    private sealed class FakeCoordinator : IImportCoordinator
    {
        public bool Held { get; set; }

        public bool TryAcquire()
        {
            if (Held)
            {
                return false;
            }

            Held = true;
            return true;
        }

        public void Release() => Held = false;
    }

    private sealed class FakeBlueprintRepository : IBlueprintRepository
    {
        public bool ImportCalled { get; private set; }
        public ParsedBlueprintDataset? LastDataset { get; private set; }
        public BlueprintImportCounts Counts { get; set; } = new(0, 0);
        public Dictionary<string, MaterialMatch> Map { get; } = new();

        public Task<IReadOnlyDictionary<string, MaterialMatch>> ResolveMaterialsAsync(
            IReadOnlyCollection<string> names, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, MaterialMatch>>(Map);

        public Task<BlueprintImportCounts> ImportAsync(ParsedBlueprintDataset dataset, CancellationToken ct = default)
        {
            ImportCalled = true;
            LastDataset = dataset;
            return Task.FromResult(Counts);
        }

        public Task<IReadOnlyList<BlueprintListItemDto>> GetListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BlueprintListItemDto>>([]);
    }
}
