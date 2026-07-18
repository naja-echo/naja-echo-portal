import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/tests/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BlueprintsImportTab } from '../components/BlueprintsImportTab'

const validDataset = {
  version: '1.4.0',
  meta: { totalBlueprints: 1, totalProducts: 1, totalResources: 1, totalItems: 0 },
  dismantle: { efficiency: 0.5, dismantleTimeSeconds: 60, blacklistedResources: [], blacklistedEntityClasses: [] },
  properties: {},
  resources: ['Steel'],
  items: [],
  blueprints: [{ guid: '11111111-1111-1111-1111-111111111111', tag: 'BP', productEntityClass: '22222222-2222-2222-2222-222222222222', gear: 'Weapon', type: null, subtype: null, productName: 'Widget', manufacturer: 'ACME', tiers: [] }],
}

const importResponse = {
  version: '1.4.0',
  blueprints: { read: 1, inserted: 1, updated: 0, rejected: 0 },
  resources: { read: 1, inserted: 1, updated: 0, rejected: 0 },
  items: { read: 0, inserted: 0, updated: 0, rejected: 0 },
  properties: { read: 0, inserted: 0, updated: 0, rejected: 0 },
  referenceDataReplaced: true,
  warnings: ["material 'Steel' matched no item; stored unlinked."],
  rejections: [{ guid: '99999999-9999-9999-9999-999999999999', productName: 'Bad', reason: 'guid is missing or not a valid UUID.' }],
}

function renderTab() {
  server.use(http.get('/api/admin/blueprints', () => HttpResponse.json({ blueprints: [] })))
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return {
    user: userEvent.setup(),
    ...render(
      <QueryClientProvider client={client}>
        <BlueprintsImportTab />
      </QueryClientProvider>,
    ),
  }
}

function jsonFile(content: unknown, name = 'dataset.json') {
  return new File([JSON.stringify(content)], name, { type: 'application/json' })
}

describe('BlueprintsImportTab', () => {
  it('renders the upload control', () => {
    renderTab()
    expect(screen.getByLabelText(/select blueprint dataset json file/i)).toBeDefined()
  })

  it('uploads a valid file and renders the result summary', async () => {
    server.use(http.post('/api/admin/blueprints/import', () => HttpResponse.json(importResponse)))
    const { user } = renderTab()

    await user.upload(screen.getByLabelText(/select blueprint dataset json file/i), jsonFile(validDataset))

    await waitFor(() => {
      expect(screen.getByRole('status', { name: /import result summary/i })).toBeDefined()
    })
    const summary = screen.getByRole('status', { name: /import result summary/i })
    expect(summary.textContent).toContain('1.4.0')
    expect(summary.textContent).toContain('Reference data replaced')
    expect(summary.textContent).toContain('Warnings')
    expect(summary.textContent).toContain('Rejected entries')
  })

  it('rejects an oversize file client-side without a network call', async () => {
    const { user } = renderTab()
    const bigFile = jsonFile(validDataset)
    Object.defineProperty(bigFile, 'size', { value: 51 * 1024 * 1024 })

    await user.upload(screen.getByLabelText(/select blueprint dataset json file/i), bigFile)

    await waitFor(() => {
      expect(screen.getByRole('alert').textContent).toContain('50 MB')
    })
  })

  it('rejects an invalid JSON file client-side', async () => {
    const { user } = renderTab()
    const badFile = new File(['{ not json'], 'bad.json', { type: 'application/json' })

    await user.upload(screen.getByLabelText(/select blueprint dataset json file/i), badFile)

    await waitFor(() => {
      expect(screen.getByRole('alert').textContent).toMatch(/invalid json/i)
    })
  })

  it('rejects a file whose shape is not a dataset document', async () => {
    const { user } = renderTab()

    await user.upload(screen.getByLabelText(/select blueprint dataset json file/i), jsonFile({ foo: 'bar' }))

    await waitFor(() => {
      expect(screen.getByRole('alert').textContent).toMatch(/unexpected file format/i)
    })
  })
})
