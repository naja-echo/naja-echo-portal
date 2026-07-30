import { useMutation } from '@tanstack/react-query'
import { enrichBlueprints } from '../api/blueprintsApi'

export function useEnrichBlueprints() {
  return useMutation({
    mutationFn: (documentJson: string) => enrichBlueprints(documentJson),
  })
}
