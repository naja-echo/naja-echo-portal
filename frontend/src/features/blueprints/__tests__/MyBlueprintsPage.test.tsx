import { render, screen, waitFor } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/tests/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MyBlueprintsPage } from '../pages/MyBlueprintsPage'

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MyBlueprintsPage />
    </QueryClientProvider>,
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
            { blueprintId: '11111111-1111-1111-1111-111111111111', productName: 'Widget Mk1', type: 'Weapon', ingredientCount: 3 },
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
})
