import { Combobox, type ComboboxOption } from '@/components/ui/combobox'

export interface BlueprintFiltersProps {
  name: string
  category: string
  subcategory: string
  armorType: string
  categoryOptions: ComboboxOption[]
  subcategoryOptions: ComboboxOption[]
  armorTypeOptions: ComboboxOption[]
  onNameChange: (v: string) => void
  onCategoryChange: (v: string) => void
  onSubcategoryChange: (v: string) => void
  onArmorTypeChange: (v: string) => void
}

export function BlueprintFilters({
  name,
  category,
  subcategory,
  armorType,
  categoryOptions,
  subcategoryOptions,
  armorTypeOptions,
  onNameChange,
  onCategoryChange,
  onSubcategoryChange,
  onArmorTypeChange,
}: BlueprintFiltersProps) {
  return (
    <div className="flex flex-wrap gap-3">
      <div className="flex flex-col gap-1">
        <label htmlFor="bp-filter-name" className="text-xs text-muted-foreground">Name</label>
        <input
          id="bp-filter-name"
          className="h-9 w-48 rounded-md border border-input bg-background px-3 text-sm text-foreground"
          value={name}
          onChange={e => onNameChange(e.target.value)}
          placeholder="Filter by name…"
        />
      </div>

      <div className="flex flex-col gap-1">
        <label className="text-xs text-muted-foreground">Category</label>
        <Combobox
          options={categoryOptions}
          value={category}
          onValueChange={onCategoryChange}
          placeholder="All categories"
          searchPlaceholder="Search categories…"
          className="w-44"
          aria-label="Category"
        />
      </div>

      <div className="flex flex-col gap-1">
        <label className="text-xs text-muted-foreground">Subcategory</label>
        <Combobox
          options={subcategoryOptions}
          value={subcategory}
          onValueChange={onSubcategoryChange}
          placeholder="All subcategories"
          searchPlaceholder="Search subcategories…"
          className="w-48"
          aria-label="Subcategory"
        />
      </div>

      {subcategory !== '' && armorTypeOptions.length > 0 && (
        <div className="flex flex-col gap-1">
          <label className="text-xs text-muted-foreground">Type</label>
          <Combobox
            options={armorTypeOptions}
            value={armorType}
            onValueChange={onArmorTypeChange}
            placeholder="All types"
            searchPlaceholder="Search types…"
            className="w-36"
            aria-label="Type"
          />
        </div>
      )}
    </div>
  )
}
