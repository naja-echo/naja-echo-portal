import { useState, useMemo } from 'react'
import type { ComboboxOption } from '@/components/ui/combobox'
import { blueprintLabel } from '../config/blueprintLabels'

interface BlueprintItem {
  blueprintId: string
  productName: string | null
  type: string | null
  subtype: string | null
  gear: string | null
  ingredientCount: number
}

interface BlueprintFilterState {
  name: string
  category: string
  subcategory: string
}

interface UseBlueprintFiltersResult<T extends BlueprintItem> {
  filters: BlueprintFilterState
  setName: (v: string) => void
  setCategory: (v: string) => void
  setSubcategory: (v: string) => void
  categoryOptions: ComboboxOption[]
  subcategoryOptions: ComboboxOption[]
  filtered: T[]
}

export function useBlueprintFilters<T extends BlueprintItem>(items: T[]): UseBlueprintFiltersResult<T> {
  const [filters, setFilters] = useState<BlueprintFilterState>({ name: '', category: '', subcategory: '' })

  const setName = (v: string) => setFilters(f => ({ ...f, name: v }))
  const setCategory = (v: string) => setFilters(f => ({ ...f, category: v, subcategory: '' }))
  const setSubcategory = (v: string) => setFilters(f => ({ ...f, subcategory: v }))

  // Category = gear field
  const categoryOptions = useMemo<ComboboxOption[]>(() => {
    const seen = new Set<string>()
    for (const item of items) {
      if (item.gear) seen.add(item.gear)
    }
    return Array.from(seen)
      .sort((a, b) => blueprintLabel(a).localeCompare(blueprintLabel(b)))
      .map(v => ({ value: v, label: blueprintLabel(v) }))
  }, [items])

  // Subcategory = type field, narrowed to the selected gear category
  const subcategoryOptions = useMemo<ComboboxOption[]>(() => {
    const source = filters.category
      ? items.filter(item => item.gear === filters.category)
      : items
    const seen = new Set<string>()
    for (const item of source) {
      if (item.type) seen.add(item.type)
    }
    return Array.from(seen)
      .sort((a, b) => blueprintLabel(a).localeCompare(blueprintLabel(b)))
      .map(v => ({ value: v, label: blueprintLabel(v) }))
  }, [items, filters.category])

  const filtered = useMemo<T[]>(() => {
    const nameLower = filters.name.toLowerCase()
    return items.filter(item => {
      if (nameLower && !item.productName?.toLowerCase().includes(nameLower)) return false
      if (filters.category && item.gear !== filters.category) return false
      if (filters.subcategory && item.type !== filters.subcategory) return false
      return true
    })
  }, [items, filters])

  return { filters, setName, setCategory, setSubcategory, categoryOptions, subcategoryOptions, filtered }
}
