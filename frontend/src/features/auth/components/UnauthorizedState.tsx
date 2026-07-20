import { ShieldOff } from 'lucide-react'
import { Link } from 'react-router-dom'
import { buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'

/**
 * Shown when a signed-in user reaches a page their roles do not permit. Rendering this beats a
 * silent redirect: the user learns the page exists and that access is the missing piece.
 */
export function UnauthorizedState() {
  return (
    <div className="flex flex-col items-center justify-center gap-4 p-12 text-center">
      <ShieldOff className="h-10 w-10 text-muted-foreground" aria-hidden />
      <div className="space-y-1">
        <h1 className="text-xl font-semibold">You don&apos;t have access to this page</h1>
        <p className="text-sm text-muted-foreground">
          This area is limited to specific roles. Contact an administrator if you need access.
        </p>
      </div>
      <Link to="/dashboard" className={cn(buttonVariants({ variant: 'outline', size: 'sm' }))}>
        Back to dashboard
      </Link>
    </div>
  )
}
