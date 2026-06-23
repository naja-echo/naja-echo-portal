import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Combobox } from '@/components/ui/combobox'
import { addLedgerEntryRequestSchema, type AddLedgerEntryRequest, type DistributionRow } from '../schemas/lootSchemas'
import { useAwardLootPoints } from '../hooks/useAwardLootPoints'

interface AwardLootDialogProps {
  open: boolean
  onClose: () => void
  members: DistributionRow[]
}

export function AwardLootDialog({ open, onClose, members }: AwardLootDialogProps) {
  const [memberId, setMemberId] = useState('')
  const mutation = useAwardLootPoints(memberId)
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<AddLedgerEntryRequest>({
    resolver: zodResolver(addLedgerEntryRequestSchema),
  })

  const memberOptions = members.map((m) => ({ value: m.memberId, label: m.displayName }))

  const close = () => {
    reset()
    setMemberId('')
    onClose()
  }

  const onSubmit = async (data: AddLedgerEntryRequest) => {
    if (!memberId) return
    await mutation.mutateAsync(data)
    close()
  }

  return (
    <Dialog open={open} onOpenChange={(o) => !o && close()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Award Loot Points</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="space-y-2">
            <label htmlFor="award-member" className="text-sm font-medium">Member</label>
            <Combobox
              options={memberOptions}
              value={memberId}
              onValueChange={setMemberId}
              placeholder="Select a member…"
              searchPlaceholder="Search members…"
              emptyText="No members found."
              className="w-full"
              aria-label="Member"
            />
          </div>
          <div className="space-y-2">
            <label htmlFor="award-amount" className="text-sm font-medium">Amount</label>
            <Input
              id="award-amount"
              type="number"
              {...register('amount', { valueAsNumber: true })}
              aria-describedby={errors.amount ? 'award-amount-error' : undefined}
            />
            {errors.amount && (
              <p id="award-amount-error" className="text-sm text-destructive">{errors.amount.message}</p>
            )}
          </div>
          <div className="space-y-2">
            <label htmlFor="award-reason" className="text-sm font-medium">Reason</label>
            <Input
              id="award-reason"
              {...register('reason')}
              aria-describedby={errors.reason ? 'award-reason-error' : undefined}
            />
            {errors.reason && (
              <p id="award-reason-error" className="text-sm text-destructive">{errors.reason.message}</p>
            )}
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={close}>Cancel</Button>
            <Button type="submit" disabled={!memberId || mutation.isPending}>
              {mutation.isPending ? 'Submitting...' : 'Award Points'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
