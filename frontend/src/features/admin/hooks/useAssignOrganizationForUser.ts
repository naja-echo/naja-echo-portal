import { useMutation, useQueryClient } from '@tanstack/react-query'
import { assignOrganizationForUser } from '../api/organizationsApi'
import { userKeys } from './userKeys'

export function useAssignOrganizationForUser() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ userId, organizationId }: { userId: string; organizationId: string | null }) =>
      assignOrganizationForUser(userId, organizationId),
    onSuccess: () => {
      // The members list carries each member's organization, so it is what goes stale here.
      queryClient.invalidateQueries({ queryKey: userKeys.adminUsers.list() })
    },
  })
}
