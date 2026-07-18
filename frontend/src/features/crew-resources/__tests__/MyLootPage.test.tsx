import { render, screen, waitFor } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/tests/server'
import { createWrapper } from '@/tests/testUtils'
import { MyLootPage } from '../pages/MyLootPage'

const emptyLedger = {
  memberId: 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
  displayName: 'Test User',
  orgPoints: [],
  lootPoints: [],
  orgPointsTotal: 0,
  lootPointsTotal: 0,
  claimPriority: 0,
}

const populatedLedger = {
  memberId: 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
  displayName: 'Test User',
  orgPoints: [
    {
      id: 'e0eebc99-9c0b-4ef8-bb6d-6bb9bd380a55',
      amount: 100,
      reason: 'Event participation',
      postedBy: 'Admin User',
      createdAt: '2026-06-01T12:00:00Z',
    },
  ],
  lootPoints: [
    {
      id: 'f0eebc99-9c0b-4ef8-bb6d-6bb9bd380a66',
      amount: 50,
      reason: 'Ship loot awarded',
      postedBy: 'QM User',
      createdAt: '2026-06-02T12:00:00Z',
    },
  ],
  orgPointsTotal: 100,
  lootPointsTotal: 50,
  claimPriority: 2.0,
}

describe('MyLootPage', () => {
  it('renders two LedgerTable sections and ClaimPriorityBadge with data', async () => {
    server.use(
      http.get('/api/loot/me', () => HttpResponse.json(populatedLedger))
    )
    render(<MyLootPage />, { wrapper: createWrapper() })

    await waitFor(() => expect(screen.getByText('My Loot')).toBeDefined())

    expect(screen.getByText('Org Points')).toBeDefined()
    expect(screen.getByText('Loot Points')).toBeDefined()
    expect(screen.getByText('Event participation')).toBeDefined()
    expect(screen.getByText('Ship loot awarded')).toBeDefined()
    expect(screen.getByText('2.00')).toBeDefined()
  })

  it('shows 0.00 Claim Priority and empty-state messages for new member', async () => {
    server.use(
      http.get('/api/loot/me', () => HttpResponse.json(emptyLedger))
    )
    render(<MyLootPage />, { wrapper: createWrapper() })

    await waitFor(() => expect(screen.getByText('My Loot')).toBeDefined())

    expect(screen.getByText('0.00')).toBeDefined()
    expect(screen.getAllByText(/no.*entries/i).length).toBeGreaterThanOrEqual(2)
  })
})
