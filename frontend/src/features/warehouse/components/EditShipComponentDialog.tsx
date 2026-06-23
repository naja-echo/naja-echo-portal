import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { LocationCombobox } from './LocationCombobox'
import { useInventoryFilters } from '../hooks/useInventoryFilters'
import { useUpdateInventoryItem } from '../hooks/useUpdateInventoryItem'
import type { ShipComponentRow } from '../schemas/shipComponentSchemas'
import type { LocationOption } from '../schemas/locationSchemas'

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  row: ShipComponentRow | null
  onSuccess?: () => void
}

export function EditShipComponentDialog({ open, onOpenChange, row, onSuccess }: Props) {
  const [ownerUserId, setOwnerUserId] = useState<string>(row?.ownerUserId ?? '')
  const [location, setLocation] = useState<LocationOption | undefined>(
    row?.locationId && row?.locationType
      ? { id: row.locationId, name: row.location, type: row.locationType as 'Station' | 'City' }
      : undefined
  )
  const [quantity, setQuantity] = useState<string>(row ? String(row.quantity) : '')

  const filters = useInventoryFilters()
  const mutation = useUpdateInventoryItem()

  const handleConfirm = async () => {
    if (!row || !ownerUserId || !location || !quantity) return

    await mutation.mutateAsync({
      id: row.id,
      ownerUserId,
      locationId: location.id,
      locationType: location.type,
      quantity: Number(quantity),
    })
    onSuccess?.()
    onOpenChange(false)
  }

  const quantityNum = Number(quantity)
  const isValid = ownerUserId && location && quantityNum > 0

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Edit Ship Component</DialogTitle>
        </DialogHeader>

        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <label htmlFor="edit-sc-owner" className="text-sm font-medium">
              Owner
            </label>
            <Select value={ownerUserId} onValueChange={setOwnerUserId} disabled={mutation.isPending}>
              <SelectTrigger id="edit-sc-owner">
                <SelectValue placeholder="Select owner" />
              </SelectTrigger>
              <SelectContent>
                {filters.data?.owners.map((owner) => (
                  <SelectItem key={owner.userId} value={owner.userId}>
                    {owner.displayName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="flex flex-col gap-2">
            <label htmlFor="edit-sc-location" className="text-sm font-medium">
              Location
            </label>
            <LocationCombobox
              value={location?.id}
              onValueChange={(loc) => setLocation(loc ?? undefined)}
              disabled={mutation.isPending}
              aria-label="Location"
            />
          </div>

          <div className="flex flex-col gap-2">
            <label htmlFor="edit-sc-quantity" className="text-sm font-medium">
              Quantity
            </label>
            <Input
              id="edit-sc-quantity"
              type="number"
              min="1"
              value={quantity}
              onChange={(e) => setQuantity(e.target.value)}
              disabled={mutation.isPending}
              placeholder="1"
            />
          </div>

          {mutation.error && (
            <p className="text-sm text-destructive">
              Update failed: {mutation.error instanceof Error ? mutation.error.message : 'Unknown error'}
            </p>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={mutation.isPending}>
            Cancel
          </Button>
          <Button onClick={() => void handleConfirm()} disabled={!isValid || mutation.isPending}>
            {mutation.isPending ? 'Saving…' : 'Confirm'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
