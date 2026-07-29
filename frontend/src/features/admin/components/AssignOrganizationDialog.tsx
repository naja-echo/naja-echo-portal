import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogHeader, DialogFooter, DialogTitle } from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { ApiError } from '@/lib/apiClient'
import { useOrganizations } from '../hooks/useOrganizations'
import { useAssignOrganizationForUser } from '../hooks/useAssignOrganizationForUser'

interface AssignOrganizationDialogProps {
  open: boolean
  userId: string
  currentOrganizationId: string | null
  onClose: () => void
}

/**
 * Radix Select treats the empty string as "no value", so an explicit unassign option needs a
 * sentinel of its own. Belonging to no organization is a real, valid state (FR-005) rather than
 * an absence of input, and the control has to be able to say so.
 */
const NO_ORGANIZATION = 'none'

function mapError(err: unknown): string {
  if (!(err instanceof ApiError)) return 'Something went wrong.'
  if (err.status === 404) return err.message ?? 'Member or organization not found.'
  if (err.status === 403) return 'You do not have permission to change organizations.'
  return err.message ?? 'Something went wrong.'
}

export function AssignOrganizationDialog({
  open,
  userId,
  currentOrganizationId,
  onClose,
}: AssignOrganizationDialogProps) {
  const [selected, setSelected] = useState<string>(currentOrganizationId ?? NO_ORGANIZATION)
  const [apiError, setApiError] = useState<string | null>(null)

  const { data, isLoading, isError } = useOrganizations()
  const mutation = useAssignOrganizationForUser()

  const organizations = data?.organizations ?? []

  function handleOpenChange(isOpen: boolean) {
    if (!isOpen) {
      setSelected(currentOrganizationId ?? NO_ORGANIZATION)
      setApiError(null)
      onClose()
    }
  }

  async function handleSubmit() {
    setApiError(null)
    try {
      await mutation.mutateAsync({
        userId,
        organizationId: selected === NO_ORGANIZATION ? null : selected,
      })
      onClose()
    } catch (err) {
      setApiError(mapError(err))
    }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle>Assign Organization</DialogTitle>
        </DialogHeader>

        <div className="flex flex-col gap-3">
          {isLoading && <p className="text-muted-foreground text-sm">Loading organizations…</p>}
          {isError && <p className="text-destructive text-sm">Failed to load organizations.</p>}

          {!isLoading && !isError && (
            <Select value={selected} onValueChange={setSelected}>
              <SelectTrigger id="assign-organization" aria-label="Organization">
                <SelectValue placeholder="Select an organization" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NO_ORGANIZATION}>No organization</SelectItem>
                {organizations.map((o) => (
                  <SelectItem key={o.id} value={o.id}>
                    {o.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}

          {apiError && <p className="text-sm text-destructive">{apiError}</p>}
        </div>

        <DialogFooter className="mt-4">
          <Button variant="outline" onClick={() => handleOpenChange(false)}>
            Cancel
          </Button>
          {/* Disabled on isError too: with the list unavailable the Select never renders, so
              submitting would send whatever the initial selection was and close the dialog as
              though the admin's intended change had been saved. */}
          <Button
            onClick={() => void handleSubmit()}
            disabled={mutation.isPending || isLoading || isError}
          >
            {mutation.isPending ? 'Saving…' : 'Save'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
