import { apiFetch } from '@/lib/apiClient'
import {
  memberLedgerResponseSchema,
  lootDistributionResponseSchema,
  type MemberLedgerResponse,
  type LootDistributionResponse,
  type AddLedgerEntryRequest,
  type LedgerEntry,
} from '../schemas/lootSchemas'

export async function getMyLoot(): Promise<MemberLedgerResponse> {
  const data = await apiFetch<unknown>('/api/loot/me')
  return memberLedgerResponseSchema.parse(data)
}

export async function getDistribution(): Promise<LootDistributionResponse> {
  const data = await apiFetch<unknown>('/api/loot/distribution')
  return lootDistributionResponseSchema.parse(data)
}

export async function getMemberLedger(userId: string): Promise<MemberLedgerResponse> {
  const data = await apiFetch<unknown>(`/api/loot/${userId}`)
  return memberLedgerResponseSchema.parse(data)
}

export async function addOrgPoints(userId: string, req: AddLedgerEntryRequest): Promise<LedgerEntry> {
  const data = await apiFetch<unknown>(`/api/loot/${userId}/org-points`, {
    method: 'POST',
    body: JSON.stringify(req),
  })
  return data as LedgerEntry
}

export async function awardLootPoints(userId: string, req: AddLedgerEntryRequest): Promise<LedgerEntry> {
  const data = await apiFetch<unknown>(`/api/loot/${userId}/loot-points`, {
    method: 'POST',
    body: JSON.stringify(req),
  })
  return data as LedgerEntry
}
