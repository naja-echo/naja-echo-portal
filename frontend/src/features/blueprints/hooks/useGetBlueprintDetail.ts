import { useQuery } from '@tanstack/react-query'
import { getBlueprintDetail } from '../api/blueprintsApi'
import { blueprintKeys } from './useMyBlueprints'

export function useGetBlueprintDetail(blueprintId: string | null) {
  return useQuery({
    queryKey: blueprintKeys.detail(blueprintId ?? ''),
    queryFn: () => getBlueprintDetail(blueprintId!),
    enabled: blueprintId !== null,
  })
}
