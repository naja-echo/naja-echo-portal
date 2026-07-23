using NajaEcho.Application.Abstractions;

namespace NajaEcho.Application.Features.Blueprints.RemoveMyBlueprint;

public sealed class RemoveMyBlueprintHandler(IUserBlueprintRepository repository)
{
    public Task<bool> HandleAsync(RemoveMyBlueprintCommand command, CancellationToken ct = default) =>
        repository.RemoveAsync(command.UserId, command.BlueprintId, ct);
}
