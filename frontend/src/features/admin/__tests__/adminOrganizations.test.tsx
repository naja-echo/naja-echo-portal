import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/tests/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { RoleRoute } from '@/features/auth/RoleRoute'
import { ROLES } from '@/features/auth/lib/roles'
import { AdminUsersPage } from '../pages/AdminUsersPage'

const adminSession = {
  authenticated: true as const,
  user: {
    id: 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
    displayName: 'Admin User',
    discordUsername: 'adminuser',
    roles: ['Admin'],
  },
}

const najaEcho = { id: '9b8ac811-3cec-421c-8cfb-cc56f775ad5a', name: 'Naja Echo' }

const sampleUsers = [
  {
    id: 'a0000001-0000-4000-a000-000000000001',
    authName: 'alice',
    roles: ['Admin'],
    characters: [],
    organization: najaEcho,
  },
  {
    id: 'a0000002-0000-4000-a000-000000000002',
    authName: 'bob',
    roles: [],
    characters: [],
    organization: null,
  },
]

function mockEndpoints(users = sampleUsers) {
  server.use(
    http.get('/api/auth/me', () => HttpResponse.json(adminSession)),
    http.get('/api/admin/users', () => HttpResponse.json({ users })),
    http.get('/api/admin/organizations', () =>
      HttpResponse.json({ organizations: [najaEcho] })),
  )
}

function renderUsersPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return {
    user: userEvent.setup(),
    ...render(
      <QueryClientProvider client={client}>
        <MemoryRouter initialEntries={['/dashboard/admin/users']}>
          <Routes>
            <Route element={<RoleRoute allow={[ROLES.Admin]} />}>
              <Route path="/dashboard/admin/users" element={<AdminUsersPage />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    ),
  }
}

/** The table row for a member, so assertions do not accidentally match another row's cells. */
async function rowFor(authName: string) {
  const cell = await screen.findByText(authName)
  return cell.closest('tr') as HTMLElement
}

describe('Members page — Organization column (FR-011)', () => {
  it('shows the organization name for an assigned member', async () => {
    mockEndpoints()
    renderUsersPage()

    const row = await rowFor('alice')
    expect(within(row).getByText('Naja Echo')).toBeDefined()
  })

  it('shows an em-dash for a member belonging to no organization', async () => {
    mockEndpoints()
    renderUsersPage()

    const row = await rowFor('bob')
    expect(within(row).getAllByText('—').length).toBeGreaterThan(0)
  })

  it('filters by organization name', async () => {
    mockEndpoints()
    const { user } = renderUsersPage()
    await rowFor('alice')

    await user.type(screen.getByPlaceholderText(/filter/i), 'naja')

    await waitFor(() => {
      expect(screen.getByText('alice')).toBeDefined()
      expect(screen.queryByText('bob')).toBeNull()
    })
  })
})

describe('Members page — assigning an organization (FR-012)', () => {
  it('saves the selected organization and refreshes the list', async () => {
    const assigned = vi.fn()
    mockEndpoints()
    server.use(
      http.put('/api/admin/users/:userId/organization', async ({ request, params }) => {
        assigned({ userId: params.userId, body: await request.json() })
        return new HttpResponse(null, { status: 204 })
      })
    )

    const { user } = renderUsersPage()
    const row = await rowFor('bob')

    await user.click(within(row).getByRole('button', { name: /assign organization/i }))

    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByLabelText(/organization/i))
    await user.click(await screen.findByRole('option', { name: 'Naja Echo' }))
    await user.click(within(dialog).getByRole('button', { name: /save/i }))

    await waitFor(() => {
      expect(assigned).toHaveBeenCalledWith({
        userId: 'a0000002-0000-4000-a000-000000000002',
        body: { organizationId: najaEcho.id },
      })
    })
  })

  it('sends a null organizationId when clearing', async () => {
    const assigned = vi.fn()
    mockEndpoints()
    server.use(
      http.put('/api/admin/users/:userId/organization', async ({ request }) => {
        assigned(await request.json())
        return new HttpResponse(null, { status: 204 })
      })
    )

    const { user } = renderUsersPage()
    const row = await rowFor('alice')

    await user.click(within(row).getByRole('button', { name: /assign organization/i }))

    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByLabelText(/organization/i))
    await user.click(await screen.findByRole('option', { name: /no organization/i }))
    await user.click(within(dialog).getByRole('button', { name: /save/i }))

    await waitFor(() => {
      expect(assigned).toHaveBeenCalledWith({ organizationId: null })
    })
  })

  it('surfaces a readable message when the server returns 404', async () => {
    mockEndpoints()
    server.use(
      http.put('/api/admin/users/:userId/organization', () =>
        HttpResponse.json(
          { title: 'Organization not found.', status: 404 },
          { status: 404, headers: { 'Content-Type': 'application/problem+json' } }
        ))
    )

    const { user } = renderUsersPage()
    const row = await rowFor('bob')

    await user.click(within(row).getByRole('button', { name: /assign organization/i }))

    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByLabelText(/organization/i))
    await user.click(await screen.findByRole('option', { name: 'Naja Echo' }))
    await user.click(within(dialog).getByRole('button', { name: /save/i }))

    expect(await within(dialog).findByText(/not found/i)).toBeDefined()
  })

  it('disables Save when the organization list fails to load', async () => {
    const assigned = vi.fn()
    mockEndpoints()
    server.use(
      http.get('/api/admin/organizations', () => new HttpResponse(null, { status: 500 })),
      http.put('/api/admin/users/:userId/organization', () => {
        assigned()
        return new HttpResponse(null, { status: 204 })
      })
    )

    const { user } = renderUsersPage()
    const row = await rowFor('alice')

    await user.click(within(row).getByRole('button', { name: /assign organization/i }))

    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByText(/failed to load organizations/i)).toBeDefined()

    // With no Select rendered, an enabled Save would submit the initial selection and close the
    // dialog as though the admin's intended change had been saved.
    const save = within(dialog).getByRole('button', { name: /save/i })
    expect(save).toBeDisabled()

    await user.click(save)
    expect(assigned).not.toHaveBeenCalled()
  })

  it('offers an explicit no-organization option, and no way to create one (FR-015)', async () => {
    mockEndpoints()
    const { user } = renderUsersPage()
    const row = await rowFor('bob')

    await user.click(within(row).getByRole('button', { name: /assign organization/i }))

    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByLabelText(/organization/i))

    const options = await screen.findAllByRole('option')
    expect(options).toHaveLength(2)
    expect(options[0].textContent).toMatch(/no organization/i)
    expect(options[1].textContent).toBe('Naja Echo')

    // This release ships one organization and no creation path.
    expect(within(dialog).queryByRole('button', { name: /create|new organization/i })).toBeNull()
  })
})
