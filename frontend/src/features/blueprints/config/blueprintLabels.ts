export const CATEGORY_LABELS: Record<string, string> = {
  armour: 'Armor',
  mininglaser: 'Mining Laser',
  quantumdrive: 'Quantum Drive',
  powerplant: 'Power Plant',
  size0: 'Size 0',
  size1: 'Size 1',
  size2: 'Size 2',
  size3: 'Size 3',
  size4: 'Size 4',
  size5: 'Size 5',
  size6: 'Size 6',
  size7: 'Size 7',
  tractorbeam: 'Tractor Beam',
  fpsgear: 'FPS Gear',
  vehiclegear: 'Vehicle Gear',
}

export function blueprintLabel(value: string): string {
  return CATEGORY_LABELS[value.toLowerCase()] ?? (value.charAt(0).toUpperCase() + value.slice(1))
}
