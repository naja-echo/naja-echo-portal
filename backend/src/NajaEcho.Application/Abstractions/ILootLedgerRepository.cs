using NajaEcho.Application.Features.Loot.GetDistribution;
using NajaEcho.Application.Features.Loot.GetMemberLedger;
using NajaEcho.Domain.Loot;

namespace NajaEcho.Application.Abstractions;

public interface ILootLedgerRepository
{
    Task AddEntryAsync(LootLedgerEntry entry, CancellationToken ct);
    Task<IReadOnlyList<DistributionRowDto>> GetDistributionAsync(CancellationToken ct);
    Task<MemberLedgerData?> GetMemberLedgerAsync(Guid memberId, CancellationToken ct);
}
