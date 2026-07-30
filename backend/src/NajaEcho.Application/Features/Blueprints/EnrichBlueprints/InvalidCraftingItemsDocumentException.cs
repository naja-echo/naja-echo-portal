namespace NajaEcho.Application.Features.Blueprints.EnrichBlueprints;

public sealed class InvalidCraftingItemsDocumentException(string message) : Exception(message);
