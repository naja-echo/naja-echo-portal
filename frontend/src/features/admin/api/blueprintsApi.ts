import { apiFetch } from '@/lib/apiClient'
import {
  blueprintListResponseSchema,
  enrichBlueprintsResponseSchema,
  importBlueprintsResponseSchema,
  type BlueprintListResponse,
  type EnrichBlueprintsResponse,
  type ImportBlueprintsResponse,
} from '../schemas/blueprintSchemas'

/** `documentJson` is the raw JSON text of the dataset file (already validated + parsed by the caller). */
export async function importBlueprints(documentJson: string): Promise<ImportBlueprintsResponse> {
  const data = await apiFetch<unknown>('/api/admin/blueprints/import', {
    method: 'POST',
    body: documentJson,
  })
  return importBlueprintsResponseSchema.parse(data)
}

export async function enrichBlueprints(documentJson: string): Promise<EnrichBlueprintsResponse> {
  const data = await apiFetch<unknown>('/api/admin/blueprints/enrich-items', {
    method: 'POST',
    body: documentJson,
  })
  return enrichBlueprintsResponseSchema.parse(data)
}

export async function getBlueprints(): Promise<BlueprintListResponse> {
  const data = await apiFetch<unknown>('/api/admin/blueprints')
  return blueprintListResponseSchema.parse(data)
}
