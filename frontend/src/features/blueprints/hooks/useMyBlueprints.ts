import { useQuery } from '@tanstack/react-query'
import { getMyBlueprints } from '../api/blueprintsApi'

export const blueprintKeys = {
  mine: () => ['blueprints', 'mine'] as const,
  search: (q: string) => ['blueprints', 'search', q] as const,
}

export function useMyBlueprints() {
  return useQuery({
    queryKey: blueprintKeys.mine(),
    queryFn: getMyBlueprints,
  })
}
