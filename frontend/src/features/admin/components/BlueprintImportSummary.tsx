import { Card, CardContent, CardDescription, CardTitle } from '@/components/ui/card'
import { Alert, AlertDescription } from '@/components/ui/alert'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import type {
  CollectionCounts,
  ImportBlueprintsResponse,
} from '../schemas/blueprintSchemas'

interface BlueprintImportSummaryProps {
  result: ImportBlueprintsResponse
}

const COLLECTIONS: { key: keyof Pick<ImportBlueprintsResponse, 'blueprints' | 'resources' | 'items' | 'properties'>; label: string }[] = [
  { key: 'blueprints', label: 'Blueprints' },
  { key: 'resources', label: 'Resources' },
  { key: 'items', label: 'Items' },
  { key: 'properties', label: 'Properties' },
]

export function BlueprintImportSummary({ result }: BlueprintImportSummaryProps) {
  return (
    <div className="flex flex-col gap-4" role="status" aria-label="Import result summary">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm text-muted-foreground">Dataset version</span>
        <span className="text-sm font-semibold">{result.version}</span>
        {result.referenceDataReplaced && (
          <span className="text-xs rounded bg-muted px-2 py-0.5 text-muted-foreground">
            Reference data replaced
          </span>
        )}
      </div>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {COLLECTIONS.map(({ key, label }) => (
          <CountCard key={key} label={label} counts={result[key]} />
        ))}
      </div>

      {result.warnings.length > 0 && (
        <Alert role="alert">
          <AlertDescription>
            <p className="mb-1 font-medium">Warnings ({result.warnings.length})</p>
            <ul className="list-inside list-disc text-xs">
              {result.warnings.map((w, i) => (
                <li key={i}>{w}</li>
              ))}
            </ul>
          </AlertDescription>
        </Alert>
      )}

      {result.rejections.length > 0 && (
        <div>
          <p className="mb-1 text-sm font-medium">Rejected entries ({result.rejections.length})</p>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Guid</TableHead>
                <TableHead>Product name</TableHead>
                <TableHead>Reason</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {result.rejections.map((r, i) => (
                <TableRow key={`${r.guid ?? 'unknown'}-${i}`}>
                  <TableCell className="font-mono text-xs">{r.guid ?? '—'}</TableCell>
                  <TableCell>{r.productName ?? '—'}</TableCell>
                  <TableCell>{r.reason}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  )
}

function CountCard({ label, counts }: { label: string; counts: CollectionCounts }) {
  return (
    <Card>
      <CardContent className="p-3">
        <CardTitle className="text-sm">{label}</CardTitle>
        <CardDescription className="mt-1 flex flex-col gap-0.5 text-xs">
          <span>Read: {counts.read}</span>
          <span>Inserted: {counts.inserted}</span>
          <span>Updated: {counts.updated}</span>
          <span>Rejected: {counts.rejected}</span>
        </CardDescription>
      </CardContent>
    </Card>
  )
}
