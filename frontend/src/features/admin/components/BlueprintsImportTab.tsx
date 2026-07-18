import { useRef, useState } from 'react'
import { Upload } from 'lucide-react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { useImportBlueprints } from '../hooks/useImportBlueprints'
import { useBlueprints } from '../hooks/useBlueprints'
import { blueprintDatasetSchema, type ImportBlueprintsResponse } from '../schemas/blueprintSchemas'
import { BlueprintImportSummary } from './BlueprintImportSummary'
import { BlueprintsList } from './BlueprintsList'

const MAX_FILE_SIZE = 50 * 1024 * 1024 // 50 MB

export function BlueprintsImportTab() {
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<ImportBlueprintsResponse | null>(null)
  const fileRef = useRef<HTMLInputElement>(null)

  const { mutate: importBlueprints, isPending } = useImportBlueprints()
  const { data, isLoading } = useBlueprints()

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setError(null)
    setResult(null)

    if (file.size > MAX_FILE_SIZE) {
      setError('File is too large. Maximum size is 50 MB.')
      resetInput()
      return
    }

    let text: string
    let parsed: unknown
    try {
      text = await file.text()
      parsed = JSON.parse(text)
    } catch {
      setError('Invalid JSON file. Please select a valid blueprint dataset export.')
      resetInput()
      return
    }

    if (!blueprintDatasetSchema.safeParse(parsed).success) {
      setError('Unexpected file format. Expected a dataset with version, meta, dismantle, properties, resources, items, and blueprints.')
      resetInput()
      return
    }

    // Send the original file text — it is already-serialized JSON, so re-stringifying the parsed
    // object would duplicate work and peak memory for large (up to 50 MB) uploads.
    importBlueprints(text, {
      onSuccess: (data) => setResult(data),
      onError: (err) => setError(err instanceof Error ? err.message : 'Import failed. Please try again.'),
      onSettled: resetInput,
    })
  }

  function resetInput() {
    if (fileRef.current) fileRef.current.value = ''
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h2 className="text-lg font-semibold">Blueprints</h2>
        <p className="text-sm text-muted-foreground">
          Upload a complete crafting blueprint dataset export (JSON).
        </p>
      </div>

      {error && (
        <Alert variant="destructive" role="alert">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <label className="flex cursor-pointer flex-col items-center gap-2 rounded-lg border-2 border-dashed border-border p-6 hover:bg-muted/30">
        <Upload className="h-6 w-6 text-muted-foreground" aria-hidden />
        <span className="text-sm font-medium">
          {isPending ? 'Importing…' : 'Click to select a dataset file'}
        </span>
        <span className="text-xs text-muted-foreground">JSON, max 50 MB</span>
        <input
          ref={fileRef}
          type="file"
          accept=".json,application/json"
          className="sr-only"
          disabled={isPending}
          onChange={handleFileChange}
          aria-label="Select blueprint dataset JSON file"
        />
      </label>

      {result && <BlueprintImportSummary result={result} />}

      <div>
        <h3 className="mb-2 text-sm font-semibold">Known blueprints</h3>
        {isLoading ? (
          <p className="text-sm text-muted-foreground">Loading…</p>
        ) : (
          <BlueprintsList blueprints={data?.blueprints ?? []} />
        )}
      </div>
    </div>
  )
}
