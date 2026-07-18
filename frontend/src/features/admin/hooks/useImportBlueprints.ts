import { useMutation, useQueryClient } from '@tanstack/react-query'
import { importBlueprints } from '../api/blueprintsApi'
import { blueprintKeys } from './blueprintKeys'

export function useImportBlueprints() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (documentJson: string) => importBlueprints(documentJson),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: blueprintKeys.all })
    },
  })
}
