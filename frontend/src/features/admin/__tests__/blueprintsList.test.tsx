import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect } from 'vitest'
import { BlueprintsList } from '../components/BlueprintsList'
import type { BlueprintListItem } from '../schemas/blueprintSchemas'

const items: BlueprintListItem[] = [
  { guid: '11111111-1111-1111-1111-111111111111', displayName: 'Alpha Widget', nameSource: 'productName', productName: 'Alpha Widget', tag: 'BP_A', manufacturer: 'ACME' },
  { guid: '22222222-2222-2222-2222-222222222222', displayName: 'Beta Gadget', nameSource: 'linkedItem', productName: null, tag: 'BP_B', manufacturer: null },
  { guid: '33333333-3333-3333-3333-333333333333', displayName: 'Gamma Tool', nameSource: 'tag', productName: null, tag: 'BP_G', manufacturer: null },
]

describe('BlueprintsList', () => {
  it('renders all blueprints by display name', () => {
    render(<BlueprintsList blueprints={items} />)
    expect(screen.getByText('Alpha Widget')).toBeDefined()
    expect(screen.getByText('Beta Gadget')).toBeDefined()
    expect(screen.getByText('Gamma Tool')).toBeDefined()
  })

  it('filters case-insensitively by substring', async () => {
    const user = userEvent.setup()
    render(<BlueprintsList blueprints={items} />)

    await user.type(screen.getByRole('textbox', { name: /search blueprints/i }), 'beta')

    expect(screen.getByText('Beta Gadget')).toBeDefined()
    expect(screen.queryByText('Alpha Widget')).toBeNull()
    expect(screen.queryByText('Gamma Tool')).toBeNull()
  })

  it('shows a no-match empty state when the search matches nothing', async () => {
    const user = userEvent.setup()
    render(<BlueprintsList blueprints={items} />)

    await user.type(screen.getByRole('textbox', { name: /search blueprints/i }), 'zzzzz')

    expect(screen.getByText(/no blueprints match/i)).toBeDefined()
  })

  it('shows an empty state directing to upload when there are no blueprints', () => {
    render(<BlueprintsList blueprints={[]} />)
    expect(screen.getByText(/no blueprints have been imported yet/i)).toBeDefined()
  })
})
