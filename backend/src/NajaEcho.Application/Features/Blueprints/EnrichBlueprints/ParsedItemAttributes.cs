namespace NajaEcho.Application.Features.Blueprints.EnrichBlueprints;

public sealed record ParsedItemAttributes(
    Guid EntityClass,
    string? ComponentClass,
    short? ComponentSize,
    string? ComponentGrade);
