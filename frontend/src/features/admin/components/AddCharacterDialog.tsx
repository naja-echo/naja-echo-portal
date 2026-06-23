import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogHeader, DialogFooter, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { ApiError } from '@/lib/apiClient'
import { useAddCharacterForUser } from '../hooks/useAddCharacterForUser'

interface AddCharacterDialogProps {
  open: boolean
  userId: string
  onClose: () => void
}

function mapError(err: unknown): string {
  if (!(err instanceof ApiError)) return 'Something went wrong.'

  if (err.status === 409) return 'This handle is already claimed by another member.'
  if (err.status === 502) return 'RSI could not be reached. Please try again later.'
  if (err.status === 422) return 'Character name could not be retrieved — the handle may be valid but the RSI page returned no name.'

  if (err.status === 404) {
    const msg = (err.message ?? '').toLowerCase()
    if (msg.includes('rsi handle not found')) {
      return 'RSI handle not found. Please check the handle and try again.'
    }
    return 'User not found.'
  }

  return err.message ?? 'Something went wrong.'
}

export function AddCharacterDialog({ open, userId, onClose }: AddCharacterDialogProps) {
  const [handle, setHandle] = useState('')
  const [fieldError, setFieldError] = useState<string | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const mutation = useAddCharacterForUser()

  function reset() {
    setHandle('')
    setFieldError(null)
    setApiError(null)
  }

  function handleOpenChange(isOpen: boolean) {
    if (!isOpen) {
      reset()
      onClose()
    }
  }

  async function handleSubmit() {
    if (!handle.trim()) {
      setFieldError('Handle is required.')
      return
    }
    setFieldError(null)
    setApiError(null)

    try {
      await mutation.mutateAsync({ userId, handle: handle.trim() })
      reset()
      onClose()
    } catch (err) {
      setApiError(mapError(err))
    }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle>Add Character</DialogTitle>
        </DialogHeader>

        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-1">
            <label htmlFor="add-char-handle" className="text-sm font-medium">
              RSI Handle
            </label>
            <Input
              id="add-char-handle"
              placeholder="RSI handle"
              value={handle}
              onChange={(e) => setHandle(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter') void handleSubmit() }}
            />
            {fieldError && <p className="text-sm text-destructive">{fieldError}</p>}
          </div>

          {apiError && <p className="text-sm text-destructive">{apiError}</p>}
        </div>

        <DialogFooter className="mt-4">
          <Button variant="outline" onClick={() => handleOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={() => void handleSubmit()} disabled={mutation.isPending}>
            {mutation.isPending ? 'Adding…' : 'Add'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
