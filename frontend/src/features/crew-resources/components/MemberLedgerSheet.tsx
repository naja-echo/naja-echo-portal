import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { useMemberLedger } from '../hooks/useMemberLedger'
import { LedgerTable } from './LedgerTable'
import { ClaimPriorityBadge } from './ClaimPriorityBadge'

interface MemberLedgerSheetProps {
  memberId: string
  memberName: string
  open: boolean
  onClose: () => void
}

export function MemberLedgerSheet({ memberId, memberName, open, onClose }: MemberLedgerSheetProps) {
  const { data: ledger, isLoading } = useMemberLedger(memberId)

  return (
    <Sheet open={open} onOpenChange={(o) => !o && onClose()}>
      <SheetContent className="w-full sm:max-w-xl overflow-y-auto">
        <SheetHeader>
          <SheetTitle>{memberName}</SheetTitle>
        </SheetHeader>

        <div className="mt-4 space-y-6">
          {isLoading ? (
            <p className="text-muted-foreground">Loading...</p>
          ) : ledger ? (
            <>
              <div className="flex items-center gap-2">
                <span className="text-sm text-muted-foreground">Claim Priority:</span>
                <ClaimPriorityBadge value={ledger.claimPriority} />
              </div>

              <section className="space-y-2">
                <h3 className="text-sm font-semibold">Org Points (Total: {ledger.orgPointsTotal})</h3>
                <LedgerTable entries={ledger.orgPoints} emptyMessage="No OrgPoints entries." />
              </section>

              <section className="space-y-2">
                <h3 className="text-sm font-semibold">Loot Points (Total: {ledger.lootPointsTotal})</h3>
                <LedgerTable entries={ledger.lootPoints} emptyMessage="No LootPoints entries." />
              </section>
            </>
          ) : (
            <p className="text-destructive">Failed to load ledger.</p>
          )}
        </div>
      </SheetContent>
    </Sheet>
  )
}
