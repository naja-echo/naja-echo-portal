import { useState } from 'react'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command'
import { useDebounce } from '@/features/warehouse/hooks/useDebounce'
import { useBlueprintSearch } from '../hooks/useBlueprintSearch'
import { useAddMyBlueprint } from '../hooks/useAddMyBlueprint'
import type { BlueprintSearchResult } from '../api/blueprintsApi'

interface Props {
  open: boolean
  onClose: () => void
}

export function AddBlueprintDialog({ open, onClose }: Props) {
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState<BlueprintSearchResult | null>(null)
  const [error, setError] = useState('')

  const debouncedSearch = useDebounce(search, 300)
  const { data } = useBlueprintSearch(debouncedSearch)
  const addBlueprint = useAddMyBlueprint()

  const results = data?.results ?? []

  async function handleSubmit() {
    if (!selected) return
    setError('')

    try {
      await addBlueprint.mutateAsync(selected.blueprintId)
      handleClose()
    } catch (err) {
      const status = (err as { status?: number }).status
      if (status === 409) {
        setError('This blueprint is already in your list.')
      } else {
        setError(err instanceof Error ? err.message : 'Something went wrong.')
      }
    }
  }

  function handleClose() {
    setSearch('')
    setSelected(null)
    setError('')
    onClose()
  }

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) handleClose() }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Add Blueprint</DialogTitle>
          <DialogDescription className="sr-only">
            Search the blueprint catalog and add a blueprint to your personal list.
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-1">
            <label htmlFor="add-blueprint-search" className="text-sm font-medium">
              Blueprint Name
            </label>
            <Command shouldFilter={false}>
              <CommandInput
                id="add-blueprint-search"
                aria-label="Search blueprints"
                placeholder="Search blueprints…"
                value={search}
                onValueChange={(v) => { setSearch(v); setSelected(null) }}
              />
              <CommandList className="max-h-44">
                {results.length === 0 && debouncedSearch && (
                  <CommandEmpty>No blueprints found.</CommandEmpty>
                )}
                {results.length > 0 && !selected && (
                  <CommandGroup>
                    {results.map((item) => (
                      <CommandItem
                        key={item.blueprintId}
                        value={item.blueprintId}
                        onSelect={() => { setSelected(item); setSearch(item.productName) }}
                      >
                        {item.productName}
                        {item.type && (
                          <span className="ml-1 text-muted-foreground">({item.type})</span>
                        )}
                      </CommandItem>
                    ))}
                  </CommandGroup>
                )}
              </CommandList>
            </Command>
          </div>

          {error && <p className="text-sm text-destructive">{error}</p>}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose}>
            Cancel
          </Button>
          <Button
            onClick={() => void handleSubmit()}
            disabled={!selected || addBlueprint.isPending}
          >
            {addBlueprint.isPending ? 'Adding…' : 'Add Blueprint'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
