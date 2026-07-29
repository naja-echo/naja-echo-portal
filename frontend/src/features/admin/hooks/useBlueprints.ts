import { useQuery } from '@tanstack/react-query'
import { getBlueprints } from '../api/blueprintsApi'
import { blueprintKeys } from './blueprintKeys'

export function useBlueprints() {
  return useQuery({
    queryKey: blueprintKeys.list(),
    queryFn: getBlueprints,
  })
}
