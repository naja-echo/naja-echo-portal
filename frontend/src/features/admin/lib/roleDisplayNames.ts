import { ALL_ROLES, ROLES, type Role } from '@/features/auth/lib/roles'

export const roleDisplayNames: Record<Role, string> = {
  [ROLES.Admin]: 'Administrator',
  [ROLES.Quartermaster]: 'Quartermaster',
  [ROLES.CrewResourceOfficer]: 'Crew Resource Officer',
}

export const availableRoles = ALL_ROLES

export function getRoleDisplayName(role: string): string {
  return roleDisplayNames[role as Role] ?? role
}
