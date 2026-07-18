import { useMyLoot } from '../hooks/useMyLoot'
import { LedgerTable } from '../components/LedgerTable'
import { ClaimPriorityBadge } from '../components/ClaimPriorityBadge'

export function MyLootPage() {
  const { data, isLoading, isError } = useMyLoot()

  if (isLoading) return <div className="p-6">Loading...</div>
  if (isError || !data) return <div className="p-6 text-destructive">Failed to load loot data.</div>

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">My Loot</h1>
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          Claim Priority: <ClaimPriorityBadge value={data.claimPriority} />
        </div>
      </div>

      <section className="space-y-2">
        <h2 className="text-lg font-semibold">Org Points</h2>
        <p className="text-sm text-muted-foreground">Total: {data.orgPointsTotal}</p>
        <LedgerTable entries={data.orgPoints} emptyMessage="No OrgPoints entries yet." />
      </section>

      <section className="space-y-2">
        <h2 className="text-lg font-semibold">Loot Points</h2>
        <p className="text-sm text-muted-foreground">Total: {data.lootPointsTotal}</p>
        <LedgerTable entries={data.lootPoints} emptyMessage="No LootPoints entries yet." />
      </section>
    </div>
  )
}
