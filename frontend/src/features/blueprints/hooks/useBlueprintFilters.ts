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
  componentClass: string | null
  componentSize: number | null
  componentGrade: string | null
  ingredientCount: number
}

interface BlueprintFilterState {
  name: string
  category: string
  subcategory: string
  itemType: string
  componentClass: string
  componentSize: string
  componentGrade: string
}

interface UseBlueprintFiltersResult<T extends BlueprintItem> {
  filters: BlueprintFilterState
  setName: (v: string) => void
  setCategory: (v: string) => void
  setSubcategory: (v: string) => void
  setItemType: (v: string) => void
  setComponentClass: (v: string) => void
  setComponentSize: (v: string) => void
  setComponentGrade: (v: string) => void
  categoryOptions: ComboboxOption[]
  subcategoryOptions: ComboboxOption[]
  itemTypeOptions: ComboboxOption[]
  componentClassOptions: ComboboxOption[]
  componentSizeOptions: ComboboxOption[]
  componentGradeOptions: ComboboxOption[]
  filtered: T[]
}

// Derives the Type filter value for a blueprint.
// Armor items encode their body part in the tag; all others use subtype (null → 'misc').
function getItemType(item: BlueprintItem): string {
  return extractArmorType(item.tag) ?? item.subtype ?? 'misc'
}

const EMPTY_STATE: BlueprintFilterState = {
  name: '', category: '', subcategory: '', itemType: '',
  componentClass: '', componentSize: '', componentGrade: '',
}

export function useBlueprintFilters<T extends BlueprintItem>(items: T[]): UseBlueprintFiltersResult<T> {
  const [filters, setFilters] = useState<BlueprintFilterState>(EMPTY_STATE)

  const setName = (v: string) => setFilters(f => ({ ...f, name: v }))
  const setCategory = (v: string) => setFilters(f => ({ ...EMPTY_STATE, name: f.name, category: v }))
  const setSubcategory = (v: string) => setFilters(f => ({ ...f, subcategory: v, itemType: '', componentClass: '', componentSize: '', componentGrade: '' }))
  const setItemType = (v: string) => setFilters(f => ({ ...f, itemType: v }))
  const setComponentClass = (v: string) => setFilters(f => ({ ...f, componentClass: v }))
  const setComponentSize = (v: string) => setFilters(f => ({ ...f, componentSize: v }))
  const setComponentGrade = (v: string) => setFilters(f => ({ ...f, componentGrade: v }))

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

  // Items passing name + category + subcategory filters
  const subcategoryFiltered = useMemo<T[]>(() => {
    const nameLower = filters.name.toLowerCase()
    return items.filter(item => {
      if (nameLower && !item.productName?.toLowerCase().includes(nameLower)) return false
      if (filters.category && item.gear !== filters.category) return false
      if (filters.subcategory && item.type !== filters.subcategory) return false
      return true
    })
  }, [items, filters.name, filters.category, filters.subcategory])

  // Type options (armor body parts or weapon subtypes) from items in scope
  const itemTypeOptions = useMemo<ComboboxOption[]>(() => {
    const seen = new Set<string>()
    for (const item of subcategoryFiltered) {
      seen.add(getItemType(item))
    }
    return Array.from(seen)
      .sort((a, b) => blueprintLabel(a).localeCompare(blueprintLabel(b)))
      .map(v => ({ value: v, label: blueprintLabel(v) }))
  }, [subcategoryFiltered])

  // Component attribute options — derived from items in scope after subcategory filter
  const componentClassOptions = useMemo<ComboboxOption[]>(() => {
    const seen = new Set<string>()
    for (const item of subcategoryFiltered) {
      if (item.componentClass) seen.add(item.componentClass)
    }
    return Array.from(seen).sort().map(v => ({ value: v, label: v }))
  }, [subcategoryFiltered])

  const componentSizeOptions = useMemo<ComboboxOption[]>(() => {
    const seen = new Set<number>()
    for (const item of subcategoryFiltered) {
      if (item.componentSize != null) seen.add(item.componentSize)
    }
    return Array.from(seen)
      .sort((a, b) => a - b)
      .map(v => ({ value: String(v), label: String(v) }))
  }, [subcategoryFiltered])

  const componentGradeOptions = useMemo<ComboboxOption[]>(() => {
    const seen = new Set<string>()
    for (const item of subcategoryFiltered) {
      if (item.componentGrade) seen.add(item.componentGrade)
    }
    return Array.from(seen).sort().map(v => ({ value: v, label: v }))
  }, [subcategoryFiltered])

  const filtered = useMemo<T[]>(() => {
    return subcategoryFiltered.filter(item => {
      if (filters.itemType && getItemType(item) !== filters.itemType) return false
      if (filters.componentClass && item.componentClass !== filters.componentClass) return false
      if (filters.componentSize && String(item.componentSize ?? '') !== filters.componentSize) return false
      if (filters.componentGrade && item.componentGrade !== filters.componentGrade) return false
      return true
    })
  }, [subcategoryFiltered, filters.itemType, filters.componentClass, filters.componentSize, filters.componentGrade])

  return {
    filters,
    setName, setCategory, setSubcategory, setItemType,
    setComponentClass, setComponentSize, setComponentGrade,
    categoryOptions, subcategoryOptions, itemTypeOptions,
    componentClassOptions, componentSizeOptions, componentGradeOptions,
    filtered,
  }
}
