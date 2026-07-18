import { describe, it, expect } from 'vitest'
import { navItems } from '@/features/dashboard/navigation/navItems'

describe('Crew Resources nav group', () => {
  const crewResourcesItems = navItems.filter((item) => item.group === 'Crew Resources')

  it('includes My Loot nav item', () => {
    const myLoot = crewResourcesItems.find((item) => item.label === 'My Loot')
    expect(myLoot).toBeDefined()
    expect(myLoot?.path).toBe('/crew-resources/my-loot')
  })

  it('includes Loot Distribution nav item', () => {
    const distribution = crewResourcesItems.find((item) => item.label === 'Loot Distribution')
    expect(distribution).toBeDefined()
    expect(distribution?.path).toBe('/crew-resources/loot-distribution')
  })

  it('no access guard on Crew Resources items (visible to all authenticated members)', () => {
    crewResourcesItems.forEach((item) => {
      expect(item.access).toBeUndefined()
    })
  })
})
