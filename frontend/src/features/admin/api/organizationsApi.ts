import { apiFetch } from '@/lib/apiClient'
import {
  organizationListResponseSchema,
  type OrganizationListResponse,
  type OrganizationSummary,
} from '../schemas/organizationSchemas'

export async function getOrganizations(): Promise<OrganizationListResponse> {
  const data = await apiFetch<unknown>('/api/admin/organizations')
  return organizationListResponseSchema.parse(data)
}

/** Sets the member's current organization, or clears it when `organizationId` is null. */
export async function assignOrganizationForUser(
  userId: string,
  organizationId: string | null,
): Promise<void> {
  await apiFetch<unknown>(`/api/admin/users/${userId}/organization`, {
    method: 'PUT',
    body: JSON.stringify({ organizationId }),
  })
}

export type { OrganizationListResponse, OrganizationSummary }
