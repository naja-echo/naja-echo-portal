using FluentAssertions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Blueprints.GetBlueprints;
using NajaEcho.Application.Features.Blueprints.ImportBlueprints;
using NajaEcho.Application.Features.Blueprints.SearchBlueprints;
using Xunit;

namespace NajaEcho.Application.Tests.Features.Blueprints;

public class SearchBlueprintsHandlerTests
{
    private sealed class FakeSearchBlueprintRepository : IBlueprintRepository
    {
        private readonly List<BlueprintSearchResultDto> _results;

        public FakeSearchBlueprintRepository(IEnumerable<BlueprintSearchResultDto>? results = null)
        {
            _results = results?.ToList() ?? [];
        }

        public Task<IReadOnlyList<BlueprintSearchResultDto>> SearchAsync(string term, int limit = 20, CancellationToken ct = default)
        {
            var filtered = _results
                .Where(r => r.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<BlueprintSearchResultDto>>(filtered);
        }

        public Task<IReadOnlyDictionary<string, MaterialMatch>> ResolveMaterialsAsync(
            IReadOnlyCollection<string> names, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, MaterialMatch>>(new Dictionary<string, MaterialMatch>());

        public Task<BlueprintImportCounts> ImportAsync(ParsedBlueprintDataset dataset, CancellationToken ct = default) =>
            Task.FromResult(new BlueprintImportCounts(0, 0));

        public Task<IReadOnlyList<BlueprintListItemDto>> GetListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BlueprintListItemDto>>([]);
    }

    [Fact]
    public async Task Handle_ReturnsMatchingResults_CaseInsensitive()
    {
        var repo = new FakeSearchBlueprintRepository([
            new(Guid.NewGuid(), "Widget Mk1", "Weapon"),
            new(Guid.NewGuid(), "WIDGET Mk2", null),
            new(Guid.NewGuid(), "Unrelated Thing", "Tool"),
        ]);
        var handler = new SearchBlueprintsHandler(repo);

        var result = await handler.HandleAsync(new SearchBlueprintsQuery("widget"));

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r => r.ProductName.Should().ContainEquivalentOf("widget"));
    }

    [Fact]
    public async Task Handle_NoMatches_ReturnsEmptyList()
    {
        var repo = new FakeSearchBlueprintRepository([new(Guid.NewGuid(), "Widget", "Weapon")]);
        var handler = new SearchBlueprintsHandler(repo);

        var result = await handler.HandleAsync(new SearchBlueprintsQuery("xyznotexist"));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsAtMost20Results()
    {
        var many = Enumerable.Range(1, 25)
            .Select(i => new BlueprintSearchResultDto(Guid.NewGuid(), $"Widget {i:D2}", null))
            .ToList();
        var repo = new FakeSearchBlueprintRepository(many);
        var handler = new SearchBlueprintsHandler(repo);

        var result = await handler.HandleAsync(new SearchBlueprintsQuery("widget"));

        result.Should().HaveCount(20);
    }
}
