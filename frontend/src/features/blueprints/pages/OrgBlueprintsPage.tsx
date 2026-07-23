import { useState } from 'react'
import { useOrgBlueprints } from '../hooks/useOrgBlueprints'
import { OrgBlueprintDetailPanel } from '../components/OrgBlueprintDetailPanel'

export function OrgBlueprintsPage() {
  const [selectedBlueprintId, setSelectedBlueprintId] = useState<string | null>(null)
  const { data, isLoading } = useOrgBlueprints()

  const blueprints = data?.blueprints ?? []

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Org Blueprints</h1>
      </div>

      {isLoading ? (
        <p className="text-muted-foreground">Loading…</p>
      ) : blueprints.length === 0 ? (
        <p className="text-muted-foreground">No blueprints found in your org.</p>
      ) : (
        <div className="rounded-md border">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-muted/50">
                <th className="px-4 py-3 text-left font-medium">Blueprint</th>
                <th className="px-4 py-3 text-left font-medium">Type</th>
                <th className="px-4 py-3 text-left font-medium">Ingredients</th>
              </tr>
            </thead>
            <tbody>
              {blueprints.map((bp) => (
                <tr
                  key={bp.blueprintId}
                  className="border-b last:border-0 cursor-pointer hover:bg-muted/50"
                  onClick={() => setSelectedBlueprintId(bp.blueprintId)}
                >
                  <td className="px-4 py-3">{bp.productName ?? '—'}</td>
                  <td className="px-4 py-3 text-muted-foreground">{bp.type ?? '—'}</td>
                  <td className="px-4 py-3">{bp.ingredientCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <OrgBlueprintDetailPanel
        blueprintId={selectedBlueprintId}
        onClose={() => setSelectedBlueprintId(null)}
      />
    </div>
  )
}
