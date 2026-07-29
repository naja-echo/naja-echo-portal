namespace NajaEcho.Domain.Blueprints;

public sealed class BlueprintNotFoundException(Guid blueprintId)
    : Exception($"Blueprint {blueprintId} was not found in the catalog.");
