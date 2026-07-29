import { renderHook, act } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { useBlueprintFilters } from '../hooks/useBlueprintFilters'

const BP = (overrides: Partial<{
  blueprintId: string
  productName: string | null
  type: string | null
  subtype: string | null
  ingredientCount: number
}> = {}) => ({
  blueprintId: '11111111-1111-1111-1111-111111111111',
  productName: 'Widget Mk1',
  type: 'Weapon',
  subtype: 'Pistol',
  ingredientCount: 2,
  ...overrides,
})

describe('useBlueprintFilters', () => {
  it('returns all items when no filters are active', () => {
    const items = [BP(), BP({ blueprintId: '22222222-2222-2222-2222-222222222222', type: 'Ship', subtype: 'Fighter' })]
    const { result } = renderHook(() => useBlueprintFilters(items))
    expect(result.current.filtered).toHaveLength(2)
  })

  it('filters by category', () => {
    const items = [
      BP({ type: 'Weapon', subtype: 'Pistol' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', type: 'Ship', subtype: 'Fighter' }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    act(() => result.current.setCategory('Weapon'))
    expect(result.current.filtered).toHaveLength(1)
    expect(result.current.filtered[0].type).toBe('Weapon')
  })

  it('filters by subcategory without a category selected', () => {
    const items = [
      BP({ type: 'Weapon', subtype: 'Pistol' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', type: 'Ship', subtype: 'Fighter' }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    act(() => result.current.setSubcategory('Pistol'))
    expect(result.current.filtered).toHaveLength(1)
    expect(result.current.filtered[0].subtype).toBe('Pistol')
  })

  it('filters by category AND subcategory', () => {
    const items = [
      BP({ type: 'Weapon', subtype: 'Pistol' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', type: 'Weapon', subtype: 'Rifle' }),
      BP({ blueprintId: '33333333-3333-3333-3333-333333333333', type: 'Ship', subtype: 'Fighter' }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    act(() => {
      result.current.setCategory('Weapon')
      result.current.setSubcategory('Pistol')
    })
    expect(result.current.filtered).toHaveLength(1)
    expect(result.current.filtered[0].subtype).toBe('Pistol')
  })

  it('categoryOptions are distinct, sorted, non-null type values', () => {
    const items = [
      BP({ type: 'Weapon' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', type: 'Ship' }),
      BP({ blueprintId: '33333333-3333-3333-3333-333333333333', type: 'Weapon' }),
      BP({ blueprintId: '44444444-4444-4444-4444-444444444444', type: null }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    expect(result.current.categoryOptions.map(o => o.value)).toEqual(['Ship', 'Weapon'])
  })

  it('subcategoryOptions show all when no category selected', () => {
    const items = [
      BP({ type: 'Weapon', subtype: 'Pistol' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', type: 'Ship', subtype: 'Fighter' }),
      BP({ blueprintId: '33333333-3333-3333-3333-333333333333', type: 'Weapon', subtype: null }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    expect(result.current.subcategoryOptions.map(o => o.value)).toEqual(['Fighter', 'Pistol'])
  })

  it('subcategoryOptions narrow when category selected', () => {
    const items = [
      BP({ type: 'Weapon', subtype: 'Pistol' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', type: 'Ship', subtype: 'Fighter' }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    act(() => result.current.setCategory('Weapon'))
    expect(result.current.subcategoryOptions.map(o => o.value)).toEqual(['Pistol'])
  })

  it('clearing category does not auto-clear subcategory selection', () => {
    const items = [
      BP({ type: 'Weapon', subtype: 'Pistol' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', type: 'Ship', subtype: 'Fighter' }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    act(() => {
      result.current.setCategory('Weapon')
      result.current.setSubcategory('Pistol')
    })
    act(() => result.current.setCategory(''))
    expect(result.current.filters.subcategory).toBe('Pistol')
  })

  // Phase 4 (US2) name filter tests
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
      BP({ productName: 'Widget Mk1', type: 'Weapon' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', productName: 'Widget Hull', type: 'Ship' }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    act(() => {
      result.current.setName('widget')
      result.current.setCategory('Weapon')
    })
    expect(result.current.filtered).toHaveLength(1)
    expect(result.current.filtered[0].type).toBe('Weapon')
  })

  it('clearing name restores category-filtered list', () => {
    const items = [
      BP({ productName: 'Widget Mk1', type: 'Weapon' }),
      BP({ blueprintId: '22222222-2222-2222-2222-222222222222', productName: 'Hull Panel', type: 'Weapon' }),
    ]
    const { result } = renderHook(() => useBlueprintFilters(items))
    act(() => {
      result.current.setCategory('Weapon')
      result.current.setName('widget')
    })
    expect(result.current.filtered).toHaveLength(1)
    act(() => result.current.setName(''))
    expect(result.current.filtered).toHaveLength(2)
  })
})
