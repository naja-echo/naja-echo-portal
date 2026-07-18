export const roleDisplayNames: Record<string, string> = {
  Admin: 'Administrator',
  Quartermaster: 'Quartermaster',
  CrewResourceOfficer: 'Crew Resource Officer',
}

export const availableRoles = Object.keys(roleDisplayNames)

export function getRoleDisplayName(role: string): string {
  return roleDisplayNames[role] ?? role
}
