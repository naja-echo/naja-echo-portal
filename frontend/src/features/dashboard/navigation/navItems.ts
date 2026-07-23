import type { LucideIcon } from 'lucide-react'
import { BarChart2, Coins, Database, LayoutDashboard, Package, ScrollText, Ship, Users } from 'lucide-react'
import { ROLES, type Role } from '@/features/auth/lib/roles'

export interface NavItem {
  label: string
  path: string
  icon: LucideIcon
  end?: boolean
  /** Roles permitted to see this entry. Omit for entries open to any authenticated user. */
  access?: Role[]
  group?: string
}

export const navItems: NavItem[] = [
  { label: 'Dashboard', path: '/dashboard', icon: LayoutDashboard, end: true },
  { label: 'My Hangar', path: '/hangar/mine', icon: Ship, group: 'Hangar' },
  { label: 'Org Hangar', path: '/hangar/org', icon: Users, group: 'Hangar' },
  { label: 'Items', path: '/warehouse/items', icon: Package, group: 'Warehouse' },
  { label: 'Ship Components', path: '/warehouse/ship-components', icon: Package, group: 'Warehouse' },
  { label: 'Materials', path: '/warehouse/materials', icon: Package, group: 'Warehouse' },
  { label: 'My Blueprints', path: '/blueprints/mine', icon: ScrollText, group: 'Blueprints' },
  { label: 'My Loot', path: '/crew-resources/my-loot', icon: Coins, group: 'Crew Resources' },
  {
    label: 'Loot Distribution',
    path: '/crew-resources/loot-distribution',
    icon: BarChart2,
    access: [ROLES.CrewResourceOfficer, ROLES.Quartermaster],
    group: 'Crew Resources',
  },
  {
    label: 'Users',
    path: '/dashboard/admin/users',
    icon: Users,
    access: [ROLES.Admin],
    group: 'Admin',
  },
  {
    label: 'Data Import',
    path: '/dashboard/admin/data-import',
    icon: Database,
    access: [ROLES.Admin],
    group: 'Admin',
  },
]
