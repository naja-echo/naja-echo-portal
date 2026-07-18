import { useMutation, useQueryClient } from '@tanstack/react-query'
import { awardLootPoints } from '../api/lootApi'
import { lootKeys } from './lootKeys'
import type { AddLedgerEntryRequest } from '../schemas/lootSchemas'

export function useAwardLootPoints(userId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (req: AddLedgerEntryRequest) => awardLootPoints(userId, req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: lootKeys.memberLedger(userId) })
      queryClient.invalidateQueries({ queryKey: lootKeys.distribution() })
    },
  })
}
