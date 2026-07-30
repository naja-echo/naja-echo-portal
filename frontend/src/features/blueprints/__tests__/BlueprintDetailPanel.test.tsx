import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/tests/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BlueprintDetailPanel } from '../components/BlueprintDetailPanel'

const BLUEPRINT_ID = '11111111-1111-1111-1111-111111111111'

function renderPanel(blueprintId: string | null = BLUEPRINT_ID, onClose = vi.fn()) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return {
    user: userEvent.setup(),
    onClose,
    ...render(
      <QueryClientProvider client={client}>
        <BlueprintDetailPanel blueprintId={blueprintId} subtype={null} tag={null} onClose={onClose} />
      </QueryClientProvider>,
    ),
  }
}

function seedDetail(overrides: Partial<Record<string, unknown>> = {}) {
  server.use(
    http.get(`/api/blueprints/mine/${BLUEPRINT_ID}`, () =>
      HttpResponse.json({
        blueprintId: BLUEPRINT_ID,
        productName: 'Widget Mk1',
        type: 'Weapon',
        craftTimeSeconds: 330,
        ingredientCount: 2,
        slots: [
          {
            slotIndex: 0,
            slotName: 'Cast Iron',
            options: [
              { optionIndex: 0, materialName: 'Injector Nozzles', kind: 'material', quantity: 1.72 },
            ],
          },
        ],
        ...overrides,
      }),
    ),
  )
}

describe('BlueprintDetailPanel', () => {
  // ── Summary section (US2) ──────────────────────────────────────

  it('shows blueprint name in the SheetTitle when data loads', async () => {
    seedDetail()
    renderPanel()

    await waitFor(() => {
      expect(screen.getByText('Widget Mk1')).toBeDefined()
    })
  })

  it('shows Type, Craft Time, and Ingredients labels with correct values', async () => {
    seedDetail()
    renderPanel()

    await waitFor(() => {
      expect(screen.getByText('Widget Mk1')).toBeDefined()
    })

    expect(screen.getByText('Type')).toBeDefined()
    expect(screen.getByText('Weapon')).toBeDefined()
    expect(screen.getByText('Craft Time')).toBeDefined()
    expect(screen.getByText('5m 30s')).toBeDefined()
    expect(screen.getAllByText('Ingredients').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('2')).toBeDefined()
  })

  it('shows dash for null type and null craftTimeSeconds', async () => {
    seedDetail({ type: null, craftTimeSeconds: null })
    renderPanel()

    await waitFor(() => {
      expect(screen.getByText('Widget Mk1')).toBeDefined()
    })

    expect(screen.getAllByText('—').length).toBeGreaterThanOrEqual(2)
  })

  it('formats craftTimeSeconds=330 as "5m 30s"', async () => {
    seedDetail({ craftTimeSeconds: 330 })
    renderPanel()

    await waitFor(() => {
      expect(screen.getByText('5m 30s')).toBeDefined()
    })
  })

  // ── Ingredient listing (US3) ───────────────────────────────────

  it('renders top-level slot rows (slot_name visible)', async () => {
    seedDetail()
    renderPanel()

    await waitFor(() => {
      expect(screen.getByText('Cast Iron')).toBeDefined()
    })
  })

  it('renders sub-option rows beneath the parent slot (materialName visible)', async () => {
    seedDetail()
    renderPanel()

    await waitFor(() => {
      expect(screen.getByText(/Injector Nozzles/)).toBeDefined()
    })
  })

  it('shows "No ingredients listed" when slots array is empty', async () => {
    seedDetail({ slots: [] })
    renderPanel()

    await waitFor(() => {
      expect(screen.getByText(/no ingredients listed/i)).toBeDefined()
    })
  })

  // ── Remove flow (US4) ─────────────────────────────────────────

  it('shows the Remove button', async () => {
    seedDetail()
    renderPanel()

    await waitFor(() => screen.getByText('Widget Mk1'))
    expect(screen.getByRole('button', { name: /remove/i })).toBeDefined()
  })

  it('clicking Remove shows inline confirmation', async () => {
    seedDetail()
    const { user } = renderPanel()

    await waitFor(() => screen.getByText('Widget Mk1'))
    await user.click(screen.getByRole('button', { name: /^remove$/i }))

    expect(screen.getByText(/remove this blueprint\?/i)).toBeDefined()
    expect(screen.getByRole('button', { name: /confirm/i })).toBeDefined()
    expect(screen.getByRole('button', { name: /cancel/i })).toBeDefined()
  })

  it('clicking Cancel hides confirmation and panel stays open', async () => {
    seedDetail()
    const { user } = renderPanel()

    await waitFor(() => screen.getByText('Widget Mk1'))
    await user.click(screen.getByRole('button', { name: /^remove$/i }))
    await user.click(screen.getByRole('button', { name: /cancel/i }))

    expect(screen.queryByText(/remove this blueprint\?/i)).toBeNull()
    expect(screen.getByRole('dialog')).toBeDefined()
  })

  it('successful delete calls onClose', async () => {
    seedDetail()
    server.use(
      http.delete(`/api/blueprints/mine/${BLUEPRINT_ID}`, () => new HttpResponse(null, { status: 204 })),
    )
    const { user, onClose } = renderPanel()

    await waitFor(() => screen.getByText('Widget Mk1'))
    await user.click(screen.getByRole('button', { name: /^remove$/i }))
    await user.click(screen.getByRole('button', { name: /confirm/i }))

    await waitFor(() => {
      expect(onClose).toHaveBeenCalled()
    })
  })
})
