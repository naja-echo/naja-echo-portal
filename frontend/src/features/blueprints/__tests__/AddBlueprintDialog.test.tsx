import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/tests/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AddBlueprintDialog } from '../components/AddBlueprintDialog'

function renderDialog(onClose = vi.fn()) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return {
    user: userEvent.setup(),
    onClose,
    ...render(
      <QueryClientProvider client={client}>
        <AddBlueprintDialog open onClose={onClose} />
      </QueryClientProvider>,
    ),
  }
}

describe('AddBlueprintDialog', () => {
  it('renders with Add Blueprint button disabled initially', () => {
    server.use(http.get('/api/blueprints/search', () => HttpResponse.json({ results: [] })))
    renderDialog()

    const btn = screen.getByRole('button', { name: /add blueprint/i })
    expect(btn.hasAttribute('disabled')).toBe(true)
  })

  it('typing in search input triggers search and shows results', async () => {
    server.use(
      http.get('/api/blueprints/search', () =>
        HttpResponse.json({
          results: [{ blueprintId: '11111111-1111-1111-1111-111111111111', productName: 'Widget Mk1', type: 'Weapon' }],
        }),
      ),
    )
    const { user } = renderDialog()

    await user.type(screen.getByPlaceholderText(/search blueprints/i), 'widget')

    await waitFor(() => {
      expect(screen.getByText('Widget Mk1')).toBeDefined()
    })
  })

  it('selecting a result enables the Add Blueprint button', async () => {
    server.use(
      http.get('/api/blueprints/search', () =>
        HttpResponse.json({
          results: [{ blueprintId: '11111111-1111-1111-1111-111111111111', productName: 'Widget Mk1', type: 'Weapon' }],
        }),
      ),
    )
    const { user } = renderDialog()

    await user.type(screen.getByPlaceholderText(/search blueprints/i), 'widget')
    await waitFor(() => screen.getByText('Widget Mk1'))
    await user.click(screen.getByText('Widget Mk1'))

    const btn = screen.getByRole('button', { name: /add blueprint/i })
    expect(btn.hasAttribute('disabled')).toBe(false)
  })

  it('successful submit calls onClose', async () => {
    server.use(
      http.get('/api/blueprints/search', () =>
        HttpResponse.json({
          results: [{ blueprintId: '11111111-1111-1111-1111-111111111111', productName: 'Widget Mk1', type: null }],
        }),
      ),
      http.post('/api/blueprints/mine', () =>
        HttpResponse.json(
          { blueprintId: '11111111-1111-1111-1111-111111111111', productName: 'Widget Mk1', type: null, ingredientCount: 2 },
          { status: 201 },
        ),
      ),
    )
    const { user, onClose } = renderDialog()

    await user.type(screen.getByPlaceholderText(/search blueprints/i), 'widget')
    await waitFor(() => screen.getByText('Widget Mk1'))
    await user.click(screen.getByText('Widget Mk1'))
    await user.click(screen.getByRole('button', { name: /add blueprint/i }))

    await waitFor(() => {
      expect(onClose).toHaveBeenCalled()
    })
  })

  it('shows inline duplicate message and keeps modal open on 409', async () => {
    server.use(
      http.get('/api/blueprints/search', () =>
        HttpResponse.json({
          results: [{ blueprintId: '11111111-1111-1111-1111-111111111111', productName: 'Widget Mk1', type: null }],
        }),
      ),
      http.post('/api/blueprints/mine', () =>
        HttpResponse.json({ title: 'Conflict' }, { status: 409 }),
      ),
    )
    const { user, onClose } = renderDialog()

    await user.type(screen.getByPlaceholderText(/search blueprints/i), 'widget')
    await waitFor(() => screen.getByText('Widget Mk1'))
    await user.click(screen.getByText('Widget Mk1'))
    await user.click(screen.getByRole('button', { name: /add blueprint/i }))

    await waitFor(() => {
      expect(screen.getByText(/already in your list/i)).toBeDefined()
    })
    expect(onClose).not.toHaveBeenCalled()
  })
})
