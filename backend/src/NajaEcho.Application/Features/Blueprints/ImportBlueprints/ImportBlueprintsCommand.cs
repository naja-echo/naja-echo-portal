using System.Text.Json;

namespace NajaEcho.Application.Features.Blueprints.ImportBlueprints;

/// <summary>The uploaded dataset document (the parsed JSON body of the file).</summary>
public sealed record ImportBlueprintsCommand(JsonElement Document);
