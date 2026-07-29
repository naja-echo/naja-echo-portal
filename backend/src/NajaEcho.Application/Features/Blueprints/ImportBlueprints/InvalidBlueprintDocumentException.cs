namespace NajaEcho.Application.Features.Blueprints.ImportBlueprints;

/// <summary>
/// Thrown when the uploaded document's top-level structure is invalid or missing a required
/// section (FR-004). The endpoint maps this to 400 with nothing stored.
/// </summary>
public sealed class InvalidBlueprintDocumentException(string reason)
    : Exception(reason);
