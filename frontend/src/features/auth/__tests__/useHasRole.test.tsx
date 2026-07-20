import { renderHook, waitFor } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/tests/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useHasRole } from '../hooks/useHasRole'
import { ROLES, type Role } from '../lib/roles'

function renderUseHasRole(session: object, allowed: Role[]) {
  server.use(http.get('/api/auth/me', () => HttpResponse.json(session)))
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
  return renderHook(() => useHasRole(allowed), { wrapper })
}

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

describe('useHasRole', () => {
  it('returns true for a user holding the role', async () => {
    const { result } = renderUseHasRole(sessionWithRoles([ROLES.Quartermaster]), [
      ROLES.Quartermaster,
    ])

    await waitFor(() => expect(result.current).toBe(true))
  })

  it('returns true for an Admin who does not hold the role directly', async () => {
    const { result } = renderUseHasRole(sessionWithRoles([ROLES.Admin]), [
      ROLES.CrewResourceOfficer,
    ])

    await waitFor(() => expect(result.current).toBe(true))
  })

  it('returns false for a signed-in user without the role', async () => {
    const { result } = renderUseHasRole(sessionWithRoles([]), [ROLES.Quartermaster])

    await waitFor(() => expect(result.current).toBe(false))
  })

  it('returns false for anonymous visitors', async () => {
    const { result } = renderUseHasRole({ authenticated: false }, [ROLES.Quartermaster])

    await waitFor(() => expect(result.current).toBe(false))
  })

  it('returns false while the session is still loading', () => {
    const { result } = renderUseHasRole(sessionWithRoles([ROLES.Admin]), [ROLES.Admin])

    expect(result.current).toBe(false)
  })
})
