import type {
  CheckOperand,
  InvoiceCheck,
  InvoiceFacts,
  InvoiceMissingField,
  IssueOwner,
} from './api'

const titles: Record<string, string> = {
  DOCUMENT_CONFLICT: 'Document conflict',
  SUPPLIER_IDENTITY: 'Supplier identity',
  BUYER_IDENTITY: 'Buyer identity',
  ORDER_MATCH: 'Purchase order match',
  CURRENCY_MATCH: 'Currency match',
  LINE_MATCH: 'Invoice line match',
  QTY_RECEIVED: 'Received quantity',
  QTY_ORDER_REMAINING: 'Remaining order quantity',
  UNIT_PRICE: 'Unit price',
  LINE_ARITHMETIC: 'Line arithmetic',
  TOTALS_ARITHMETIC: 'Totals arithmetic',
  BANK_ACCOUNT: 'Bank account',
}

export function checkTitle(code: string) {
  return titles[code] ?? code
}

const issueOwnerLabels: Record<IssueOwner, string> = {
  accounts_payable: 'Accounts payable',
  procurement: 'Procurement',
  receiving: 'Receiving',
}

export function issueOwnerLabel(owner: IssueOwner) {
  return issueOwnerLabels[owner]
}

export function verdictLabel(check: InvoiceCheck) {
  switch (check.status) {
    case 'pass':
      return 'Pass'
    case 'fail':
      return check.severity === 'blocked' ? 'Blocked' : 'Needs review'
    case 'unknown':
      return 'Unknown'
    case 'not_applicable':
      return 'Not applicable'
    default:
      return check.status
  }
}

const lineChecks = new Set([
  'LINE_MATCH',
  'QTY_RECEIVED',
  'QTY_ORDER_REMAINING',
  'UNIT_PRICE',
  'LINE_ARITHMETIC',
])

export function isLineCheck(check: InvoiceCheck) {
  return lineChecks.has(check.code)
}

export interface PresentedOperand extends CheckOperand {
  missingReason?: InvoiceMissingField['reason']
}

const invoiceFactParents: Record<string, string> = {
  BANK_ACCOUNT: '/bank',
  BUYER_IDENTITY: '/buyer',
  SUPPLIER_IDENTITY: '/supplier',
}

export function actualOperands(
  facts: InvoiceFacts,
  check: InvoiceCheck | undefined,
): Record<string, PresentedOperand> {
  if (!check?.actual) return {}

  const factParent = isLineCheck(check)
    ? `/lines/${facts.lines.findIndex((line) => line.line_id === check.object_id)}`
    : (invoiceFactParents[check.code] ?? '')
  return Object.fromEntries(
    Object.entries(check.actual)
      .filter(([name]) => name !== 'mismatches')
      .map(([name, operand]) => {
        const missingReason =
          operand.source === 'invoice' && operand.value === null
            ? facts.missing_fields.find(
                (field) => field.field_path === `${factParent}/${name}`,
              )?.reason
            : undefined
        return [name, missingReason ? { ...operand, missingReason } : operand]
      }),
  )
}
