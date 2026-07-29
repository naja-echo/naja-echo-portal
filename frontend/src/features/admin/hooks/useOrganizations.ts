import { useQuery } from '@tanstack/react-query'
import { getOrganizations } from '../api/organizationsApi'
import { organizationKeys } from './organizationKeys'

export function useOrganizations() {
  return useQuery({
    queryKey: organizationKeys.organizations.list(),
    queryFn: getOrganizations,
  })
}
