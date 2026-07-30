import { Combobox, type ComboboxOption } from '@/components/ui/combobox'

export interface BlueprintFiltersProps {
  name: string
  category: string
  subcategory: string
  itemType: string
  componentClass: string
  componentSize: string
  componentGrade: string
  categoryOptions: ComboboxOption[]
  subcategoryOptions: ComboboxOption[]
  itemTypeOptions: ComboboxOption[]
  componentClassOptions: ComboboxOption[]
  componentSizeOptions: ComboboxOption[]
  componentGradeOptions: ComboboxOption[]
  onNameChange: (v: string) => void
  onCategoryChange: (v: string) => void
  onSubcategoryChange: (v: string) => void
  onItemTypeChange: (v: string) => void
  onComponentClassChange: (v: string) => void
  onComponentSizeChange: (v: string) => void
  onComponentGradeChange: (v: string) => void
}

const isVehicleGear = (category: string) => category === 'vehiclegear'
const isMiningLaser = (category: string, subcategory: string) =>
  isVehicleGear(category) && subcategory === 'mininglaser'
const isSalvage = (category: string, subcategory: string) =>
  isVehicleGear(category) && subcategory === 'salvage'
const isTractorBeam = (category: string, subcategory: string) =>
  isVehicleGear(category) && subcategory === 'tractorbeam'

// Subcategories where the item-type ("All types") filter is not applicable.
const HIDE_ITEM_TYPE_FILTER = new Set([
  'mininglaser', 'tractorbeam', 'cooler', 'quantumdrive', 'shield', 'radar',
])

export function BlueprintFilters({
  name, category, subcategory, itemType,
  componentClass, componentSize, componentGrade,
  categoryOptions, subcategoryOptions, itemTypeOptions,
  componentClassOptions, componentSizeOptions, componentGradeOptions,
  onNameChange, onCategoryChange, onSubcategoryChange, onItemTypeChange,
  onComponentClassChange, onComponentSizeChange, onComponentGradeChange,
}: BlueprintFiltersProps) {
  const vehicleGear = isVehicleGear(category)
  const miningLaser = isMiningLaser(category, subcategory)
  const salvage = isSalvage(category, subcategory)
  const tractorBeam = isTractorBeam(category, subcategory)

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

      {subcategory !== '' && itemTypeOptions.length > 0 && !HIDE_ITEM_TYPE_FILTER.has(subcategory) && (
        <div className="flex flex-col gap-1">
          <label className="text-xs text-muted-foreground">{vehicleGear && subcategory !== 'weapons' ? 'Size' : 'Type'}</label>
          <Combobox
            options={itemTypeOptions}
            value={itemType}
            onValueChange={onItemTypeChange}
            placeholder="All types"
            searchPlaceholder="Search types…"
            className="w-36"
            aria-label="Type"
          />
        </div>
      )}

      {vehicleGear && subcategory !== '' && !salvage && componentSizeOptions.length > 0 && (
        <div className="flex flex-col gap-1">
          <label className="text-xs text-muted-foreground">Size</label>
          <Combobox
            options={componentSizeOptions}
            value={componentSize}
            onValueChange={onComponentSizeChange}
            placeholder="All sizes"
            searchPlaceholder="Search sizes…"
            className="w-28"
            aria-label="Size"
          />
        </div>
      )}

      {vehicleGear && subcategory !== '' && componentClassOptions.length > 0 && (
        <div className="flex flex-col gap-1">
          <label className="text-xs text-muted-foreground">Class</label>
          <Combobox
            options={componentClassOptions}
            value={componentClass}
            onValueChange={onComponentClassChange}
            placeholder="All classes"
            searchPlaceholder="Search classes…"
            className="w-36"
            aria-label="Class"
          />
        </div>
      )}

      {vehicleGear && subcategory !== '' && !miningLaser && !salvage && !tractorBeam && subcategory !== 'weapons' && componentGradeOptions.length > 0 && (
        <div className="flex flex-col gap-1">
          <label className="text-xs text-muted-foreground">Grade</label>
          <Combobox
            options={componentGradeOptions}
            value={componentGrade}
            onValueChange={onComponentGradeChange}
            placeholder="All grades"
            searchPlaceholder="Search grades…"
            className="w-28"
            aria-label="Grade"
          />
        </div>
      )}
    </div>
  )
}
