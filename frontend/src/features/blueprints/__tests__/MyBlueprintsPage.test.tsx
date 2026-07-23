import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/tests/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MyBlueprintsPage } from '../pages/MyBlueprintsPage'

const BLUEPRINT_ID = '11111111-1111-1111-1111-111111111111'

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return {
    user: userEvent.setup(),
    ...render(
      <QueryClientProvider client={client}>
        <MyBlueprintsPage />
      </QueryClientProvider>,
    ),
  }
}

function seedDetailHandler() {
  server.use(
    http.get(`/api/blueprints/mine/${BLUEPRINT_ID}`, () =>
      HttpResponse.json({
        blueprintId: BLUEPRINT_ID,
        productName: 'Widget Mk1',
        type: 'Weapon',
        craftTimeSeconds: 330,
        ingredientCount: 3,
        slots: [],
      }),
    ),
  )
}

describe('MyBlueprintsPage', () => {
  it('shows empty state when API returns no blueprints', async () => {
    server.use(
      http.get('/api/blueprints/mine', () => HttpResponse.json({ blueprints: [] })),
    )
    renderPage()

    await waitFor(() => {
      expect(screen.getByText(/no blueprints yet/i)).toBeDefined()
    })
  })

  it('renders three-column table with Blueprint, Type, and Ingredients headers', async () => {
    server.use(
      http.get('/api/blueprints/mine', () =>
        HttpResponse.json({
          blueprints: [
            { blueprintId: BLUEPRINT_ID, productName: 'Widget Mk1', type: 'Weapon', ingredientCount: 3 },
          ],
        }),
      ),
    )
    renderPage()

    await waitFor(() => {
      expect(screen.getByText('Widget Mk1')).toBeDefined()
    })

    expect(screen.getByText('Blueprint')).toBeDefined()
    expect(screen.getByText('Type')).toBeDefined()
    expect(screen.getByText('Ingredients')).toBeDefined()
    expect(screen.getByText('Weapon')).toBeDefined()
    expect(screen.getByText('3')).toBeDefined()
  })

  it('shows dash for null type and product name', async () => {
    server.use(
      http.get('/api/blueprints/mine', () =>
        HttpResponse.json({
          blueprints: [
            { blueprintId: '22222222-2222-2222-2222-222222222222', productName: null, type: null, ingredientCount: 0 },
          ],
        }),
      ),
    )
    renderPage()

    await waitFor(() => {
      expect(screen.getAllByText('—').length).toBeGreaterThanOrEqual(2)
    })
  })

  it('renders Add Blueprint button', async () => {
    server.use(
      http.get('/api/blueprints/mine', () => HttpResponse.json({ blueprints: [] })),
    )
    renderPage()

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /add blueprint/i })).toBeDefined()
    })
  })

  it('clicking a blueprint row opens the detail panel', async () => {
    server.use(
      http.get('/api/blueprints/mine', () =>
        HttpResponse.json({
          blueprints: [
            { blueprintId: BLUEPRINT_ID, productName: 'Widget Mk1', type: 'Weapon', ingredientCount: 3 },
          ],
        }),
      ),
    )
    seedDetailHandler()

    const { user } = renderPage()

    await waitFor(() => screen.getByText('Widget Mk1'))
    await user.click(screen.getAllByText('Widget Mk1')[0])

    expect(screen.getByRole('dialog')).toBeDefined()
  })

  it('the detail panel closes when the X button is clicked', async () => {
    server.use(
      http.get('/api/blueprints/mine', () =>
        HttpResponse.json({
          blueprints: [
            { blueprintId: BLUEPRINT_ID, productName: 'Widget Mk1', type: 'Weapon', ingredientCount: 3 },
          ],
        }),
      ),
    )
    seedDetailHandler()

    const { user } = renderPage()

    await waitFor(() => screen.getByText('Widget Mk1'))
    await user.click(screen.getAllByText('Widget Mk1')[0])
    expect(screen.getByRole('dialog')).toBeDefined()

    await user.click(screen.getByRole('button', { name: /close/i }))
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).toBeNull()
    })
  })
})
