import { useMutation, useQueryClient } from '@tanstack/react-query'
import { removeMyBlueprint } from '../api/blueprintsApi'
import { blueprintKeys } from './useMyBlueprints'

export function useRemoveMyBlueprint() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (blueprintId: string) => removeMyBlueprint(blueprintId),
    onSuccess: (_data, blueprintId) => {
      void queryClient.invalidateQueries({ queryKey: blueprintKeys.mine() })
      void queryClient.invalidateQueries({ queryKey: blueprintKeys.detail(blueprintId) })
    },
  })
}
