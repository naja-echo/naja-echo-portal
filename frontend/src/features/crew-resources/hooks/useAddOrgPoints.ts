import { useMutation, useQueryClient } from '@tanstack/react-query'
import { addOrgPoints } from '../api/lootApi'
import { lootKeys } from './lootKeys'
import type { AddLedgerEntryRequest } from '../schemas/lootSchemas'

export function useAddOrgPoints(userId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (req: AddLedgerEntryRequest) => addOrgPoints(userId, req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: lootKeys.memberLedger(userId) })
      queryClient.invalidateQueries({ queryKey: lootKeys.distribution() })
    },
  })
}
