import { useQuery } from '@tanstack/react-query'
import { getMemberLedger } from '../api/lootApi'
import { lootKeys } from './lootKeys'

export function useMemberLedger(userId: string) {
  return useQuery({
    queryKey: lootKeys.memberLedger(userId),
    queryFn: () => getMemberLedger(userId),
    enabled: !!userId,
  })
}
