import type { CaseStage } from './caseStage'

type CaseOrigin = 'assignment' | 'own'
export type ReadingOrigin = 'fixture' | 'agent' | 'runner'

export interface CaseSummary {
  case_id: string
  origin: CaseOrigin
  number: string | null
  supplier: string | null
  declared_total: string | null
  currency: string | null
  decision: string | null
  stage: CaseStage
}

export interface InvoiceFacts {
  number: string
  issue_date: string
  due_date: string | null
  currency: string | null
  purchase_order_reference: string | null
  supplier: { legal_name: string | null }
  buyer: { legal_name: string | null }
  declared_subtotal: string
  declared_tax_total: string
  declared_total: string
  lines: InvoiceLine[]
  evidence: InvoiceEvidence[]
  missing_fields: InvoiceMissingField[]
}

export interface InvoiceLine {
  line_id: string
  description: string
  quantity: string
  unit: string
  unit_net_price: string
  tax_rate: string
  declared_net_amount: string
  declared_tax_amount: string | null
}

export interface InvoiceLineResult {
  document_line_id: string
  order_line_id: string | null
  calculated_net: string
  calculated_tax: string
  ordered: string | null
  received: string | null
  previously_invoiced: string | null
  available_to_invoice: string | null
  remaining_on_order: string | null
}

export interface InvoiceEvidence {
  field_path: string
  page: number
  excerpt: string
}

export interface InvoiceMissingField {
  field_path: string
  reason: 'not_present_in_document' | 'illegible'
}

export interface InvoiceResult {
  decision: string
  matched_order_id: string | null
  checks: InvoiceCheck[]
  lines: InvoiceLineResult[]
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
  actual: Record<string, CheckOperand> | null
  expected: Record<string, CheckOperand> | null
}

export interface CheckOperand {
  value: unknown
  source: string
}

export interface CaseView {
  case: { case_id: string; origin: CaseOrigin }
  current_revision: number
  stage: CaseStage
  revision: {
    number: number
    context_version: string
    document: {
      reference: number
      source: { relative_path: string } | null
      facts: InvoiceFacts
      reading_origin: ReadingOrigin
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
