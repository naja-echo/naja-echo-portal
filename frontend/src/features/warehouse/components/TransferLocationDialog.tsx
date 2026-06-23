import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { LocationCombobox } from './LocationCombobox'
import { useTransferItemLocation } from '../hooks/useTransferItemLocation'
import { useTransferMaterialLocation } from '../hooks/useTransferMaterialLocation'
import { useLastTransferLocation } from '../hooks/useLastTransferLocation'
import type { LocationOption } from '../schemas/locationSchemas'

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  rowId: string
  entityType: 'item' | 'material'
  onSuccess?: () => void
}

export function TransferLocationDialog({ open, onOpenChange, rowId, entityType, onSuccess }: Props) {
  const { lastLocation, setLastLocation } = useLastTransferLocation()
  const [selectedLocation, setSelectedLocation] = useState<LocationOption | undefined>(lastLocation)

  const transferItem = useTransferItemLocation()
  const transferMaterial = useTransferMaterialLocation()

  const mutation = entityType === 'item' ? transferItem : transferMaterial
  const isPending = mutation.isPending
  const mutationError = mutation.error

  const handleConfirm = async () => {
    if (!selectedLocation) return

    await mutation.mutateAsync({ id: rowId, locationId: selectedLocation.id, locationType: selectedLocation.type })
    setLastLocation(selectedLocation)
    onSuccess?.()
    onOpenChange(false)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Transfer to Location</DialogTitle>
          <DialogDescription>
            Select a location to transfer this {entityType === 'item' ? 'item' : 'material'} to.
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-3">
          <LocationCombobox
            value={selectedLocation?.id}
            onValueChange={(loc) => setSelectedLocation(loc ?? undefined)}
            disabled={isPending}
          />
          {mutationError && (
            <p className="text-sm text-destructive">
              Transfer failed: {mutationError instanceof Error ? mutationError.message : 'Unknown error'}
            </p>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
            Cancel
          </Button>
          <Button onClick={() => void handleConfirm()} disabled={!selectedLocation || isPending}>
            {isPending ? 'Transferring…' : 'Confirm Transfer'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
