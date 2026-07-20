import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect } from 'vitest'
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

const regularSession = {
  authenticated: true as const,
  user: {
    id: 'b1ffcd00-0d1c-4ef8-bb6d-6bb9bd380a22',
    displayName: 'Regular User',
    discordUsername: 'regularuser',
    roles: [],
  },
}

const sampleUsers = [
  {
    id: 'a0000001-0000-4000-a000-000000000001',
    authName: 'alice',
    roles: ['Admin'],
    characters: [
      { id: 'c0000001-0000-4000-a000-000000000001', name: 'AliceChar', handle: 'alicehandle' },
    ],
  },
  {
    id: 'a0000002-0000-4000-a000-000000000002',
    authName: 'bob',
    roles: ['Quartermaster'],
    characters: [],
  },
  {
    id: 'a0000003-0000-4000-a000-000000000003',
    authName: 'charlie',
    roles: [],
    characters: [],
  },
]

function renderUsersPage(session = adminSession, initialRoute = '/dashboard/admin/users') {
  server.use(http.get('/api/auth/me', () => HttpResponse.json(session)))
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return {
    user: userEvent.setup(),
    ...render(
      <QueryClientProvider client={client}>
        <MemoryRouter initialEntries={[initialRoute]}>
          <Routes>
            <Route path="/dashboard" element={<div>Dashboard</div>} />
            <Route element={<RoleRoute allow={[ROLES.Admin]} />}>
              <Route path="/dashboard/admin/users" element={<AdminUsersPage />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    ),
  }
}

describe('AdminUsersPage — table rendering', () => {
  it('renders one row per member with auth name', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    renderUsersPage()
    await waitFor(() => {
      expect(screen.getByText('alice')).toBeDefined()
      expect(screen.getByText('bob')).toBeDefined()
      expect(screen.getByText('charlie')).toBeDefined()
    })
  })

  it('renders friendly role label for Admin', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    renderUsersPage()
    await waitFor(() => {
      expect(screen.getByText('Administrator')).toBeDefined()
    })
  })

  it('renders character name and handle', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    renderUsersPage()
    await waitFor(() => {
      expect(screen.getByText('AliceChar (alicehandle)')).toBeDefined()
    })
  })

  it('shows empty state cell for member with no characters', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    renderUsersPage()
    await waitFor(() => {
      expect(screen.getAllByText('—').length).toBeGreaterThan(0)
    })
  })
})

describe('AdminUsersPage — filtering', () => {
  it('filters by auth name', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    const { user } = renderUsersPage()
    await waitFor(() => expect(screen.getByText('alice')).toBeDefined())

    const input = screen.getByPlaceholderText(/filter/i)
    await user.type(input, 'alice')

    await waitFor(() => {
      expect(screen.getByText('alice')).toBeDefined()
      expect(screen.queryByText('bob')).toBeNull()
    })
  })

  it('filters by character handle', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    const { user } = renderUsersPage()
    await waitFor(() => expect(screen.getByText('alice')).toBeDefined())

    const input = screen.getByPlaceholderText(/filter/i)
    await user.type(input, 'alicehandle')

    await waitFor(() => {
      expect(screen.getByText('alice')).toBeDefined()
      expect(screen.queryByText('bob')).toBeNull()
    })
  })

  it('filters by role (friendly name)', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    const { user } = renderUsersPage()
    await waitFor(() => expect(screen.getByText('alice')).toBeDefined())

    const input = screen.getByPlaceholderText(/filter/i)
    await user.type(input, 'Administrator')

    await waitFor(() => {
      expect(screen.getByText('alice')).toBeDefined()
      expect(screen.queryByText('bob')).toBeNull()
    })
  })

  it('shows zero-results empty-state message when filter matches nothing', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    const { user } = renderUsersPage()
    await waitFor(() => expect(screen.getByText('alice')).toBeDefined())

    const input = screen.getByPlaceholderText(/filter/i)
    await user.type(input, 'zzznonexistent')

    await waitFor(() => {
      expect(screen.getByText(/no members found/i)).toBeDefined()
    })
  })
})

