export interface BlueprintSlotOption {
  optionIndex: number
  materialName: string
  kind: string
  quantity: number
}

export interface BlueprintSlot {
  slotIndex: number
  slotName: string
  options: BlueprintSlotOption[]
}

export interface BlueprintDetail {
  blueprintId: string
  productName: string | null
  type: string | null
  craftTimeSeconds: number | null
  ingredientCount: number
  componentClass: string | null
  componentSize: number | null
  componentGrade: string | null
  slots: BlueprintSlot[]
}

export interface BlueprintSearchResult {
  blueprintId: string
  productName: string
  type: string | null
}

export interface BlueprintSearchResponse {
  results: BlueprintSearchResult[]
}

export interface MyBlueprintListItem {
  blueprintId: string
  productName: string | null
  type: string | null
  subtype: string | null
  gear: string | null
  tag: string | null
  componentClass: string | null
  componentSize: number | null
  componentGrade: string | null
  ingredientCount: number
}

export interface MyBlueprintListResponse {
  blueprints: MyBlueprintListItem[]
}

export async function searchBlueprints(q: string): Promise<BlueprintSearchResponse> {
  const params = new URLSearchParams({ q })
  const res = await fetch(`/api/blueprints/search?${params}`, { credentials: 'include' })
  if (!res.ok) throw new Error(`Search failed: ${res.status}`)
  return res.json() as Promise<BlueprintSearchResponse>
}

export async function getMyBlueprints(): Promise<MyBlueprintListResponse> {
  const res = await fetch('/api/blueprints/mine', { credentials: 'include' })
  if (!res.ok) throw new Error(`Failed to load blueprints: ${res.status}`)
  return res.json() as Promise<MyBlueprintListResponse>
}

export async function getBlueprintDetail(blueprintId: string): Promise<BlueprintDetail> {
  const res = await fetch(`/api/blueprints/mine/${blueprintId}`, { credentials: 'include' })
  if (!res.ok) throw Object.assign(new Error(`Failed to load blueprint detail: ${res.status}`), { status: res.status })
  return res.json() as Promise<BlueprintDetail>
}

export async function removeMyBlueprint(blueprintId: string): Promise<void> {
  const res = await fetch(`/api/blueprints/mine/${blueprintId}`, {
    method: 'DELETE',
    credentials: 'include',
  })
  if (!res.ok) throw Object.assign(new Error(`Failed to remove blueprint: ${res.status}`), { status: res.status })
}

export interface OrgBlueprintListItem {
  blueprintId: string
  productName: string | null
  type: string | null
  subtype: string | null
  gear: string | null
  tag: string | null
  componentClass: string | null
  componentSize: number | null
  componentGrade: string | null
  ingredientCount: number
}

export interface OrgBlueprintListResponse {
  blueprints: OrgBlueprintListItem[]
}

export interface BlueprintOwner {
  userId: string
  displayName: string
}

export interface OrgBlueprintDetail {
  blueprintId: string
  productName: string | null
  type: string | null
  craftTimeSeconds: number | null
  ingredientCount: number
  componentClass: string | null
  componentSize: number | null
  componentGrade: string | null
  slots: BlueprintSlot[]
  owners: BlueprintOwner[]
}

export async function getOrgBlueprints(): Promise<OrgBlueprintListResponse> {
  const res = await fetch('/api/blueprints/org', { credentials: 'include' })
  if (!res.ok) throw new Error(`Failed to load org blueprints: ${res.status}`)
  return res.json() as Promise<OrgBlueprintListResponse>
}

export async function getOrgBlueprintDetail(blueprintId: string): Promise<OrgBlueprintDetail> {
  const res = await fetch(`/api/blueprints/org/${blueprintId}`, { credentials: 'include' })
  if (!res.ok) throw Object.assign(new Error(`Failed to load org blueprint detail: ${res.status}`), { status: res.status })
  return res.json() as Promise<OrgBlueprintDetail>
}

export async function addMyBlueprint(blueprintId: string): Promise<MyBlueprintListItem> {
  const res = await fetch('/api/blueprints/mine', {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ blueprintId }),
  })
  if (res.status === 409) {
    throw Object.assign(new Error('Blueprint already in your list.'), { status: 409 })
  }
  if (res.status === 404) {
    throw Object.assign(new Error('Blueprint not found.'), { status: 404 })
  }
  if (!res.ok) throw new Error(`Failed to add blueprint: ${res.status}`)
  return res.json() as Promise<MyBlueprintListItem>
}
