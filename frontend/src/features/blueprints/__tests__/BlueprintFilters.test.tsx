import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import { BlueprintFilters } from '../components/BlueprintFilters'

const noop = () => {}

const defaultProps = {
  name: '',
  category: '',
  subcategory: '',
  categoryOptions: [
    { value: 'Weapon', label: 'Weapon' },
    { value: 'Ship', label: 'Ship' },
  ],
  subcategoryOptions: [
    { value: 'Pistol', label: 'Pistol' },
    { value: 'Fighter', label: 'Fighter' },
  ],
  onNameChange: noop,
  onCategoryChange: noop,
  onSubcategoryChange: noop,
}

describe('BlueprintFilters', () => {
  it('renders name search input', () => {
    render(<BlueprintFilters {...defaultProps} />)
    expect(screen.getByPlaceholderText(/filter by name/i)).toBeDefined()
  })

  it('renders category combobox', () => {
    render(<BlueprintFilters {...defaultProps} />)
    expect(screen.getByRole('combobox', { name: /category/i })).toBeDefined()
  })

  it('renders subcategory combobox', () => {
    render(<BlueprintFilters {...defaultProps} />)
    expect(screen.getByRole('combobox', { name: /subcategory/i })).toBeDefined()
  })

  it('subcategory combobox is always rendered (not hidden when no category)', () => {
    render(<BlueprintFilters {...defaultProps} category="" />)
    expect(screen.getByRole('combobox', { name: /subcategory/i })).toBeDefined()
  })

  it('typing in name input calls onNameChange', async () => {
    const onNameChange = vi.fn()
    const user = userEvent.setup()
    render(<BlueprintFilters {...defaultProps} onNameChange={onNameChange} />)
    await user.type(screen.getByPlaceholderText(/filter by name/i), 'w')
    expect(onNameChange).toHaveBeenCalledWith('w')
  })

  it('selecting a category calls onCategoryChange', async () => {
    const onCategoryChange = vi.fn()
    const user = userEvent.setup()
    render(<BlueprintFilters {...defaultProps} onCategoryChange={onCategoryChange} />)
    await user.click(screen.getByRole('combobox', { name: /category/i }))
    await user.click(screen.getByText('Weapon'))
    expect(onCategoryChange).toHaveBeenCalledWith('Weapon')
  })

  it('selecting a subcategory calls onSubcategoryChange', async () => {
    const onSubcategoryChange = vi.fn()
    const user = userEvent.setup()
    render(<BlueprintFilters {...defaultProps} onSubcategoryChange={onSubcategoryChange} />)
    await user.click(screen.getByRole('combobox', { name: /subcategory/i }))
    await user.click(screen.getByText('Pistol'))
    expect(onSubcategoryChange).toHaveBeenCalledWith('Pistol')
  })
})
