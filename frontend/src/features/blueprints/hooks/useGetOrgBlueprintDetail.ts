import { useQuery } from '@tanstack/react-query'
import { getOrgBlueprintDetail } from '../api/blueprintsApi'

export function useGetOrgBlueprintDetail(blueprintId: string | null) {
  return useQuery({
    queryKey: ['blueprints', 'org-detail', blueprintId],
    queryFn: () => getOrgBlueprintDetail(blueprintId!),
    enabled: blueprintId !== null,
  })
}
