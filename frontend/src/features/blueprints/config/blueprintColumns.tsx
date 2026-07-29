import { blueprintLabel } from './blueprintLabels'

interface BlueprintRow {
  productName: string | null
  type: string | null
  subtype: string | null
  ingredientCount: number
}

export interface BlueprintColumn {
  header: string
  render: (bp: BlueprintRow) => React.ReactNode
  className?: string
}

const DEFAULT_COLUMNS: BlueprintColumn[] = [
  { header: 'Blueprint', render: bp => bp.productName ?? '—' },
  { header: 'Type', render: bp => bp.type ? blueprintLabel(bp.type) : '—', className: 'text-muted-foreground' },
  { header: 'Ingredients', render: bp => bp.ingredientCount },
]

const CATEGORY_COLUMNS: Record<string, BlueprintColumn[]> = {}

export function getBlueprintColumns(category: string): BlueprintColumn[] {
  return CATEGORY_COLUMNS[category.toLowerCase()] ?? DEFAULT_COLUMNS
}
