namespace NajaEcho.Domain.Blueprints;

public sealed class DuplicateBlueprintException(Guid blueprintId)
    : Exception($"Blueprint {blueprintId} is already in the user's personal list.");
