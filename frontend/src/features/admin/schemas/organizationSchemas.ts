import { z } from 'zod'
import type { components } from '@/lib/api/organizations'

/**
 * Zod schemas for the organization admin endpoints.
 *
 * Each schema is annotated with the matching type generated from
 * `specs/021-org-foundation/contracts/openapi.yaml`, so the contract still governs the shape:
 * a schema that drifts from the OpenAPI document fails to compile rather than failing at runtime
 * against a real response. Runtime parsing stays Zod, matching every other feature here.
 *
 * See plan.md's Constitution Check for why this half-step, rather than full adoption of the
 * generated types, is the right scope for this feature.
 */
export const organizationSummarySchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
}) satisfies z.ZodType<components['schemas']['OrganizationSummary']>

export const organizationListResponseSchema = z.object({
  organizations: z.array(organizationSummarySchema),
}) satisfies z.ZodType<components['schemas']['OrganizationListResponse']>

export type OrganizationSummary = z.infer<typeof organizationSummarySchema>
export type OrganizationListResponse = z.infer<typeof organizationListResponseSchema>
