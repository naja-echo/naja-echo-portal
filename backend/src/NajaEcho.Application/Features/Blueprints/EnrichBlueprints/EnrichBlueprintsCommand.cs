using System.Text.Json;

namespace NajaEcho.Application.Features.Blueprints.EnrichBlueprints;

public sealed record EnrichBlueprintsCommand(JsonElement Document);
