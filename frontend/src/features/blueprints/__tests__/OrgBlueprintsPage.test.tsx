import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/tests/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { OrgBlueprintsPage } from '../pages/OrgBlueprintsPage'

const BLUEPRINT_ID = '11111111-1111-1111-1111-111111111111'

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return {
    user: userEvent.setup(),
    ...render(
      <QueryClientProvider client={client}>
        <OrgBlueprintsPage />
      </QueryClientProvider>,
    ),
  }
}

describe('OrgBlueprintsPage', () => {
  it('shows empty state when API returns no blueprints', async () => {
    server.use(
      http.get('/api/blueprints/org', () => HttpResponse.json({ blueprints: [] })),
    )
    renderPage()

    await waitFor(() => {
      expect(screen.getByText(/no blueprints found in your org/i)).toBeDefined()
    })
  })

  it('renders a table of blueprints returned by the API', async () => {
    server.use(
      http.get('/api/blueprints/org', () =>
        HttpResponse.json({
          blueprints: [
            { blueprintId: BLUEPRINT_ID, productName: 'Widget Mk1', type: 'Weapon', subtype: null, gear: null, ingredientCount: 3 },
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

  it('shows filter bar when blueprints are loaded', async () => {
    server.use(
      http.get('/api/blueprints/org', () =>
        HttpResponse.json({
          blueprints: [
            { blueprintId: BLUEPRINT_ID, productName: 'Widget Mk1', type: 'Weapon', subtype: 'Pistol', gear: null, ingredientCount: 3 },
          ],
        }),
      ),
    )
    renderPage()

    await waitFor(() => screen.getByText('Widget Mk1'))
    expect(screen.getByPlaceholderText(/filter by name/i)).toBeDefined()
    expect(screen.getByRole('combobox', { name: /category/i })).toBeDefined()
  })

  it('filtering by category hides non-matching rows', async () => {
    server.use(
      http.get('/api/blueprints/org', () =>
        HttpResponse.json({
          blueprints: [
            { blueprintId: BLUEPRINT_ID, productName: 'Widget Mk1', type: 'Weapon', subtype: 'Pistol', gear: null, ingredientCount: 3 },
            { blueprintId: '22222222-2222-2222-2222-222222222222', productName: 'Hull Panel', type: 'Ship', subtype: null, gear: null, ingredientCount: 1 },
          ],
        }),
      ),
    )
    const { user } = renderPage()

    await waitFor(() => screen.getByText('Widget Mk1'))
    await user.click(screen.getByRole('combobox', { name: /category/i }))
    await user.click(screen.getByText('Weapon'))

    await waitFor(() => {
      expect(screen.queryByText('Hull Panel')).toBeNull()
    })
    expect(screen.getByText('Widget Mk1')).toBeDefined()
  })

  it('shows filtered-empty state when no blueprints match', async () => {
    server.use(
      http.get('/api/blueprints/org', () =>
        HttpResponse.json({
          blueprints: [
            { blueprintId: BLUEPRINT_ID, productName: 'Widget Mk1', type: 'Weapon', subtype: 'Pistol', gear: null, ingredientCount: 3 },
          ],
        }),
      ),
    )
    const { user } = renderPage()

    await waitFor(() => screen.getByText('Widget Mk1'))
    await user.type(screen.getByPlaceholderText(/filter by name/i), 'zzz')

    await waitFor(() => {
      expect(screen.getByText(/no blueprints match/i)).toBeDefined()
    })
  })

  it('clicking a blueprint row opens a panel', async () => {
    server.use(
      http.get('/api/blueprints/org', () =>
        HttpResponse.json({
          blueprints: [
            { blueprintId: BLUEPRINT_ID, productName: 'Widget Mk1', type: 'Weapon', subtype: null, gear: null, ingredientCount: 3 },
          ],
        }),
      ),
      http.get(`/api/blueprints/org/${BLUEPRINT_ID}`, () =>
        HttpResponse.json({
          blueprintId: BLUEPRINT_ID,
          productName: 'Widget Mk1',
          type: 'Weapon',
          craftTimeSeconds: 330,
          ingredientCount: 3,
          slots: [],
          owners: [],
        }),
      ),
    )

    const { user } = renderPage()

    await waitFor(() => screen.getByText('Widget Mk1'))
    await user.click(screen.getAllByText('Widget Mk1')[0])

    expect(screen.getByRole('dialog')).toBeDefined()
  })
})
