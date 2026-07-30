import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { useGetOrgBlueprintDetail } from '../hooks/useGetOrgBlueprintDetail'
import { blueprintLabel } from '../config/blueprintLabels'

interface OrgBlueprintDetailPanelProps {
  blueprintId: string | null
  onClose: () => void
}

function formatCraftTime(seconds: number | null | undefined): string {
  if (seconds == null) return '—'
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return `${m}m ${s}s`
}

export function OrgBlueprintDetailPanel({ blueprintId, onClose }: OrgBlueprintDetailPanelProps) {
  const { data, isLoading } = useGetOrgBlueprintDetail(blueprintId)

  return (
    <Sheet open={blueprintId !== null} onOpenChange={(open) => { if (!open) onClose() }}>
      <SheetContent side="right" className="flex flex-col w-116 max-w-full">
        <SheetHeader>
          <SheetTitle>
            {isLoading ? 'Loading…' : (data?.productName ?? '—')}
          </SheetTitle>
        </SheetHeader>

        {!isLoading && data && (
          <div className="flex flex-col gap-6 flex-1 overflow-y-auto">
            <hr className="border-border" />

            {/* Summary row */}
            <div className="grid grid-cols-3 gap-4 text-sm">
              <div>
                <p className="text-muted-foreground font-medium">Type</p>
                <p>{data.type ? blueprintLabel(data.type) : '—'}</p>
              </div>
              <div>
                <p className="text-muted-foreground font-medium">Craft Time</p>
                <p>{formatCraftTime(data.craftTimeSeconds)}</p>
              </div>
              <div>
                <p className="text-muted-foreground font-medium">Ingredients</p>
                <p>{data.ingredientCount}</p>
              </div>
            </div>

            {/* Ingredient listing */}
            <div>
              <h3 className="text-sm font-semibold mb-2">Ingredients</h3>
              <hr className="border-border mb-2" />
              {data.slots.length === 0 ? (
                <p className="text-sm text-muted-foreground">No ingredients listed.</p>
              ) : (
                <div className="space-y-3 text-sm">
                  {data.slots.map((slot) => (
                    <div key={slot.slotIndex}>
                      <p className="font-medium">{slot.slotName}</p>
                      {slot.options.map((opt) => (
                        <p key={opt.optionIndex} className="text-muted-foreground">
                          {opt.materialName} &mdash; {opt.quantity}
                        </p>
                      ))}
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Owners section */}
            <div>
              <hr className="border-border mb-4" />
              <h3 className="text-sm font-semibold mb-2">Who has this blueprint?</h3>
              {data.owners.length === 0 ? (
                <p className="text-sm text-muted-foreground">No members currently have this blueprint.</p>
              ) : (
                <div className="space-y-1">
                  {data.owners.map((owner) => (
                    <p key={owner.userId} className="text-sm text-muted-foreground">
                      {owner.displayName}
                    </p>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}
      </SheetContent>
    </Sheet>
  )
}