describe('AdminUsersPage — access control', () => {
  it('non-admin navigating to /dashboard/admin/users gets an unauthorized state', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    renderUsersPage(regularSession)
    await waitFor(() => {
      expect(screen.getByText(/don't have access to this page/i)).toBeDefined()
    })
    expect(screen.queryByText('alice')).toBeNull()
  })
})

describe('AdminUsersPage — Add Character dialog', () => {
  it('shows inline error for blank handle before any fetch', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    const { user } = renderUsersPage()
    await waitFor(() => expect(screen.getByText('alice')).toBeDefined())

    const addButtons = screen.getAllByRole('button', { name: /add character/i })
    await user.click(addButtons[0])

    const submitButton = screen.getByRole('button', { name: /add/i })
    await user.click(submitButton)

    await waitFor(() => {
      expect(screen.getByText(/handle is required/i)).toBeDefined()
    })
  })

  it('closes dialog after successful add', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    const { user } = renderUsersPage()
    await waitFor(() => expect(screen.getByText('alice')).toBeDefined())

    server.use(
      http.post('/api/admin/users/:userId/characters', () =>
        HttpResponse.json(
          { id: 'c0000002-0000-4000-a000-000000000002', name: 'NewCharName', handle: 'newcharhandle' },
          { status: 201 }
        )
      )
    )

    const addButtons = screen.getAllByRole('button', { name: /add character/i })
    await user.click(addButtons[0])

    const handleInput = screen.getByPlaceholderText(/rsi handle/i)
    await user.type(handleInput, 'newcharhandle')

    const submitButton = screen.getByRole('button', { name: /^add$/i })
    await user.click(submitButton)

    await waitFor(() => {
      expect(screen.queryByPlaceholderText(/rsi handle/i)).toBeNull()
    })
  })

  it('shows already-claimed message on 409', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    const { user } = renderUsersPage()
    await waitFor(() => expect(screen.getByText('alice')).toBeDefined())

    server.use(
      http.post('/api/admin/users/:userId/characters', () =>
        HttpResponse.json({ title: 'Already claimed', status: 409 }, { status: 409 })
      )
    )

    const addButtons = screen.getAllByRole('button', { name: /add character/i })
    await user.click(addButtons[0])
    const handleInput = screen.getByPlaceholderText(/rsi handle/i)
    await user.type(handleInput, 'claimedhandle')
    const submitButton = screen.getByRole('button', { name: /^add$/i })
    await user.click(submitButton)

    await waitFor(() => {
      expect(screen.getByText(/already claimed/i)).toBeDefined()
    })
  })

  it('shows handle-not-found error for 404 with RSI type', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    const { user } = renderUsersPage()
    await waitFor(() => expect(screen.getByText('alice')).toBeDefined())

    server.use(
      http.post('/api/admin/users/:userId/characters', () =>
        HttpResponse.json(
          { title: 'RSI handle not found.', status: 404, type: 'urn:najaecho:error:rsi-handle-not-found' },
          { status: 404 }
        )
      )
    )

    const addButtons = screen.getAllByRole('button', { name: /add character/i })
    await user.click(addButtons[0])
    const handleInput = screen.getByPlaceholderText(/rsi handle/i)
    await user.type(handleInput, 'unknownhandle')
    const submitButton = screen.getByRole('button', { name: /^add$/i })
    await user.click(submitButton)

    await waitFor(() => {
      expect(screen.getByText(/handle not found/i)).toBeDefined()
    })
  })

  it('shows RSI unreachable error on 502', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    const { user } = renderUsersPage()
    await waitFor(() => expect(screen.getByText('alice')).toBeDefined())

    server.use(
      http.post('/api/admin/users/:userId/characters', () =>
        HttpResponse.json({ title: 'Unreachable', status: 502 }, { status: 502 })
      )
    )

    const addButtons = screen.getAllByRole('button', { name: /add character/i })
    await user.click(addButtons[0])
    const handleInput = screen.getByPlaceholderText(/rsi handle/i)
    await user.type(handleInput, 'somehandle')
    const submitButton = screen.getByRole('button', { name: /^add$/i })
    await user.click(submitButton)

    await waitFor(() => {
      expect(screen.getByText(/rsi could not be reached/i)).toBeDefined()
    })
  })

  it('shows name-not-extractable error on 422', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    const { user } = renderUsersPage()
    await waitFor(() => expect(screen.getByText('alice')).toBeDefined())

    server.use(
      http.post('/api/admin/users/:userId/characters', () =>
        HttpResponse.json(
          {
            title: 'Name not extractable',
            status: 422,
            detail: 'Character name could not be retrieved',
          },
          { status: 422 }
        )
      )
    )

    const addButtons = screen.getAllByRole('button', { name: /add character/i })
    await user.click(addButtons[0])
    const handleInput = screen.getByPlaceholderText(/rsi handle/i)
    await user.type(handleInput, 'somehandle')
    const submitButton = screen.getByRole('button', { name: /^add$/i })
    await user.click(submitButton)

    await waitFor(() => {
      expect(screen.getByText(/character name could not be retrieved/i)).toBeDefined()
    })
  })
})

