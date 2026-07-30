import { useState } from 'react'
import { useOrgBlueprints } from '../hooks/useOrgBlueprints'
import { useBlueprintFilters } from '../hooks/useBlueprintFilters'
import { OrgBlueprintDetailPanel } from '../components/OrgBlueprintDetailPanel'
import { BlueprintFilters } from '../components/BlueprintFilters'
import { getBlueprintColumns } from '../config/blueprintColumns'

export function OrgBlueprintsPage() {
  const [selected, setSelected] = useState<{ blueprintId: string; subtype: string | null; tag: string | null } | null>(null)
  const { data, isLoading } = useOrgBlueprints()

  const blueprints = data?.blueprints ?? []
  const {
    filters,
    setName,
    setCategory,
    setSubcategory,
    setItemType,
    setComponentClass,
    setComponentSize,
    setComponentGrade,
    categoryOptions,
    subcategoryOptions,
    itemTypeOptions,
    componentClassOptions,
    componentSizeOptions,
    componentGradeOptions,
    filtered,
  } = useBlueprintFilters(blueprints)

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Org Blueprints</h1>
      </div>

      {!isLoading && blueprints.length > 0 && (
        <BlueprintFilters
          name={filters.name}
          category={filters.category}
          subcategory={filters.subcategory}
          itemType={filters.itemType}
          componentClass={filters.componentClass}
          componentSize={filters.componentSize}
          componentGrade={filters.componentGrade}
          categoryOptions={categoryOptions}
          subcategoryOptions={subcategoryOptions}
          itemTypeOptions={itemTypeOptions}
          componentClassOptions={componentClassOptions}
          componentSizeOptions={componentSizeOptions}
          componentGradeOptions={componentGradeOptions}
          onNameChange={setName}
          onCategoryChange={setCategory}
          onSubcategoryChange={setSubcategory}
          onItemTypeChange={setItemType}
          onComponentClassChange={setComponentClass}
          onComponentSizeChange={setComponentSize}
          onComponentGradeChange={setComponentGrade}
        />
      )}

      {isLoading ? (
        <p className="text-muted-foreground">Loading…</p>
      ) : blueprints.length === 0 ? (
        <p className="text-muted-foreground">No blueprints found in your org.</p>
      ) : filtered.length === 0 ? (
        <p className="text-muted-foreground">No blueprints match the current filters.</p>
      ) : (
        <div className="rounded-md border">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-muted/50">
                {getBlueprintColumns(filters.category, filters.subcategory).map(col => (
                  <th key={col.header} className="px-4 py-3 text-left font-medium">{col.header}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {filtered.map((bp) => (
                <tr
                  key={bp.blueprintId}
                  className="border-b last:border-0 cursor-pointer hover:bg-muted/50"
                  onClick={() => setSelected({ blueprintId: bp.blueprintId, subtype: bp.subtype, tag: bp.tag })}
                >
                  {getBlueprintColumns(filters.category, filters.subcategory).map(col => (
                    <td key={col.header} className={`px-4 py-3${col.className ? ` ${col.className}` : ''}`}>
                      {col.render(bp)}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <OrgBlueprintDetailPanel
        blueprintId={selected?.blueprintId ?? null}
        subtype={selected?.subtype ?? null}
        tag={selected?.tag ?? null}
        onClose={() => setSelected(null)}
      />
    </div>
  )
}
