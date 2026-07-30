import { useState, useMemo } from 'react'
import type { ComboboxOption } from '@/components/ui/combobox'
import { blueprintLabel, extractArmorType } from '../config/blueprintLabels'

interface BlueprintItem {
  blueprintId: string
  productName: string | null
  type: string | null
  subtype: string | null
  gear: string | null
  tag: string | null
  ingredientCount: number
}

interface BlueprintFilterState {
  name: string
  category: string
  subcategory: string
  armorType: string
}

interface UseBlueprintFiltersResult<T extends BlueprintItem> {
  filters: BlueprintFilterState
  setName: (v: string) => void
  setCategory: (v: string) => void
  setSubcategory: (v: string) => void
  setArmorType: (v: string) => void
  categoryOptions: ComboboxOption[]
  subcategoryOptions: ComboboxOption[]
  armorTypeOptions: ComboboxOption[]
  filtered: T[]
}

export function useBlueprintFilters<T extends BlueprintItem>(items: T[]): UseBlueprintFiltersResult<T> {
  const [filters, setFilters] = useState<BlueprintFilterState>({ name: '', category: '', subcategory: '', armorType: '' })

  const setName = (v: string) => setFilters(f => ({ ...f, name: v }))
  const setCategory = (v: string) => setFilters(f => ({ ...f, category: v, subcategory: '', armorType: '' }))
  const setSubcategory = (v: string) => setFilters(f => ({ ...f, subcategory: v, armorType: '' }))
  const setArmorType = (v: string) => setFilters(f => ({ ...f, armorType: v }))

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

  // Items passing name + category + subcategory filters (source for armorType options and final filter)
  const subcategoryFiltered = useMemo<T[]>(() => {
    const nameLower = filters.name.toLowerCase()
    return items.filter(item => {
      if (nameLower && !item.productName?.toLowerCase().includes(nameLower)) return false
      if (filters.category && item.gear !== filters.category) return false
      if (filters.subcategory && item.type !== filters.subcategory) return false
      return true
    })
  }, [items, filters.name, filters.category, filters.subcategory])

  // Armor type options derived from items currently in scope after category+subcategory filters
  const armorTypeOptions = useMemo<ComboboxOption[]>(() => {
    const seen = new Set<string>()
    for (const item of subcategoryFiltered) {
      const part = extractArmorType(item.tag)
      if (part) seen.add(part)
    }
    return Array.from(seen)
      .sort((a, b) => blueprintLabel(a).localeCompare(blueprintLabel(b)))
      .map(v => ({ value: v, label: blueprintLabel(v) }))
  }, [subcategoryFiltered])

  const filtered = useMemo<T[]>(() => {
    if (!filters.armorType) return subcategoryFiltered
    return subcategoryFiltered.filter(item => extractArmorType(item.tag) === filters.armorType)
  }, [subcategoryFiltered, filters.armorType])

  return { filters, setName, setCategory, setSubcategory, setArmorType, categoryOptions, subcategoryOptions, armorTypeOptions, filtered }
}
