import { useState, useMemo } from 'react'
import type { ComboboxOption } from '@/components/ui/combobox'

interface BlueprintItem {
  blueprintId: string
  productName: string | null
  type: string | null
  subtype: string | null
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
  const setCategory = (v: string) => setFilters(f => ({ ...f, category: v }))
  const setSubcategory = (v: string) => setFilters(f => ({ ...f, subcategory: v }))

  const categoryOptions = useMemo<ComboboxOption[]>(() => {
    const seen = new Set<string>()
    for (const item of items) {
      if (item.type) seen.add(item.type)
    }
    return Array.from(seen)
      .sort()
      .map(v => ({ value: v, label: v }))
  }, [items])

  const subcategoryOptions = useMemo<ComboboxOption[]>(() => {
    const source = filters.category
      ? items.filter(item => item.type === filters.category)
      : items
    const seen = new Set<string>()
    for (const item of source) {
      if (item.subtype) seen.add(item.subtype)
    }
    return Array.from(seen)
      .sort()
      .map(v => ({ value: v, label: v }))
  }, [items, filters.category])

  const filtered = useMemo<T[]>(() => {
    const nameLower = filters.name.toLowerCase()
    return items.filter(item => {
      if (nameLower && !item.productName?.toLowerCase().includes(nameLower)) return false
      if (filters.category && item.type !== filters.category) return false
      if (filters.subcategory && item.subtype !== filters.subcategory) return false
      return true
    })
  }, [items, filters])

  return { filters, setName, setCategory, setSubcategory, categoryOptions, subcategoryOptions, filtered }
}
