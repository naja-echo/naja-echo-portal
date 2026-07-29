import { useState } from 'react'
import { Eye } from 'lucide-react'
import { useDistribution } from '../hooks/useDistribution'
import { MemberLedgerSheet } from '../components/MemberLedgerSheet'
import { ClaimPriorityBadge } from '../components/ClaimPriorityBadge'
import { AddPointsDialog } from '../components/AddPointsDialog'
import { AwardLootDialog } from '../components/AwardLootDialog'
import { useHasRole } from '@/features/auth/hooks/useHasRole'
import { ROLES } from '@/features/auth/lib/roles'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import type { DistributionRow } from '../schemas/lootSchemas'

export function LootDistributionPage() {
  const { data, isLoading, isError } = useDistribution()
  const [selectedMember, setSelectedMember] = useState<DistributionRow | null>(null)
  const [showAddPoints, setShowAddPoints] = useState(false)
  const [showAwardLoot, setShowAwardLoot] = useState(false)

  const isCroOrAdmin = useHasRole([ROLES.CrewResourceOfficer])
  const isQmOrAdmin = useHasRole([ROLES.Quartermaster])

  if (isLoading) return <div className="p-6">Loading...</div>
  if (isError || !data) return <div className="p-6 text-destructive">Failed to load distribution data.</div>

  const sorted = [...data.members].sort((a, b) => a.claimPriority - b.claimPriority)

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Loot Distribution</h1>
        <div className="flex gap-2">
          {isCroOrAdmin && (
            <Button size="sm" onClick={() => setShowAddPoints(true)}>
              Add Points
            </Button>
          )}
          {isQmOrAdmin && (
            <Button size="sm" variant="outline" onClick={() => setShowAwardLoot(true)}>
              Award Loot
            </Button>
          )}
        </div>
      </div>

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Member</TableHead>
              <TableHead className="w-32 text-right">Org Points</TableHead>
              <TableHead className="w-32 text-right">Loot Points</TableHead>
              <TableHead className="w-40 text-right">Claim Priority</TableHead>
              <TableHead className="w-24"></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {sorted.map((member) => (
              <TableRow key={member.memberId}>
                <TableCell>{member.displayName}</TableCell>
                <TableCell className="text-right">{member.orgPointsTotal}</TableCell>
                <TableCell className="text-right">{member.lootPointsTotal}</TableCell>
                <TableCell className="text-right">
                  <ClaimPriorityBadge value={member.claimPriority} />
                </TableCell>
                <TableCell>
                  <Button
                    size="icon"
                    variant="ghost"
                    aria-label="View ledger"
                    onClick={() => setSelectedMember(member)}
                  >
                    <Eye className="h-4 w-4" />
                  </Button>
                </TableCell>
              </TableRow>
            ))}
            {sorted.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} className="text-center text-muted-foreground py-6">
                  No members found.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      {selectedMember && (
        <MemberLedgerSheet
          memberId={selectedMember.memberId}
          memberName={selectedMember.displayName}
          open={!!selectedMember}
          onClose={() => setSelectedMember(null)}
        />
      )}

      {showAddPoints && (
        <AddPointsDialog
          open={showAddPoints}
          onClose={() => setShowAddPoints(false)}
          members={data.members}
        />
      )}
      {showAwardLoot && (
        <AwardLootDialog
          open={showAwardLoot}
          onClose={() => setShowAwardLoot(false)}
          members={data.members}
        />
      )}
    </div>
  )
}
