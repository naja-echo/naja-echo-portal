using NajaEcho.Application.Abstractions;

namespace NajaEcho.Application.Features.Blueprints.EnrichBlueprints;

public sealed class EnrichBlueprintsHandler(IBlueprintRepository repository)
{
    public async Task<EnrichBlueprintsResult> HandleAsync(EnrichBlueprintsCommand command, CancellationToken ct = default)
    {
        var items = CraftingItemsParser.Parse(command.Document);
        var updated = await repository.EnrichAsync(items, ct);
        return new EnrichBlueprintsResult(items.Count, updated);
    }
}
