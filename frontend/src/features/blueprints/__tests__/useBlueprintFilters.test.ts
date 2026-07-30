import { renderHook, act } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { useBlueprintFilters } from '../hooks/useBlueprintFilters'

const BP = (overrides: Partial<{
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
}> = {}) => ({
  blueprintId: '11111111-1111-1111-1111-111111111111',
  productName: 'Widget Mk1',
  type: 'Weapon',
  subtype: 'Pistol',
  gear: 'spacegear',
  tag: null,
  componentClass: null,
  componentSize: null,
  componentGrade: null,
  ingredientCount: 2,
  ...overrides,
})

describe('useBlueprintFilters', () => {
  it('returns all items when no filters are active', () => {
    const items = [BP(), BP({ blueprintId: '22222222-2222-2222-2222-222222222222', gear: 'vehiclegear', type: 'Cooler' })]
    const { result } = renderHook(() => useBlueprintFilters(items))
    expect(result.current.filtered).toHaveLength(2)
  })

  it('filters by category (gear)', () => {
    const items = [
      BP({ gear: 'spacegear', type: 'Weapon' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', gear: 'vehiclegear', type: 'Cooler' }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    act(() => result.current.setCategory('vehiclegear'))
    expect(result.current.filtered).toHaveLength(1)
    expect(result.current.filtered[0].gear).toBe('vehiclegear')
  })

  it('filters by subcategory (type)', () => {
    const items = [
      BP({ gear: 'vehiclegear', type: 'Weapon' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', gear: 'vehiclegear', type: 'Cooler' }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    act(() => result.current.setSubcategory('Cooler'))
    expect(result.current.filtered).toHaveLength(1)
    expect(result.current.filtered[0].type).toBe('Cooler')
  })

  it('filters by category AND subcategory', () => {
    const items = [
      BP({ gear: 'vehiclegear', type: 'Weapon' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', gear: 'vehiclegear', type: 'Cooler' }),
      BP({ blueprintId: '33333333-3333-3333-3333-333333333333', gear: 'spacegear', type: 'Cooler' }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    act(() => { result.current.setCategory('vehiclegear'); result.current.setSubcategory('Cooler') })
    expect(result.current.filtered).toHaveLength(1)
    expect(result.current.filtered[0].gear).toBe('vehiclegear')
    expect(result.current.filtered[0].type).toBe('Cooler')
  })

  it('categoryOptions are distinct sorted non-null gear values', () => {
    const items = [
      BP({ gear: 'vehiclegear' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', gear: 'spacegear' }),
      BP({ blueprintId: '33333333-3333-3333-3333-333333333333', gear: 'vehiclegear' }),
      BP({ blueprintId: '44444444-4444-4444-4444-444444444444', gear: null }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    expect(result.current.categoryOptions.map(o => o.value)).toEqual(['spacegear', 'vehiclegear'])
  })

  it('subcategoryOptions show all types when no category selected', () => {
    const items = [
      BP({ gear: 'vehiclegear', type: 'Weapon' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', gear: 'spacegear', type: 'Cooler' }),
      BP({ blueprintId: '33333333-3333-3333-3333-333333333333', gear: 'vehiclegear', type: null }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    expect(result.current.subcategoryOptions.map(o => o.value)).toEqual(['Cooler', 'Weapon'])
  })

  it('subcategoryOptions narrow to types within selected gear', () => {
    const items = [
      BP({ gear: 'vehiclegear', type: 'Weapon' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', gear: 'spacegear', type: 'Cooler' }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    act(() => result.current.setCategory('vehiclegear'))
    expect(result.current.subcategoryOptions.map(o => o.value)).toEqual(['Weapon'])
  })

  it('changing category clears the subcategory selection', () => {
    const items = [
      BP({ gear: 'vehiclegear', type: 'Weapon' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', gear: 'spacegear', type: 'Cooler' }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    act(() => { result.current.setCategory('vehiclegear'); result.current.setSubcategory('Weapon') })
    act(() => result.current.setCategory('spacegear'))
    expect(result.current.filters.subcategory).toBe('')
  })

  it('filters by name case-insensitively', () => {
    const items = [
      BP({ productName: 'Widget Mk1' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', productName: 'Hull Panel' }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    act(() => result.current.setName('widget'))
    expect(result.current.filtered).toHaveLength(1)
    expect(result.current.filtered[0].productName).toBe('Widget Mk1')
  })

  it('applies name filter alongside category filter', () => {
    const items = [
      BP({ productName: 'Widget Mk1', gear: 'vehiclegear' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', productName: 'Widget Hull', gear: 'spacegear' }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    act(() => { result.current.setName('widget'); result.current.setCategory('vehiclegear') })
    expect(result.current.filtered).toHaveLength(1)
    expect(result.current.filtered[0].gear).toBe('vehiclegear')
  })

  it('clearing name restores category-filtered list', () => {
    const items = [
      BP({ productName: 'Widget Mk1', gear: 'vehiclegear' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', productName: 'Hull Panel', gear: 'vehiclegear' }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    act(() => { result.current.setCategory('vehiclegear'); result.current.setName('widget') })
    expect(result.current.filtered).toHaveLength(1)
    act(() => result.current.setName(''))
    expect(result.current.filtered).toHaveLength(2)
  })
})
