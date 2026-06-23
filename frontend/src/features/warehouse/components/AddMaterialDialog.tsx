import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogFooter, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { useCommoditySearch } from '../hooks/useCommoditySearch'
import { useAddMaterial } from '../hooks/useAddMaterial'
import { useMaterialFilters } from '../hooks/useMaterialFilters'
import { LocationCombobox } from './LocationCombobox'
import { useDebounce } from '../hooks/useDebounce'
import type { CommodityCatalogItem } from '../schemas/materialSchemas'
import type { LocationOption } from '../schemas/locationSchemas'

interface Props {
  open: boolean
  onClose: (opts?: { rememberedLocation?: LocationOption; rememberedOwnerId?: string }) => void
  currentUserId: string
  rememberedLocation?: LocationOption
  rememberedOwnerId?: string
}

export function AddMaterialDialog({
  open,
  onClose,
  currentUserId,
  rememberedLocation,
  rememberedOwnerId = '',
}: Props) {
  const [search, setSearch] = useState('')
  const [selectedCommodity, setSelectedCommodity] = useState<CommodityCatalogItem | null>(null)
  const [ownerUserId, setOwnerUserId] = useState(rememberedOwnerId || currentUserId)
  const [location, setLocation] = useState<LocationOption | undefined>(rememberedLocation)
  const [quantity, setQuantity] = useState(1)
  const [quality, setQuality] = useState(500)
  const [error, setError] = useState('')

  const debouncedSearch = useDebounce(search, 300)

  const { data: catalogData } = useCommoditySearch(debouncedSearch || undefined)
  const { data: filtersData } = useMaterialFilters()
  const addMaterial = useAddMaterial()

  const owners = filtersData?.owners ?? []
  const results = catalogData?.commodities ?? []

  async function handleSubmit() {
    if (!selectedCommodity) { setError('Select a commodity from the catalog.'); return }
    if (quantity <= 0) { setError('Quantity must be greater than 0.'); return }
    if (!Number.isInteger(quality) || quality < 1 || quality > 1000) {
      setError('Quality must be an integer between 1 and 1000.')
      return
    }
    setError('')

    try {
      await addMaterial.mutateAsync({
        commodityId: selectedCommodity.commodityId,
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

  const isPending = addMaterial.isPending

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) onClose() }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Add Material</DialogTitle>
          <DialogDescription className="sr-only">
            Search the commodity catalog, then fill in owner, location, quantity, and quality to add a material.
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-1">
            <label htmlFor="add-material-search" className="text-sm font-medium">
              Search Commodities
            </label>
            <Command shouldFilter={false}>
              <CommandInput
                id="add-material-search"
                aria-label="Search commodities"
                placeholder="Search materials…"
                value={search}
                onValueChange={(v) => { setSearch(v); setSelectedCommodity(null) }}
              />
              <CommandList className="max-h-44">
                {results.length === 0 && search && (
                  <CommandEmpty>No results found.</CommandEmpty>
                )}
                {results.length > 0 && !selectedCommodity && (
                  <CommandGroup>
                    {results.map((c) => (
                      <CommandItem
                        key={c.commodityId}
                        value={c.commodityId}
                        onSelect={() => { setSelectedCommodity(c); setSearch(c.name) }}
                      >
                        {c.name}
                        {c.code && <span className="ml-1 text-muted-foreground">({c.code})</span>}
                      </CommandItem>
                    ))}
                  </CommandGroup>
                )}
              </CommandList>
            </Command>
          </div>

          <div className="flex flex-col gap-1">
            <label htmlFor="add-material-owner" className="text-sm font-medium">Owner</label>
            <Select value={ownerUserId} onValueChange={setOwnerUserId}>
              <SelectTrigger id="add-material-owner" aria-label="Owner">
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
            <label htmlFor="add-material-location" className="text-sm font-medium">Location</label>
            <LocationCombobox
              value={location?.id}
              onValueChange={(loc) => setLocation(loc ?? undefined)}
              placeholder="Select a location…"
              aria-label="Location"
            />
          </div>

          <div className="flex flex-col gap-1">
            <label htmlFor="add-material-quantity" className="text-sm font-medium">
              Quantity
            </label>
            <Input
              id="add-material-quantity"
              aria-label="Quantity"
              type="number"
              inputMode="decimal"
              step="0.001"
              min={0.001}
              value={quantity}
              onChange={(e) => setQuantity(Number(e.target.value))}
            />
          </div>

          <div className="flex flex-col gap-1">
            <label htmlFor="add-material-quality" className="text-sm font-medium">
              Quality
            </label>
            <Input
              id="add-material-quality"
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
            {isPending ? 'Adding…' : 'Add Material'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
