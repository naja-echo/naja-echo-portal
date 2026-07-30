import { render, screen, waitFor } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/tests/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { OrgBlueprintDetailPanel } from '../components/OrgBlueprintDetailPanel'

const BLUEPRINT_ID = '11111111-1111-1111-1111-111111111111'

function renderPanel(blueprintId: string | null = BLUEPRINT_ID) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <OrgBlueprintDetailPanel blueprintId={blueprintId} subtype={null} tag={null} onClose={() => {}} />
    </QueryClientProvider>,
  )
}

describe('OrgBlueprintDetailPanel', () => {
  it('shows blueprint name in SheetTitle', async () => {
    server.use(
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
    renderPanel()

    await waitFor(() => {
      expect(screen.getByText('Widget Mk1')).toBeDefined()
    })
  })

  it('shows Type / Craft Time / Ingredients labels and values', async () => {
    server.use(
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
    renderPanel()

    await waitFor(() => screen.getByText('Widget Mk1'))

    expect(screen.getByText('Type')).toBeDefined()
    expect(screen.getByText('Weapon')).toBeDefined()
    expect(screen.getByText('Craft Time')).toBeDefined()
    expect(screen.getByText('5m 30s')).toBeDefined()
    expect(screen.getAllByText('Ingredients').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('3')).toBeDefined()
  })

  it('shows dash for null type and null craftTimeSeconds', async () => {
    server.use(
      http.get(`/api/blueprints/org/${BLUEPRINT_ID}`, () =>
        HttpResponse.json({
          blueprintId: BLUEPRINT_ID,
          productName: 'Widget Mk1',
          type: null,
          craftTimeSeconds: null,
          ingredientCount: 0,
          slots: [],
          owners: [],
        }),
      ),
    )
    renderPanel()

    await waitFor(() => screen.getByText('Widget Mk1'))

    expect(screen.getAllByText('—').length).toBeGreaterThanOrEqual(2)
  })

  it('formats craftTimeSeconds=330 as "5m 30s"', async () => {
    server.use(
      http.get(`/api/blueprints/org/${BLUEPRINT_ID}`, () =>
        HttpResponse.json({
          blueprintId: BLUEPRINT_ID,
          productName: 'Widget',
          type: null,
          craftTimeSeconds: 330,
          ingredientCount: 0,
          slots: [],
          owners: [],
        }),
      ),
    )
    renderPanel()

    await waitFor(() => screen.getByText('Widget'))
    expect(screen.getByText('5m 30s')).toBeDefined()
  })

  it('does not show a Remove button', async () => {
    server.use(
      http.get(`/api/blueprints/org/${BLUEPRINT_ID}`, () =>
        HttpResponse.json({
          blueprintId: BLUEPRINT_ID,
          productName: 'Widget Mk1',
          type: null,
          craftTimeSeconds: null,
          ingredientCount: 0,
          slots: [],
          owners: [],
        }),
      ),
    )
    renderPanel()

    await waitFor(() => screen.getByText('Widget Mk1'))
    expect(screen.queryByRole('button', { name: /remove/i })).toBeNull()
  })

  it('renders "Who has this blueprint?" heading and each owner displayName', async () => {
    server.use(
      http.get(`/api/blueprints/org/${BLUEPRINT_ID}`, () =>
        HttpResponse.json({
          blueprintId: BLUEPRINT_ID,
          productName: 'Widget Mk1',
          type: null,
          craftTimeSeconds: null,
          ingredientCount: 0,
          slots: [],
          owners: [
            { userId: 'aaaaaaaa-0000-0000-0000-000000000001', displayName: 'Alice' },
            { userId: 'aaaaaaaa-0000-0000-0000-000000000002', displayName: 'Bob' },
          ],
        }),
      ),
    )
    renderPanel()

    await waitFor(() => screen.getByText('Widget Mk1'))

    expect(screen.getByText('Who has this blueprint?')).toBeDefined()
    expect(screen.getByText('Alice')).toBeDefined()
    expect(screen.getByText('Bob')).toBeDefined()
  })

  it('shows placeholder text when owners array is empty', async () => {
    server.use(
      http.get(`/api/blueprints/org/${BLUEPRINT_ID}`, () =>
        HttpResponse.json({
          blueprintId: BLUEPRINT_ID,
          productName: 'Widget Mk1',
          type: null,
          craftTimeSeconds: null,
          ingredientCount: 0,
          slots: [],
          owners: [],
        }),
      ),
    )
    renderPanel()

    await waitFor(() => screen.getByText('Widget Mk1'))

    expect(screen.getByText('No members currently have this blueprint.')).toBeDefined()
  })
})
