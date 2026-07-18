import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/tests/server'
import { createWrapper } from '@/tests/testUtils'
import { LootDistributionPage } from '../pages/LootDistributionPage'

const memberId1 = 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11'
const memberId2 = 'b0eebc99-9c0b-4ef8-bb6d-6bb9bd380a22'

const distributionData = {
  members: [
    {
      memberId: memberId2,
      displayName: 'Bob',
      orgPointsTotal: 200,
      lootPointsTotal: 50,
      claimPriority: 4.0,
    },
    {
      memberId: memberId1,
      displayName: 'Alice',
      orgPointsTotal: 100,
      lootPointsTotal: 200,
      claimPriority: 0.5,
    },
  ],
}

const memberLedger = {
  memberId: memberId2,
  displayName: 'Bob',
  orgPoints: [],
  lootPoints: [],
  orgPointsTotal: 200,
  lootPointsTotal: 50,
  claimPriority: 4.0,
}

describe('LootDistributionPage', () => {
  it('renders all members sorted by Claim Priority ascending', async () => {
    server.use(
      http.get('/api/loot/distribution', () => HttpResponse.json(distributionData))
    )
    render(<LootDistributionPage />, { wrapper: createWrapper() })

    await waitFor(() => expect(screen.getByText('Loot Distribution')).toBeDefined())

    const rows = screen.getAllByRole('row')
    // header + 2 data rows
    expect(rows.length).toBe(3)
    // Alice (0.5) should appear before Bob (4.0)
    const cells = screen.getAllByRole('cell')
    const names = cells.filter((c) => ['Alice', 'Bob'].includes(c.textContent ?? ''))
    expect(names[0].textContent).toBe('Alice')
    expect(names[1].textContent).toBe('Bob')
  })

  it('clicking the View icon opens MemberLedgerSheet for that member', async () => {
    server.use(
      http.get('/api/loot/distribution', () => HttpResponse.json(distributionData)),
      http.get(`/api/loot/${memberId1}`, () => HttpResponse.json({ ...memberLedger, memberId: memberId1, displayName: 'Alice' }))
    )
    const user = userEvent.setup()
    render(<LootDistributionPage />, { wrapper: createWrapper() })

    await waitFor(() => expect(screen.getAllByText('Alice').length).toBeGreaterThan(0))

    const viewButtons = screen.getAllByRole('button', { name: /view ledger/i })
    await user.click(viewButtons[0])

    await waitFor(() => expect(screen.getAllByText('Alice').length).toBeGreaterThan(0))
  })

  it('shows Add Points and Award Loot for an Admin session', async () => {
    server.use(
      http.get('/api/loot/distribution', () => HttpResponse.json(distributionData)),
      http.get('/api/auth/me', () =>
        HttpResponse.json({
          authenticated: true,
          user: { id: memberId1, displayName: 'Admin', discordUsername: 'admin', roles: ['Admin'] },
        })
      )
    )
    render(<LootDistributionPage />, { wrapper: createWrapper() })

    await waitFor(() => expect(screen.getByText('Loot Distribution')).toBeDefined())
    expect(screen.getByRole('button', { name: /add points/i })).toBeDefined()
    expect(screen.getByRole('button', { name: /award loot/i })).toBeDefined()
  })

  it('hides action buttons for a plain member session', async () => {
    server.use(
      http.get('/api/loot/distribution', () => HttpResponse.json(distributionData))
    )
    render(<LootDistributionPage />, { wrapper: createWrapper() })

    await waitFor(() => expect(screen.getByText('Loot Distribution')).toBeDefined())
    expect(screen.queryByRole('button', { name: /add points/i })).toBeNull()
    expect(screen.queryByRole('button', { name: /award loot/i })).toBeNull()
  })
})
