import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogFooter, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { useCatalogItemSearch } from '../hooks/useCatalogItemSearch'
import { useAddInventoryItem } from '../hooks/useAddInventoryItem'
import { useSystemsCatalogSearch } from '../hooks/useSystemsCatalogSearch'
import { useInventoryFilters } from '../hooks/useInventoryFilters'
import { LocationCombobox } from './LocationCombobox'
import { useDebounce } from '../hooks/useDebounce'
import type { CatalogItem } from '../schemas/inventorySchemas'
import type { SystemsCatalogItem } from '../api/shipComponentsApi'
import type { LocationOption } from '../schemas/locationSchemas'

type AnyItem = CatalogItem | SystemsCatalogItem

interface BaseProps {
  open: boolean
  onClose: (opts?: { rememberedLocation?: LocationOption; rememberedOwnerId?: string }) => void
  currentUserId: string
  rememberedLocation?: LocationOption
  rememberedOwnerId?: string
}

interface InventoryProps extends BaseProps {
  scope?: 'inventory'
}

interface ShipComponentProps extends BaseProps {
  scope: 'ship-components'
}

type Props = InventoryProps | ShipComponentProps

export function AddInventoryDialog({
  open,
  onClose,
  currentUserId,
  rememberedLocation,
  rememberedOwnerId = '',
  scope = 'inventory',
}: Props) {
  const [search, setSearch] = useState('')
  const [selectedItem, setSelectedItem] = useState<AnyItem | null>(null)
  const [ownerUserId, setOwnerUserId] = useState(rememberedOwnerId || currentUserId)
  const [location, setLocation] = useState<LocationOption | undefined>(rememberedLocation)
  const [quantity, setQuantity] = useState(1)
  const [quality, setQuality] = useState(500)
  const [error, setError] = useState('')

  const isShipComponents = scope === 'ship-components'
  const debouncedSearch = useDebounce(search, 300)

  const { data: catalogData } = useCatalogItemSearch(
    !isShipComponents ? (debouncedSearch || undefined) : undefined,
    !isShipComponents
  )
  const { data: systemsCatalogData } = useSystemsCatalogSearch(
    isShipComponents ? (debouncedSearch || undefined) : undefined,
    isShipComponents
  )
  const { data: filtersData } = useInventoryFilters()
  const addItem = useAddInventoryItem(scope)

  const owners = filtersData?.owners ?? []

  const results: AnyItem[] = isShipComponents
    ? (systemsCatalogData?.items ?? [])
    : (catalogData?.items ?? [])

  async function handleSubmit() {
    if (!selectedItem) { setError('Select an item from the catalog.'); return }
    if (quantity < 1) { setError('Quantity must be at least 1.'); return }
    if (!Number.isInteger(quality) || quality < 1 || quality > 1000) {
      setError('Quality must be an integer between 1 and 1000.')
      return
    }
    setError('')

    try {
      await addItem.mutateAsync({
        itemId: selectedItem.itemId,
        ownerUserId,
        location: location?.name ?? '',
        quantity,
        quality,
        locationId: location?.id,
        locationType: location?.type,
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong.')
      return
    }

    onClose({ rememberedLocation: location, rememberedOwnerId: ownerUserId })
  }

  const isPending = addItem.isPending
  const title = isShipComponents ? 'Add Ship Component' : 'Add Item to Inventory'

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) onClose() }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription className="sr-only">
            Search the catalog, then fill in owner, location, quantity, and quality to add an item.
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-1">
            <label htmlFor="add-item-search" className="text-sm font-medium">
              Search Catalog
            </label>
            <Command shouldFilter={false}>
              <CommandInput
                id="add-item-search"
                aria-label="Search catalog"
                placeholder="Search items…"
                value={search}
                onValueChange={(v) => { setSearch(v); setSelectedItem(null) }}
              />
              <CommandList className="max-h-44">
                {results.length === 0 && search && (
                  <CommandEmpty>No results found.</CommandEmpty>
                )}
                {results.length > 0 && !selectedItem && (
                  <CommandGroup>
                    {results.map((item) => (
                      <CommandItem
                        key={item.itemId}
                        value={item.itemId}
                        onSelect={() => { setSelectedItem(item); setSearch(item.name) }}
                      >
                        {item.name}
                        {item.type && <span className="ml-1 text-muted-foreground">({item.type})</span>}
                      </CommandItem>
                    ))}
                  </CommandGroup>
                )}
              </CommandList>
            </Command>
          </div>

          {isShipComponents && selectedItem && (
            <div className="rounded border bg-muted/40 p-3 text-sm flex flex-col gap-1">
              <div className="text-muted-foreground">Name: <span className="text-foreground font-medium">{selectedItem.name}</span></div>
              <div className="text-muted-foreground">Type: <span className="text-foreground">{selectedItem.type ?? '—'}</span></div>
              <div className="text-muted-foreground text-xs">Class, Size, and Grade are derived from UEX data on first add.</div>
            </div>
          )}

          <div className="flex flex-col gap-1">
            <label htmlFor="add-item-owner" className="text-sm font-medium">Owner</label>
            <Select value={ownerUserId} onValueChange={setOwnerUserId}>
              <SelectTrigger id="add-item-owner" aria-label="Owner">
                <SelectValue placeholder="Select owner" />
              </SelectTrigger>
              <SelectContent>
                {owners.map((o) => (
                  <SelectItem key={o.userId} value={o.userId}>
                    {o.displayName}{o.userId === currentUserId ? ' (you)' : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="flex flex-col gap-1">
            <label htmlFor="add-item-location" className="text-sm font-medium">Location</label>
            <LocationCombobox
              value={location?.id}
              onValueChange={(loc) => setLocation(loc ?? undefined)}
              placeholder="Select a location…"
              aria-label="Location"
            />
          </div>

          <div className="flex flex-col gap-1">
            <label htmlFor="add-item-quantity" className="text-sm font-medium">
              Quantity
            </label>
            <Input
              id="add-item-quantity"
              aria-label="Quantity"
              type="number"
              min={1}
              value={quantity}
              onChange={(e) => setQuantity(Number(e.target.value))}
            />
          </div>

          <div className="flex flex-col gap-1">
            <label htmlFor="add-item-quality" className="text-sm font-medium">
              Quality
            </label>
            <Input
              id="add-item-quality"
              aria-label="Quality"
              type="number"
              min={1}
              max={1000}
              value={quality}
              onChange={(e) => setQuality(Number(e.target.value))}
            />
          </div>

          {error && <p className="text-sm text-destructive">{error}</p>}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onClose()}>
            Cancel
          </Button>
          <Button onClick={() => void handleSubmit()} disabled={isPending}>
            {isPending ? 'Adding…' : 'Add Item'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
