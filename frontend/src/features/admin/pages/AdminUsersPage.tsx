import { useState } from 'react'
import { useAdminUsers } from '../hooks/useAdminUsers'
import { UsersFilter } from '../components/UsersFilter'
import { UsersTable } from '../components/UsersTable'
import { AddCharacterDialog } from '../components/AddCharacterDialog'
import { AssignRolesDialog } from '../components/AssignRolesDialog'
import { AssignOrganizationDialog } from '../components/AssignOrganizationDialog'
import { getRoleDisplayName } from '../lib/roleDisplayNames'
import type { AdminUser } from '../schemas/userSchemas'

function matchesFilter(user: AdminUser, filter: string): boolean {
  const q = filter.toLowerCase()
  if (user.authName.toLowerCase().includes(q)) return true
  if (user.organization?.name.toLowerCase().includes(q)) return true
  if (user.roles.some((r) => getRoleDisplayName(r).toLowerCase().includes(q))) return true
  if (user.characters.some((c) =>
    c.name.toLowerCase().includes(q) || c.handle.toLowerCase().includes(q))) return true
  return false
}

export function AdminUsersPage() {
  const { data, isLoading, isError } = useAdminUsers()
  const [filter, setFilter] = useState('')
  const [dialogUserId, setDialogUserId] = useState<string | null>(null)
  const [assignRolesTarget, setAssignRolesTarget] = useState<{ userId: string; roles: string[] } | null>(null)
  const [assignOrganizationTarget, setAssignOrganizationTarget] =
    useState<{ userId: string; organizationId: string | null } | null>(null)

  const users = data?.users ?? []
  const filtered = filter ? users.filter((u) => matchesFilter(u, filter)) : users

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold">Members</h1>
        <p className="text-muted-foreground">All authenticated members with their roles and characters.</p>
      </div>

      <UsersFilter value={filter} onChange={setFilter} />

      {isLoading && <p className="text-muted-foreground">Loading…</p>}
      {isError && <p className="text-destructive">Failed to load members. Please try again.</p>}

      {!isLoading && !isError && (
        users.length === 0
          ? (
            <div className="flex flex-col items-center justify-center rounded-md border border-dashed py-12 text-center">
              <p className="text-muted-foreground">No members found.</p>
            </div>
          )
          : filtered.length === 0
            ? (
              <div className="flex flex-col items-center justify-center rounded-md border border-dashed py-12 text-center">
                <p className="text-muted-foreground">No members found matching your filter.</p>
              </div>
            )
            : (
              <UsersTable
                users={filtered}
                onAddCharacter={(userId) => setDialogUserId(userId)}
                onAssignRoles={(userId, roles) => setAssignRolesTarget({ userId, roles })}
                onAssignOrganization={(userId, organizationId) =>
                  setAssignOrganizationTarget({ userId, organizationId })}
              />
            )
      )}

      {dialogUserId && (
        <AddCharacterDialog
          open
          userId={dialogUserId}
          onClose={() => setDialogUserId(null)}
        />
      )}

      {assignRolesTarget && (
        <AssignRolesDialog
          open
          userId={assignRolesTarget.userId}
          currentRoles={assignRolesTarget.roles}
          onClose={() => setAssignRolesTarget(null)}
        />
      )}

      {assignOrganizationTarget && (
        <AssignOrganizationDialog
          open
          userId={assignOrganizationTarget.userId}
          currentOrganizationId={assignOrganizationTarget.organizationId}
          onClose={() => setAssignOrganizationTarget(null)}
        />
      )}
    </div>
  )
}
