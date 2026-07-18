import { render, screen, waitFor } from '@testing-library/react'
import { describe, it, expect, beforeEach } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/tests/server'
import { createWrapper } from '@/tests/testUtils'
import { MemberLedgerSheet } from '../components/MemberLedgerSheet'

const memberId = 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11'

const emptyLedger = {
  memberId,
  displayName: 'Alice',
  orgPoints: [],
  lootPoints: [],
  orgPointsTotal: 0,
  lootPointsTotal: 0,
  claimPriority: 0,
}

describe('MemberLedgerSheet', () => {
  beforeEach(() => {
    server.use(
      http.get(`/api/loot/${memberId}`, () => HttpResponse.json(emptyLedger))
    )
  })

  it('renders the ledger sections and claim priority', async () => {
    render(
      <MemberLedgerSheet memberId={memberId} memberName="Alice" open={true} onClose={() => {}} />,
      { wrapper: createWrapper() }
    )
    await waitFor(() => expect(screen.getByText('0.00')).toBeDefined())
    expect(screen.getByText(/Org Points \(Total: 0\)/)).toBeDefined()
    expect(screen.getByText(/Loot Points \(Total: 0\)/)).toBeDefined()
  })

  it('is view-only: no Add Points or Award Loot actions', async () => {
    render(
      <MemberLedgerSheet memberId={memberId} memberName="Alice" open={true} onClose={() => {}} />,
      { wrapper: createWrapper() }
    )
    await waitFor(() => expect(screen.getByText('0.00')).toBeDefined())
    expect(screen.queryByRole('button', { name: /add points/i })).toBeNull()
    expect(screen.queryByRole('button', { name: /award loot/i })).toBeNull()
  })
})
