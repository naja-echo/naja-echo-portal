import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { LedgerEntry } from '../schemas/lootSchemas'

interface LedgerTableProps {
  entries: LedgerEntry[]
  emptyMessage?: string
}

export function LedgerTable({ entries, emptyMessage = 'No entries yet.' }: LedgerTableProps) {
  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-24">Amount</TableHead>
            <TableHead>Reason</TableHead>
            <TableHead className="w-40">Posted By</TableHead>
            <TableHead className="w-40">Date</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {entries.length === 0 ? (
            <TableRow>
              <TableCell colSpan={4} className="text-center text-muted-foreground py-6">
                {emptyMessage}
              </TableCell>
            </TableRow>
          ) : (
            entries.map((entry) => (
              <TableRow key={entry.id}>
                <TableCell className={entry.amount < 0 ? 'text-destructive' : 'text-green-600 dark:text-green-400'}>
                  {entry.amount > 0 ? `+${entry.amount}` : entry.amount}
                </TableCell>
                <TableCell>{entry.reason}</TableCell>
                <TableCell>{entry.postedBy}</TableCell>
                <TableCell>{new Date(entry.createdAt).toLocaleDateString()}</TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </div>
  )
}
