import { Navigate, Outlet } from 'react-router-dom'
import { useCurrentUser } from './hooks/useCurrentUser'
import { hasAnyRole, type Role } from './lib/roles'
import { UnauthorizedState } from './components/UnauthorizedState'

interface RoleRouteProps {
  /** Roles permitted through. Admin always passes, per the shared rule. */
  allow: Role[]
}

/**
 * Route guard for pages restricted to particular roles. Anonymous visitors are sent to the
 * landing page; signed-in users lacking the role get an explicit unauthorized state rather than
 * a page that renders and then errors on interaction.
 */
export function RoleRoute({ allow }: RoleRouteProps) {
  const { data: session, isLoading } = useCurrentUser()

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <span className="text-muted-foreground">Loading…</span>
      </div>
    )
  }

  if (!session?.authenticated) {
    return <Navigate to="/" replace />
  }

  if (!hasAnyRole(session.user.roles, allow)) {
    return <UnauthorizedState />
  }

  return <Outlet />
}
