import { useMemo, useState } from 'react'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import type { BlueprintListItem } from '../schemas/blueprintSchemas'

interface BlueprintsListProps {
  blueprints: BlueprintListItem[]
}

export function BlueprintsList({ blueprints }: BlueprintsListProps) {
  const [search, setSearch] = useState('')

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase()
    if (!term) return blueprints
    return blueprints.filter((b) => b.displayName.toLowerCase().includes(term))
  }, [blueprints, search])

  if (blueprints.length === 0) {
    return (
      <p className="rounded border border-dashed p-6 text-center text-sm text-muted-foreground">
        No blueprints have been imported yet. Upload a dataset file above to get started.
      </p>
    )
  }

  return (
    <div className="flex flex-col gap-3">
      <input
        type="text"
        placeholder="Search blueprints by name…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="w-full max-w-sm rounded border bg-background px-2 py-1 text-sm"
        aria-label="Search blueprints by name"
      />

      {filtered.length === 0 ? (
        <p className="rounded border border-dashed p-6 text-center text-sm text-muted-foreground">
          No blueprints match “{search}”.
        </p>
      ) : (
        <div className="rounded border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Manufacturer</TableHead>
                <TableHead>Tag</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.map((b) => (
                <TableRow key={b.guid}>
                  <TableCell className="font-medium">{b.displayName}</TableCell>
                  <TableCell className="text-muted-foreground">{b.manufacturer ?? '—'}</TableCell>
                  <TableCell className="text-muted-foreground">{b.tag ?? '—'}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <p className="text-xs text-muted-foreground">
        {filtered.length} of {blueprints.length} {blueprints.length === 1 ? 'blueprint' : 'blueprints'}
      </p>
    </div>
  )
}
