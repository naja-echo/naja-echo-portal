import { z } from 'zod'

/**
 * Light top-level shape check applied in the browser before POSTing (FR-002/004). Unknown keys
 * are ignored; the backend performs authoritative validation. Only used with `safeParse` — the
 * original parsed document is sent, never this schema's output.
 */
export const blueprintDatasetSchema = z.object({
  version: z.string(),
  meta: z.object({}),
  dismantle: z.object({}),
  properties: z.object({}),
  resources: z.array(z.unknown()),
  items: z.array(z.unknown()),
  blueprints: z.array(z.unknown()),
})

const collectionCountsSchema = z.object({
  read: z.number(),
  inserted: z.number(),
  updated: z.number(),
  rejected: z.number(),
})

export const blueprintRejectionSchema = z.object({
  guid: z.string().nullable().optional(),
  productName: z.string().nullable().optional(),
  reason: z.string(),
})

export const importBlueprintsResponseSchema = z.object({
  version: z.string(),
  blueprints: collectionCountsSchema,
  resources: collectionCountsSchema,
  items: collectionCountsSchema,
  properties: collectionCountsSchema,
  referenceDataReplaced: z.boolean(),
  warnings: z.array(z.string()),
  rejections: z.array(blueprintRejectionSchema),
})

export const blueprintListItemSchema = z.object({
  guid: z.string(),
  displayName: z.string(),
  nameSource: z.enum(['productName', 'linkedItem', 'tag', 'guid']),
  productName: z.string().nullable().optional(),
  tag: z.string().optional(),
  manufacturer: z.string().nullable().optional(),
})

export const blueprintListResponseSchema = z.object({
  blueprints: z.array(blueprintListItemSchema),
})

export const craftingItemsSchema = z
  .object({
    version: z.string(),
    items: z.array(z.unknown()),
  })
  .passthrough()
  // Reject blueprint dataset files, which also have 'version' and 'items' but
  // additionally carry 'blueprints' and 'resources' keys.
  .refine((d) => !('blueprints' in d) && !('resources' in d), {
    message: 'Looks like a blueprint dataset file, not a crafting items file.',
  })

export const enrichBlueprintsResponseSchema = z.object({
  itemsParsed: z.number(),
  blueprintsUpdated: z.number(),
})

export type CollectionCounts = z.infer<typeof collectionCountsSchema>
export type BlueprintRejection = z.infer<typeof blueprintRejectionSchema>
export type ImportBlueprintsResponse = z.infer<typeof importBlueprintsResponseSchema>
export type EnrichBlueprintsResponse = z.infer<typeof enrichBlueprintsResponseSchema>
export type BlueprintListItem = z.infer<typeof blueprintListItemSchema>
export type BlueprintListResponse = z.infer<typeof blueprintListResponseSchema>
