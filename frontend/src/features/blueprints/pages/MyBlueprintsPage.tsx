import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Plus } from 'lucide-react'
import { useMyBlueprints } from '../hooks/useMyBlueprints'
import { useBlueprintFilters } from '../hooks/useBlueprintFilters'
import { AddBlueprintDialog } from '../components/AddBlueprintDialog'
import { BlueprintDetailPanel } from '../components/BlueprintDetailPanel'
import { BlueprintFilters } from '../components/BlueprintFilters'

export function MyBlueprintsPage() {
  const [addOpen, setAddOpen] = useState(false)
  const [selectedBlueprintId, setSelectedBlueprintId] = useState<string | null>(null)
  const { data, isLoading } = useMyBlueprints()

  const blueprints = data?.blueprints ?? []
  const {
    filters,
    setName,
    setCategory,
    setSubcategory,
    categoryOptions,
    subcategoryOptions,
    filtered,
  } = useBlueprintFilters(blueprints)

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">My Blueprints</h1>
        <Button size="sm" onClick={() => setAddOpen(true)}>
          <Plus data-icon="inline-start" aria-hidden />
          Add Blueprint
        </Button>
      </div>

      {!isLoading && blueprints.length > 0 && (
        <BlueprintFilters
          name={filters.name}
          category={filters.category}
          subcategory={filters.subcategory}
          categoryOptions={categoryOptions}
          subcategoryOptions={subcategoryOptions}
          onNameChange={setName}
          onCategoryChange={setCategory}
          onSubcategoryChange={setSubcategory}
        />
      )}

      {isLoading ? (
        <p className="text-muted-foreground">Loading…</p>
      ) : blueprints.length === 0 ? (
        <p className="text-muted-foreground">
          You have no blueprints yet. Add your first one!
        </p>
      ) : filtered.length === 0 ? (
        <p className="text-muted-foreground">No blueprints match the current filters.</p>
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
              {filtered.map((bp) => (
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

      <AddBlueprintDialog open={addOpen} onClose={() => setAddOpen(false)} />
      <BlueprintDetailPanel
        blueprintId={selectedBlueprintId}
        onClose={() => setSelectedBlueprintId(null)}
      />
    </div>
  )
}
