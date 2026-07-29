import { useCurrentUser } from './useCurrentUser'
import { hasAnyRole, type Role } from '../lib/roles'

/**
 * True when the signed-in user holds one of `allowed` — or is an Admin. Returns false while
 * the session is loading and for anonymous visitors.
 */
export function useHasRole(allowed: readonly Role[]): boolean {
  const { data: session } = useCurrentUser()

  if (session?.authenticated !== true) return false

  return hasAnyRole(session.user.roles, allowed)
}
