import type { CaseStage, CaseTab } from './caseStage'

type CaseOrigin = 'assignment' | 'own'
export type IssueOwner = 'accounts_payable' | 'procurement' | 'receiving'
export type ReadingOrigin = 'fixture' | 'agent' | 'runner'
export type MatchSource =
  'deterministic' | 'agent_proposal' | 'human_confirmation'

export interface CaseSummary {
  case_id: string
  origin: CaseOrigin
  number: string | null
  supplier: string | null
  issue_date: string | null
  declared_total: string | null
  currency: string | null
  decision: string | null
  stage: CaseStage
  tab: CaseTab
  original_document_id: string | null
  main_issue: string | null
  main_issue_owner: IssueOwner | null
  assessed_revision: number | null
  context_version: string | null
}

interface CaseList {
  counts: Record<CaseTab, number>
  cases: CaseSummary[]
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
  match_source: MatchSource | null
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
  input_revision: number
  context_version: string
  decision: string
  matched_order_id: string | null
  order_match_source: MatchSource | null
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

export interface CaseIssue {
  check: InvoiceCheck
  owner: IssueOwner
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
  source_pdfs: { index: number; document_id: string }[]
  inputs_changed: boolean
  latest_duplicate_revision: number | null
  superseded: boolean
  show_duplicate_reading: boolean
  effective_proposal_id: string | null
  proposals: ProposalView[]
  open_supplier_orders: PurchaseOrder[]
  company_data: {
    goods_receipts: ReceiptDirectory
  }
  issues: CaseIssue[]
  stage: CaseStage
  revision: RevisionView
}

export interface RevisionView {
  number: number
  context_version: string
  policy_version: string
  assessment_id: string | null
  action: {
    kind:
      | 'load_case'
      | 'register_invoice'
      | 'submit_case_inputs'
      | 'propose_match'
      | 'confirm_proposal'
      | 'replace_company_data'
      | 'recheck_invoice'
    actor: 'trusted_loader' | 'agent' | 'human' | 'runner'
    recorded_at: string
    proposal_ids: string[] | null
    confirmations: { proposal_id: string; confirmed_by: string }[] | null
    company_data_submitted: boolean
    document_submitted: boolean
  }
  document: {
    reference: number
    source: Source | null
    facts: InvoiceFacts
    reading_origin: ReadingOrigin
  } | null
  result: InvoiceResult | null
  original_invoice: { document_id: string } | null
  is_duplicate_attempt: boolean
  duplicate_attempt: {
    reading: {
      facts: InvoiceFacts
      source: Source | null
      reading_origin: ReadingOrigin
    }
  } | null
}

export interface ReceiptDirectory {
  availability: 'available' | 'unavailable'
  receipts: GoodsReceipt[]
}

interface GoodsReceipt {
  receipt_id: string
  order_id: string
  order_line_id: string
  accepted_quantity: string
  unit: string
  accepted_at: string
  status: 'accepted'
}

export interface PurchaseOrder {
  order_id: string
  reference: string
  supplier_id: string
  currency: string
  lines: {
    line_id: string
    sku: string | null
    description: string
    quantity: string
    unit: string
    unit_net_price: string
  }[]
}

export interface ProposalLineMatch {
  document_line_id: string
  order_line_id: string
}

export interface ProposalView {
  registration: {
    proposal: {
      proposal_id: string
      document_id: string
      order_id: string
      line_matches: ProposalLineMatch[]
    }
    confirmation: { confirmed_by: string } | null
  }
  order: PurchaseOrder | null
  supplier_name: string | null
  applicable: boolean
  can_preview: boolean
  can_confirm: boolean
}

interface ConfirmationPreview {
  expected_revision: number
  proposal_id: string
  result: InvoiceResult
}

export class ActionError extends Error {
  constructor(
    message: string,
    readonly field?: string,
  ) {
    super(message)
  }
}

export function listCases() {
  return request<CaseList>('/api/cases')
}

function casePath(caseId: string) {
  return `/api/cases/${encodeURIComponent(caseId)}`
}

export function getCase(caseId: string, revision?: string) {
  const path = casePath(caseId)
  return request<CaseView>(
    revision ? `${path}/revisions/${encodeURIComponent(revision)}` : path,
  )
}

export function recheckInvoice(caseId: string) {
  return request<{ inputs_unchanged: boolean; result: InvoiceResult }>(
    `${casePath(caseId)}/rechecks`,
    { method: 'POST' },
  )
}

export function getHistory(caseId: string) {
  return request<RevisionView[]>(`${casePath(caseId)}/history`)
}

export function previewConfirmation(
  caseId: string,
  proposalId: string,
  signal: AbortSignal,
) {
  return request<ConfirmationPreview>(
    `${casePath(caseId)}/proposals/${encodeURIComponent(proposalId)}/confirmation-preview`,
    { signal },
  )
}

export async function confirmProposal(
  caseId: string,
  preview: ConfirmationPreview,
  name: string,
  reviewed: boolean,
) {
  await request(`${casePath(caseId)}/confirmations`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      proposal_id: preview.proposal_id,
      expected_revision: preview.expected_revision,
      confirmed_by: name,
      supporting_documents_reviewed: reviewed,
    }),
  })
}

export function sourcePdf(caseId: string, index: number) {
  return `/api/cases/${encodeURIComponent(caseId)}/sources/${index}/pdf`
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

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(path, options)
  if (!response.ok) {
    if (response.status === 400) {
      const body = (await response.json()) as {
        error?: { details?: { path: string; message: string }[] }
      }
      const detail = body.error?.details?.[0]
      if (detail) throw new ActionError(detail.message, detail.path)
    }
    throw new Error(
      response.status === 404
        ? 'The requested case, revision or proposal was not found.'
        : 'Unable to complete the request.',
    )
  }
  return response.json() as Promise<T>
}
