import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { AdminUser } from '../schemas/userSchemas'
import { getRoleDisplayName } from '../lib/roleDisplayNames'

interface UsersTableProps {
  users: AdminUser[]
  onAddCharacter: (userId: string) => void
  onAssignRoles: (userId: string, currentRoles: string[]) => void
  onAssignOrganization: (userId: string, currentOrganizationId: string | null) => void
}

export function UsersTable({
  users,
  onAddCharacter,
  onAssignRoles,
  onAssignOrganization,
}: UsersTableProps) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Auth Name</TableHead>
          <TableHead>Organization</TableHead>
          <TableHead>Roles</TableHead>
          <TableHead>Characters</TableHead>
          <TableHead className="w-96"><span className="sr-only">Actions</span></TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {users.map((user) => (
          <TableRow key={user.id}>
            <TableCell className="font-medium">{user.authName}</TableCell>
            <TableCell>
              {user.organization
                ? user.organization.name
                : <span className="text-muted-foreground">—</span>}
            </TableCell>
            <TableCell>
              {user.roles.length > 0
                ? user.roles.map(getRoleDisplayName).join(', ')
                : <span className="text-muted-foreground">—</span>}
            </TableCell>
            <TableCell>
              {user.characters.length > 0
                ? user.characters.map((c) => `${c.name} (${c.handle})`).join(', ')
                : <span className="text-muted-foreground">—</span>}
            </TableCell>
            <TableCell>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => onAssignRoles(user.id, user.roles)}
                >
                  Assign Roles
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => onAssignOrganization(user.id, user.organization?.id ?? null)}
                >
                  Assign Organization
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => onAddCharacter(user.id)}
                >
                  Add Character
                </Button>
              </div>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
