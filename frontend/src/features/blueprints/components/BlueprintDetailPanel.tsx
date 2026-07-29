import { useState } from 'react'
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Button } from '@/components/ui/button'
import { useGetBlueprintDetail } from '../hooks/useGetBlueprintDetail'
import { useRemoveMyBlueprint } from '../hooks/useRemoveMyBlueprint'

interface BlueprintDetailPanelProps {
  blueprintId: string | null
  onClose: () => void
}

function formatCraftTime(seconds: number | null | undefined): string {
  if (seconds == null) return '—'
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return `${m}m ${s}s`
}

export function BlueprintDetailPanel({ blueprintId, onClose }: BlueprintDetailPanelProps) {
  const [isConfirming, setIsConfirming] = useState(false)
  const { data, isLoading } = useGetBlueprintDetail(blueprintId)
  const { mutate: remove, isPending: isRemoving } = useRemoveMyBlueprint()

  function handleRemoveConfirm() {
    if (!blueprintId) return
    remove(blueprintId, {
      onSuccess: () => {
        setIsConfirming(false)
        onClose()
      },
    })
  }

  return (
    <Sheet open={blueprintId !== null} onOpenChange={(open) => { if (!open) { setIsConfirming(false); onClose() } }}>
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
                <p>{data.type ?? '—'}</p>
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
          </div>
        )}

        {/* Footer */}
        <div className="mt-auto pt-4 flex justify-end">
          {isConfirming ? (
            <div className="flex items-center gap-2">
              <span className="text-sm text-muted-foreground">Remove this blueprint?</span>
              <Button
                variant="destructive"
                size="sm"
                onClick={handleRemoveConfirm}
                disabled={isRemoving}
              >
                Confirm
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setIsConfirming(false)}
                disabled={isRemoving}
              >
                Cancel
              </Button>
            </div>
          ) : (
            <Button
              variant="destructive"
              size="sm"
              onClick={() => setIsConfirming(true)}
              disabled={isLoading || !data}
            >
              Remove
            </Button>
          )}
        </div>
      </SheetContent>
    </Sheet>
  )
}
