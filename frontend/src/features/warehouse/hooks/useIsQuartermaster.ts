import { useHasRole } from '@/features/auth/hooks/useHasRole'
import { ROLES } from '@/features/auth/lib/roles'

/** Convenience wrapper — warehouse write controls gate on Quartermaster (or Admin). */
export function useIsQuartermaster(): boolean {
  return useHasRole([ROLES.Quartermaster])
}
