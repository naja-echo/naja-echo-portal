import { useQuery } from '@tanstack/react-query'
import { searchBlueprints } from '../api/blueprintsApi'
import { blueprintKeys } from './useMyBlueprints'

export function useBlueprintSearch(query: string) {
  return useQuery({
    queryKey: blueprintKeys.search(query),
    queryFn: () => searchBlueprints(query),
    enabled: query.trim().length > 0,
  })
}
