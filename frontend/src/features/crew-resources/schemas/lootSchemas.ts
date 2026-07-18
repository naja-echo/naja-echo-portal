import { z } from 'zod'

export const ledgerEntrySchema = z.object({
  id: z.string().uuid(),
  amount: z.number().int(),
  reason: z.string(),
  postedBy: z.string(),
  createdAt: z.string(),
})

export const memberLedgerResponseSchema = z.object({
  memberId: z.string().uuid(),
  displayName: z.string(),
  orgPoints: z.array(ledgerEntrySchema),
  lootPoints: z.array(ledgerEntrySchema),
  orgPointsTotal: z.number().int(),
  lootPointsTotal: z.number().int(),
  claimPriority: z.number(),
})

export const distributionRowSchema = z.object({
  memberId: z.string().uuid(),
  displayName: z.string(),
  orgPointsTotal: z.number().int(),
  lootPointsTotal: z.number().int(),
  claimPriority: z.number(),
})

export const lootDistributionResponseSchema = z.object({
  members: z.array(distributionRowSchema),
})

export const addLedgerEntryRequestSchema = z.object({
  amount: z.number().int('Must be a whole number'),
  reason: z.string().min(1, 'Reason is required').max(500, 'Reason must be 500 characters or less'),
})

export type LedgerEntry = z.infer<typeof ledgerEntrySchema>
export type MemberLedgerResponse = z.infer<typeof memberLedgerResponseSchema>
export type DistributionRow = z.infer<typeof distributionRowSchema>
export type LootDistributionResponse = z.infer<typeof lootDistributionResponseSchema>
export type AddLedgerEntryRequest = z.infer<typeof addLedgerEntryRequestSchema>
