import type { CaseStage } from './caseStage'

export interface CaseSummary {
  case_id: string
  origin: string
  number: string | null
  supplier: string | null
  declared_total: string | null
  currency: string | null
  decision: string | null
  stage: CaseStage
  reading_origin: string | null
}

export interface InvoiceFacts {
  number: string
  issue_date: string
  due_date: string | null
  currency: string | null
  purchase_order_reference: string | null
  supplier: { legal_name: string | null }
  buyer: { legal_name: string | null }
  declared_total: string
}

export interface InvoiceResult {
  decision: string
  matched_order_id: string | null
  checks: InvoiceCheck[]
  totals: {
    calculated_subtotal: string
    calculated_tax_total: string
    calculated_total: string
  }
}

export interface InvoiceCheck {
  code: string
  object_id: string
  status: string
  severity: string | null
  depends_on: string | null
  actual: Record<string, CheckOperand>
  expected: Record<string, CheckOperand>
}

export interface CheckOperand {
  value: unknown
  source: string
}

export interface CaseView {
  case: { case_id: string; origin: string }
  current_revision: number
  stage: CaseStage
  revision: {
    number: number
    context_version: string
    document: {
      reference: number
      source: { relative_path: string } | null
      facts: InvoiceFacts
      reading_origin: string
    } | null
    result: InvoiceResult | null
  }
}

export function listCases() {
  return read<CaseSummary[]>('/api/cases')
}

export function getCase(caseId: string) {
  return read<CaseView>(`/api/cases/${encodeURIComponent(caseId)}`)
}

export function documentPdf(caseId: string, reference: number) {
  return `/api/cases/${encodeURIComponent(caseId)}/documents/${reference}/pdf`
}

async function read<T>(path: string): Promise<T> {
  const response = await fetch(path)
  if (!response.ok) {
    throw new Error(
      response.status === 404
        ? 'Case not found.'
        : 'Unable to load the case data.',
    )
  }
  return response.json() as Promise<T>
}
