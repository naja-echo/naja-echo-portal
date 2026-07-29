import { useQuery } from '@tanstack/react-query'
import { getOrgBlueprints } from '../api/blueprintsApi'

export function useOrgBlueprints() {
  return useQuery({
    queryKey: ['blueprints', 'org'],
    queryFn: getOrgBlueprints,
  })
}
