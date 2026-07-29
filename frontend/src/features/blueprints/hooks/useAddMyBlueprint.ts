import { useMutation, useQueryClient } from '@tanstack/react-query'
import { addMyBlueprint } from '../api/blueprintsApi'
import { blueprintKeys } from './useMyBlueprints'

export function useAddMyBlueprint() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (blueprintId: string) => addMyBlueprint(blueprintId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: blueprintKeys.mine() })
      void queryClient.invalidateQueries({ queryKey: ['blueprints', 'org'] })
      void queryClient.invalidateQueries({ queryKey: ['blueprints', 'org-detail'] })
    },
  })
}
