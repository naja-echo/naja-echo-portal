import { useQuery } from '@tanstack/react-query'
import { getMyLoot } from '../api/lootApi'
import { lootKeys } from './lootKeys'

export function useMyLoot() {
  return useQuery({
    queryKey: lootKeys.myLoot(),
    queryFn: getMyLoot,
  })
}
