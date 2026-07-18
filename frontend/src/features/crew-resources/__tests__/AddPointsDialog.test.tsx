import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/tests/server'
import { createWrapper } from '@/tests/testUtils'
import { AddPointsDialog } from '../components/AddPointsDialog'
import { addLedgerEntryRequestSchema, type DistributionRow } from '../schemas/lootSchemas'

const memberId = 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11'

const members: DistributionRow[] = [
  { memberId, displayName: 'Alice', orgPointsTotal: 0, lootPointsTotal: 0, claimPriority: 0 },
  { memberId: 'b0eebc99-9c0b-4ef8-bb6d-6bb9bd380a22', displayName: 'Bob', orgPointsTotal: 10, lootPointsTotal: 5, claimPriority: 2 },
]

async function selectMember(user: ReturnType<typeof userEvent.setup>, label: string) {
  await user.click(screen.getByRole('combobox', { name: 'Member' }))
  await user.click(await screen.findByText(label))
}

describe('AddPointsDialog', () => {
  it('renders dialog when open=true', () => {
    render(
      <AddPointsDialog open={true} onClose={() => {}} members={members} />,
      { wrapper: createWrapper() }
    )
    expect(screen.getByRole('dialog')).toBeDefined()
    expect(screen.getByText('Add Org Points')).toBeDefined()
  })

  it('does not render when open=false', () => {
    render(
      <AddPointsDialog open={false} onClose={() => {}} members={members} />,
      { wrapper: createWrapper() }
    )
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('disables submit until a member is selected', async () => {
    const user = userEvent.setup()
    render(
      <AddPointsDialog open={true} onClose={() => {}} members={members} />,
      { wrapper: createWrapper() }
    )
    expect(screen.getByRole('button', { name: /add points/i })).toHaveProperty('disabled', true)
    await selectMember(user, 'Alice')
    expect(screen.getByRole('button', { name: /add points/i })).toHaveProperty('disabled', false)
  })

  it('blocks empty reason with validation message', async () => {
    const user = userEvent.setup()
    render(
      <AddPointsDialog open={true} onClose={() => {}} members={members} />,
      { wrapper: createWrapper() }
    )
    await selectMember(user, 'Alice')
    await user.type(screen.getByLabelText(/^Amount/i), '10')
    // leave reason empty and submit
    await user.click(screen.getByRole('button', { name: /add points/i }))

    await waitFor(() => {
      expect(screen.getByText('Reason is required')).toBeDefined()
    })
  })

  it('schema rejects non-integer amount with correct message', () => {
    // Tests FR-016: amount must be a whole integer
    const result = addLedgerEntryRequestSchema.safeParse({ amount: 10.5, reason: 'test' })
    expect(result.success).toBe(false)
    expect(result.error?.issues[0].message).toBe('Must be a whole number')
  })

  it('calls mutation for the selected member and closes on valid submit', async () => {
    const onClose = vi.fn()
    let called = false
    server.use(
      http.post(`/api/loot/${memberId}/org-points`, () => {
        called = true
        return HttpResponse.json({ id: 'new-id', amount: 10, reason: 'Test', postedBy: 'Actor', createdAt: '2026-06-01T12:00:00Z' })
      })
    )
    const user = userEvent.setup()
    render(
      <AddPointsDialog open={true} onClose={onClose} members={members} />,
      { wrapper: createWrapper() }
    )
    await selectMember(user, 'Alice')
    await user.type(screen.getByLabelText(/^Amount/i), '10')
    await user.type(screen.getByLabelText(/^Reason/i), 'Test reason')
    await user.click(screen.getByRole('button', { name: /add points/i }))

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(called).toBe(true)
  })
})