describe('AdminUsersPage — Assign Roles dialog', () => {
  it('opens dialog with current roles pre-checked', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    const { user } = renderUsersPage()
    await waitFor(() => expect(screen.getByText('alice')).toBeDefined())

    const assignButtons = screen.getAllByRole('button', { name: /assign roles/i })
    await user.click(assignButtons[0])

    await waitFor(() => {
      const adminCheckbox = screen.getByRole('checkbox', { name: /administrator/i })
      expect(adminCheckbox.getAttribute('aria-checked')).toBe('true')
      const quartermasterCheckbox = screen.getByRole('checkbox', { name: /quartermaster/i })
      expect(quartermasterCheckbox.getAttribute('aria-checked')).toBe('false')
    })
  })

  it('submits selected roles via PUT and closes dialog on success', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    let capturedBody: unknown = null
    server.use(
      http.put('/api/admin/users/:userId/roles', async ({ request }) => {
        capturedBody = await request.json()
        return new HttpResponse(null, { status: 204 })
      })
    )

    const { user } = renderUsersPage()
    await waitFor(() => expect(screen.getByText('alice')).toBeDefined())

    const assignButtons = screen.getAllByRole('button', { name: /assign roles/i })
    await user.click(assignButtons[1])

    await waitFor(() => expect(screen.getByRole('checkbox', { name: /administrator/i })).toBeDefined())

    const adminCheckbox = screen.getByRole('checkbox', { name: /administrator/i })
    await user.click(adminCheckbox)

    const saveButton = screen.getByRole('button', { name: /save/i })
    await user.click(saveButton)

    await waitFor(() => {
      expect(screen.queryByRole('button', { name: /save/i })).toBeNull()
      expect((capturedBody as { roles: string[] }).roles).toContain('Admin')
    })
  })

  it('shows error message on API failure', async () => {
    server.use(http.get('/api/admin/users', () => HttpResponse.json({ users: sampleUsers })))
    server.use(
      http.put('/api/admin/users/:userId/roles', () =>
        HttpResponse.json({ title: 'User not found.', status: 404 }, { status: 404 })
      )
    )

    const { user } = renderUsersPage()
    await waitFor(() => expect(screen.getByText('alice')).toBeDefined())

    const assignButtons = screen.getAllByRole('button', { name: /assign roles/i })
    await user.click(assignButtons[0])

    await waitFor(() => expect(screen.getByRole('button', { name: /save/i })).toBeDefined())

    const saveButton = screen.getByRole('button', { name: /save/i })
    await user.click(saveButton)

    await waitFor(() => {
      expect(screen.getByText(/user not found/i)).toBeDefined()
    })
  })
})
