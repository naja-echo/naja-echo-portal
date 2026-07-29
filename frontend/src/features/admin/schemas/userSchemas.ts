import { z } from 'zod'
import { organizationSummarySchema } from './organizationSchemas'

export const adminUserCharacterSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  handle: z.string(),
})

export const adminUserSchema = z.object({
  id: z.string().uuid(),
  authName: z.string(),
  roles: z.array(z.string()),
  characters: z.array(adminUserCharacterSchema),
  // Required but nullable, matching the contract: every member carries the field, and null means
  // they hold no current membership — a valid state, not missing data.
  organization: organizationSummarySchema.nullable(),
})

export const adminUserListResponseSchema = z.object({
  users: z.array(adminUserSchema),
})

export type AdminUserCharacter = z.infer<typeof adminUserCharacterSchema>
export type AdminUser = z.infer<typeof adminUserSchema>
export type AdminUserListResponse = z.infer<typeof adminUserListResponseSchema>
