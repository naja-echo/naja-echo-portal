interface ClaimPriorityBadgeProps {
  value: number
}

export function ClaimPriorityBadge({ value }: ClaimPriorityBadgeProps) {
  return (
    <span className="inline-flex items-center rounded-md border px-2.5 py-0.5 text-sm font-semibold font-mono">
      {value.toFixed(2)}
    </span>
  )
}
