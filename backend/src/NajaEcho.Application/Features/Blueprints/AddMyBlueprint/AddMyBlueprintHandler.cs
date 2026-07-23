using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Blueprints.GetMyBlueprints;

namespace NajaEcho.Application.Features.Blueprints.AddMyBlueprint;

public sealed class AddMyBlueprintHandler(IUserBlueprintRepository repository)
{
    public Task<MyBlueprintListItemDto> HandleAsync(AddMyBlueprintCommand command, CancellationToken ct = default) =>
        repository.AddAsync(command.UserId, command.BlueprintId, ct);
}
