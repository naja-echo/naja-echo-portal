import { useQuery } from '@tanstack/react-query'
import { getDistribution } from '../api/lootApi'
import { lootKeys } from './lootKeys'

export function useDistribution() {
  return useQuery({
    queryKey: lootKeys.distribution(),
    queryFn: getDistribution,
  })
}
