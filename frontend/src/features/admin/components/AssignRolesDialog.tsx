import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogHeader, DialogFooter, DialogTitle } from '@/components/ui/dialog'
import { ApiError } from '@/lib/apiClient'
import { useAssignRolesForUser } from '../hooks/useAssignRolesForUser'
import { availableRoles, getRoleDisplayName } from '../lib/roleDisplayNames'

interface AssignRolesDialogProps {
  open: boolean
  userId: string
  currentRoles: string[]
  onClose: () => void
}

function mapError(err: unknown): string {
  if (!(err instanceof ApiError)) return 'Something went wrong.'
  if (err.status === 404) return 'User not found.'
  if (err.status === 400) return err.message ?? 'Invalid role.'
  return err.message ?? 'Something went wrong.'
}

export function AssignRolesDialog({ open, userId, currentRoles, onClose }: AssignRolesDialogProps) {
  const [selected, setSelected] = useState<Set<string>>(() => new Set(currentRoles))
  const [apiError, setApiError] = useState<string | null>(null)
  const mutation = useAssignRolesForUser()

  function reset() {
    setSelected(new Set(currentRoles))
    setApiError(null)
  }

  function handleOpenChange(isOpen: boolean) {
    if (!isOpen) {
      reset()
      onClose()
    }
  }

  function toggleRole(role: string) {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(role)) {
        next.delete(role)
      } else {
        next.add(role)
      }
      return next
    })
  }

  async function handleSubmit() {
    setApiError(null)
    try {
      await mutation.mutateAsync({ userId, roles: [...selected] })
      onClose()
    } catch (err) {
      setApiError(mapError(err))
    }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle>Assign Roles</DialogTitle>
        </DialogHeader>

        <div className="flex flex-col gap-3">
          {availableRoles.map((role) => (
            <label key={role} className="flex items-center gap-2 text-sm cursor-pointer">
              <Checkbox
                checked={selected.has(role)}
                onCheckedChange={() => toggleRole(role)}
              />
              {getRoleDisplayName(role)}
            </label>
          ))}

          {apiError && <p className="text-sm text-destructive">{apiError}</p>}
        </div>

        <DialogFooter className="mt-4">
          <Button variant="outline" onClick={() => handleOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={() => void handleSubmit()} disabled={mutation.isPending}>
            {mutation.isPending ? 'Saving…' : 'Save'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
