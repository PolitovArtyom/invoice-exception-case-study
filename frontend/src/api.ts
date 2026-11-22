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
  original_document_id: string | null
}

interface InvoiceParty {
  legal_name: string | null
  registration_id: string | null
  country: string | null
}

export interface InvoiceFacts {
  document_id: string
  type: string
  schema_version: string
  number: string
  issue_date: string
  due_date: string | null
  currency: string | null
  purchase_order_reference: string | null
  payment_terms: string | null
  supplier: InvoiceParty
  buyer: InvoiceParty
  bank: { iban: string | null; bic: string | null }
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
  sku: string | null
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

export type ComparedLine = Omit<InvoiceLine, 'line_id' | 'description'>

export interface ComparedFacts {
  issue_date: string
  due_date: string | null
  currency: string | null
  purchase_order_reference: string | null
  iban: string | null
  declared_subtotal: string
  declared_tax_total: string
  declared_total: string
  lines: ComparedLine[]
}

interface Source {
  relative_path: string
}

export interface CaseView {
  case: { case_id: string; origin: CaseOrigin }
  current_revision: number
  original_pdf_available: boolean
  invoice_pdf_available: boolean
  inputs_changed: boolean
  latest_duplicate_revision: number | null
  stage: CaseStage
  revision: {
    number: number
    context_version: string
    document: {
      reference: number
      source: Source | null
      facts: InvoiceFacts
      reading_origin: ReadingOrigin
    } | null
    result: InvoiceResult | null
    original_invoice: { document_id: string } | null
    duplicate_attempt: {
      reading: {
        facts: InvoiceFacts
        source: Source | null
        reading_origin: ReadingOrigin
      }
    } | null
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

export function revisionPdf(
  caseId: string,
  revision: number,
  kind: 'original' | 'duplicate',
) {
  return `/api/cases/${encodeURIComponent(caseId)}/revisions/${revision}/${kind}-pdf`
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
