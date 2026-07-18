import type { LucideIcon } from 'lucide-react'
import { BarChart2, Coins, Database, LayoutDashboard, Package, Ship, Users } from 'lucide-react'

export interface NavItem {
  label: string
  path: string
  icon: LucideIcon
  end?: boolean
  access?: string
  group?: string
}

export const navItems: NavItem[] = [
  { label: 'Dashboard', path: '/dashboard', icon: LayoutDashboard, end: true },
  { label: 'My Hangar', path: '/hangar/mine', icon: Ship, group: 'Hangar' },
  { label: 'Org Hangar', path: '/hangar/org', icon: Users, group: 'Hangar' },
  { label: 'Items', path: '/warehouse/items', icon: Package, group: 'Warehouse' },
  { label: 'Ship Components', path: '/warehouse/ship-components', icon: Package, group: 'Warehouse' },
  { label: 'Materials', path: '/warehouse/materials', icon: Package, group: 'Warehouse' },
  { label: 'My Loot', path: '/crew-resources/my-loot', icon: Coins, group: 'Crew Resources' },
  { label: 'Loot Distribution', path: '/crew-resources/loot-distribution', icon: BarChart2, group: 'Crew Resources' },
  {
    label: 'Users',
    path: '/dashboard/admin/users',
    icon: Users,
    access: 'admin',
    group: 'Admin',
  },
  {
    label: 'Data Import',
    path: '/dashboard/admin/data-import',
    icon: Database,
    access: 'admin',
    group: 'Admin',
  },
]
