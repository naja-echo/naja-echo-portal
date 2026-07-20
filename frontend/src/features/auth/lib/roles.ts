/**
 * The single source of truth for role names on the frontend. Mirrors
 * `NajaEcho.Domain.Users.Roles` on the backend — adding a role means editing both.
 */
export const ROLES = {
  Admin: 'Admin',
  Quartermaster: 'Quartermaster',
  CrewResourceOfficer: 'CrewResourceOfficer',
} as const

export type Role = (typeof ROLES)[keyof typeof ROLES]

/** Every role, in display order. */
export const ALL_ROLES = Object.values(ROLES) as Role[]

/**
 * The shared authorization rule: a user passes if they hold one of the allowed roles, or if
 * they are an Admin — Admin is an implicit superset of every other role, matching the backend
 * policy definitions.
 *
 * This is a plain function rather than a hook so that components receiving roles as a prop
 * (notably `DashboardNav`) can share the same rule as components that call `useHasRole`.
 */
export function hasAnyRole(userRoles: readonly string[], allowed: readonly Role[]): boolean {
  if (userRoles.includes(ROLES.Admin)) return true
  return allowed.some((role) => userRoles.includes(role))
}
