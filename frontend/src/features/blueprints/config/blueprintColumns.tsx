import { blueprintLabel, extractArmorType } from './blueprintLabels'

interface BlueprintRow {
  productName: string | null
  gear: string | null
  type: string | null
  subtype: string | null
  tag: string | null
  componentClass: string | null
  componentSize: number | null
  componentGrade: string | null
  ingredientCount: number
}

export interface BlueprintColumn {
  header: string
  render: (bp: BlueprintRow) => React.ReactNode
  className?: string
}

const DEFAULT_COLUMNS: BlueprintColumn[] = [
  { header: 'Blueprint', render: bp => bp.productName ?? '—' },
  { header: 'Type', render: bp => {
    if (bp.gear === 'vehiclegear') {
      const display = bp.type === 'weapons' ? (bp.subtype ?? 'misc') : bp.type
      return display ? blueprintLabel(display) : '—'
    }
    const display = extractArmorType(bp.tag) ?? bp.subtype ?? 'misc'
    return blueprintLabel(display)
  }, className: 'text-muted-foreground' },
  { header: 'Ingredients', render: bp => bp.ingredientCount },
]

const VEHICLE_GEAR_COLUMNS: BlueprintColumn[] = [
  { header: 'Blueprint', render: bp => bp.productName ?? '—' },
  { header: 'Type', render: bp => {
    if (bp.type === 'weapons') return blueprintLabel(bp.subtype ?? 'misc')
    return bp.type ? blueprintLabel(bp.type) : '—'
  }, className: 'text-muted-foreground' },
  { header: 'Size', render: bp => bp.componentSize != null ? String(bp.componentSize) : '—', className: 'text-muted-foreground' },
  { header: 'Class', render: bp => bp.componentClass ?? '—', className: 'text-muted-foreground' },
  { header: 'Grade', render: bp => bp.componentGrade ?? '—', className: 'text-muted-foreground' },
  { header: 'Ingredients', render: bp => bp.ingredientCount },
]

const VEHICLE_GEAR_MINING_LASER_COLUMNS: BlueprintColumn[] = [
  { header: 'Blueprint', render: bp => bp.productName ?? '—' },
  { header: 'Type', render: bp => {
    if (bp.type === 'weapons') return blueprintLabel(bp.subtype ?? 'misc')
    return bp.type ? blueprintLabel(bp.type) : '—'
  }, className: 'text-muted-foreground' },
  { header: 'Size', render: bp => bp.componentSize != null ? String(bp.componentSize) : '—', className: 'text-muted-foreground' },
  { header: 'Ingredients', render: bp => bp.ingredientCount },
]

const VEHICLE_GEAR_SALVAGE_COLUMNS: BlueprintColumn[] = [
  { header: 'Blueprint', render: bp => bp.productName ?? '—' },
  { header: 'Type', render: bp => {
    if (bp.type === 'weapons') return blueprintLabel(bp.subtype ?? 'misc')
    return bp.type ? blueprintLabel(bp.type) : '—'
  }, className: 'text-muted-foreground' },
  { header: 'Ingredients', render: bp => bp.ingredientCount },
]

const SUBCATEGORY_COLUMNS: Record<string, BlueprintColumn[]> = {
  mininglaser: VEHICLE_GEAR_MINING_LASER_COLUMNS,
  salvage: VEHICLE_GEAR_SALVAGE_COLUMNS,
  tractorbeam: VEHICLE_GEAR_MINING_LASER_COLUMNS,
  weapons: VEHICLE_GEAR_MINING_LASER_COLUMNS,
}

const CATEGORY_COLUMNS: Record<string, BlueprintColumn[]> = {
  vehiclegear: VEHICLE_GEAR_SALVAGE_COLUMNS,
}

export function getBlueprintColumns(category: string, subcategory: string = ''): BlueprintColumn[] {
  const cat = category.toLowerCase()
  const sub = subcategory.toLowerCase()
  if (cat === 'vehiclegear' && sub && SUBCATEGORY_COLUMNS[sub]) {
    return SUBCATEGORY_COLUMNS[sub]
  }
  return CATEGORY_COLUMNS[cat] ?? DEFAULT_COLUMNS
}
