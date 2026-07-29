import { NavLink } from 'react-router-dom'
import { cn } from '@/lib/utils'
import { hasAnyRole } from '@/features/auth/lib/roles'
import type { NavItem } from '../navigation/navItems'

interface DashboardNavProps {
  items: NavItem[]
  roles?: string[]
  onNavigate?: () => void
}

export function DashboardNav({ items, roles = [], onNavigate }: DashboardNavProps) {
  // An item without `access` is open to any authenticated user; otherwise it needs one of the
  // listed roles (or Admin, per the shared rule).
  const visibleItems = items.filter((item) => !item.access || hasAnyRole(roles, item.access))

  // Group items by their group label, preserving insertion order
  const sections: Array<{ group?: string; items: NavItem[] }> = []
  for (const item of visibleItems) {
    const existing = sections.find((s) => s.group === item.group)
    if (existing) {
      existing.items.push(item)
    } else {
      sections.push({ group: item.group, items: [item] })
    }
  }

  return (
    <div className="flex flex-col gap-4">
      {sections.map((section) => (
        <div key={section.group ?? '__root'}>
          {section.group && (
            <p className="mb-1 px-3 text-xs font-semibold uppercase tracking-wider text-muted-foreground/70">
              {section.group}
            </p>
          )}
          <ul className="flex flex-col gap-1" role="list">
            {section.items.map((item) => (
              <li key={item.path}>
                <NavLink
                  to={item.path}
                  end={item.end}
                  onClick={onNavigate}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                      'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1',
                      isActive
                        ? 'border-l-2 border-primary bg-accent/30 pl-[calc(0.75rem-2px)] text-foreground'
                        : 'border-l-2 border-transparent text-muted-foreground hover:bg-accent/20 hover:text-foreground'
                    )
                  }
                >
                  {({ isActive }) => (
                    <>
                      <item.icon className="h-4 w-4 shrink-0" aria-hidden />
                      <span>{item.label}</span>
                      {isActive && <span className="sr-only">(current)</span>}
                    </>
                  )}
                </NavLink>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  )
}
