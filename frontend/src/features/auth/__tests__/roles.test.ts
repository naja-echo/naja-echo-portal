import { describe, it, expect } from 'vitest'
import { ALL_ROLES, ROLES, hasAnyRole } from '../lib/roles'

describe('hasAnyRole', () => {
  it('passes a user holding the exact role', () => {
    expect(hasAnyRole([ROLES.Quartermaster], [ROLES.Quartermaster])).toBe(true)
  })

  it('passes an Admin regardless of the required role', () => {
    expect(hasAnyRole([ROLES.Admin], [ROLES.CrewResourceOfficer])).toBe(true)
  })

  it('passes when the user holds any one of several allowed roles', () => {
    expect(
      hasAnyRole([ROLES.Quartermaster], [ROLES.CrewResourceOfficer, ROLES.Quartermaster])
    ).toBe(true)
  })

  it('rejects a user holding none of the allowed roles', () => {
    expect(hasAnyRole([ROLES.CrewResourceOfficer], [ROLES.Quartermaster])).toBe(false)
  })

  it('rejects a user with no roles at all', () => {
    expect(hasAnyRole([], [ROLES.Quartermaster])).toBe(false)
  })

  it('rejects everyone when the allowed list is empty and the user is not an Admin', () => {
    expect(hasAnyRole([ROLES.Quartermaster], [])).toBe(false)
  })

  it('still passes an Admin when the allowed list is empty', () => {
    expect(hasAnyRole([ROLES.Admin], [])).toBe(true)
  })

  it('ignores unknown role strings', () => {
    expect(hasAnyRole(['NotARealRole'], [ROLES.Quartermaster])).toBe(false)
  })
})

describe('ALL_ROLES', () => {
  it('contains every defined role exactly once', () => {
    expect(ALL_ROLES).toEqual([ROLES.Admin, ROLES.Quartermaster, ROLES.CrewResourceOfficer])
    expect(new Set(ALL_ROLES).size).toBe(ALL_ROLES.length)
  })
})
