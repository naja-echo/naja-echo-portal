import { render, screen, waitFor } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/tests/server'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RoleRoute } from '@/features/auth/RoleRoute'
import { ROLES } from '@/features/auth/lib/roles'
import { navItems } from '@/features/dashboard/navigation/navItems'
import { DashboardNav } from '@/features/dashboard/components/DashboardNav'

function sessionWithRoles(roles: string[]) {
  return {
    authenticated: true as const,
    user: {
      id: 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
      displayName: 'Test User',
      discordUsername: 'testuser',
      roles,
    },
  }
}

const adminSession = sessionWithRoles([ROLES.Admin])
const regularSession = sessionWithRoles([])
const anonymousSession = { authenticated: false as const }

function renderAdminRoute(session: object) {
  server.use(http.get('/api/auth/me', () => HttpResponse.json(session)))
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/admin']}>
        <Routes>
          <Route path="/" element={<div>Landing</div>} />
          <Route path="/dashboard" element={<div>Dashboard</div>} />
          <Route element={<RoleRoute allow={[ROLES.Admin]} />}>
            <Route path="/admin" element={<div>Admin Content</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  )
}

describe('RoleRoute', () => {
  it('allows admin user to access protected route', async () => {
    renderAdminRoute(adminSession)
    await waitFor(() => {
      expect(screen.getByText('Admin Content')).toBeDefined()
    })
  })

  it('shows an unauthorized state for a signed-in user without the role', async () => {
    renderAdminRoute(regularSession)
    await waitFor(() => {
      expect(screen.getByText(/don't have access to this page/i)).toBeDefined()
    })
    expect(screen.queryByText('Admin Content')).toBeNull()
  })

  it('redirects anonymous visitors to the landing page', async () => {
    renderAdminRoute(anonymousSession)
    await waitFor(() => {
      expect(screen.getByText('Landing')).toBeDefined()
    })
    expect(screen.queryByText('Admin Content')).toBeNull()
  })
})

function renderNav(roles: string[]) {
  return render(
    <MemoryRouter>
      <DashboardNav items={navItems} roles={roles} />
    </MemoryRouter>
  )
}

describe('DashboardNav role gating', () => {
  it('hides admin items for non-admin users', () => {
    renderNav([])
    expect(screen.queryByText('Data Import')).toBeNull()
  })

  it('shows admin items for admin users', () => {
    renderNav([ROLES.Admin])
    expect(screen.getByText('Data Import')).toBeDefined()
  })

  it('shows Admin group heading for admin users', () => {
    renderNav([ROLES.Admin])
    expect(screen.getByText('Admin')).toBeDefined()
  })

  it('hides Loot Distribution from a member with no roles', () => {
    renderNav([])
    expect(screen.queryByText('Loot Distribution')).toBeNull()
  })

  it('shows Loot Distribution to a Crew Resource Officer', () => {
    renderNav([ROLES.CrewResourceOfficer])
    expect(screen.getByText('Loot Distribution')).toBeDefined()
  })

  it('shows Loot Distribution to a Quartermaster', () => {
    renderNav([ROLES.Quartermaster])
    expect(screen.getByText('Loot Distribution')).toBeDefined()
  })

  it('shows Loot Distribution to an Admin without them holding the role', () => {
    renderNav([ROLES.Admin])
    expect(screen.getByText('Loot Distribution')).toBeDefined()
  })

  it('keeps ungated entries visible to every authenticated user', () => {
    renderNav([])
    // Warehouse reads are org-wide; only the write controls on the page itself are gated.
    expect(screen.getByText('Materials')).toBeDefined()
    expect(screen.getByText('My Loot')).toBeDefined()
  })

  it('does not leak Crew Resource Officer pages to a Quartermaster-only nav check', () => {
    renderNav([ROLES.CrewResourceOfficer])
    expect(screen.queryByText('Data Import')).toBeNull()
  })
})
