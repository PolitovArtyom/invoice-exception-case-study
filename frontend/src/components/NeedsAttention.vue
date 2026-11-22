<script setup lang="ts">
import { computed } from 'vue'
import type { InvoiceCheck, InvoiceLine } from '../api'
import { checkTitle, isLineCheck } from '../checkPresentation'
import { formatAmount, formatDecimal } from '../formatDecimal'
import CheckVerdict from './CheckVerdict.vue'

const props = defineProps<{
  checks: InvoiceCheck[]
  lines: InvoiceLine[]
  currency: string | null
}>()

const issues = computed(() =>
  props.checks.filter(
    (check) =>
      check.status === 'fail' ||
      (check.status === 'unknown' && !check.depends_on),
  ),
)
const waiting = computed(() => {
  const counts = new Map<string, number>()
  for (const check of props.checks) {
    if (check.status === 'unknown' && check.depends_on)
      counts.set(check.depends_on, (counts.get(check.depends_on) ?? 0) + 1)
  }
  return counts
})

function amount(value: unknown) {
  return typeof value === 'string' ? formatDecimal(value) : 'unknown'
}

function money(value: unknown) {
  return typeof value === 'string'
    ? formatAmount(value, props.currency)
    : 'unknown'
}

const arithmeticAmounts = [
  ['Net', 'declared_net_amount', 'calculated_net'],
  ['Line tax', 'declared_tax_amount', 'calculated_tax'],
  ['Subtotal', 'declared_subtotal', 'calculated_subtotal'],
  ['Tax total', 'declared_tax_total', 'calculated_tax_total'],
  ['Invoice total', 'declared_total', 'calculated_total'],
] as const

function decimalKey(value: string) {
  return value.replace(/(\.\d*?)0+$/, '$1').replace(/\.$/, '')
}

function describeArithmetic(check: InvoiceCheck) {
  return arithmeticAmounts
    .flatMap(([label, declared, calculated]) => {
      const invoiceAmount = check.actual?.[declared]?.value
      const calculatedAmount = check.expected?.[calculated]?.value
      if (
        typeof invoiceAmount !== 'string' ||
        typeof calculatedAmount !== 'string' ||
        decimalKey(invoiceAmount) === decimalKey(calculatedAmount)
      )
        return []

      return [
        `${label} is ${money(invoiceAmount)} on the invoice and ${money(calculatedAmount)} when calculated.`,
      ]
    })
    .join(' ')
}

function describe(check: InvoiceCheck) {
  const actual = check.actual
  const expected = check.expected
  if (check.status === 'unknown') {
    if (check.code === 'QTY_RECEIVED')
      return {
        detail: 'Goods receipt data is unavailable.',
        guidance:
          'Receiving can check whether the receipt records are available.',
      }
    if (check.code === 'BANK_ACCOUNT')
      return {
        detail: 'The invoice has no recorded IBAN.',
        guidance:
          'Accounts payable should request bank details through a known supplier contact.',
      }
    return { detail: 'Information is missing for this check.' }
  }

  switch (check.code) {
    case 'QTY_RECEIVED':
      return {
        detail: `Invoiced ${amount(actual?.quantity?.value)} ${actual?.unit?.value ?? ''}. Available to invoice from receipts is ${amount(expected?.available_to_invoice?.value)}.`,
        guidance:
          'Ask receiving whether the goods arrived. If a receipt is missing for delivered goods, ask receiving to post it. If receipts are already posted, ask accounts payable to reconcile prior invoices. For undelivered goods, request a corrected invoice.',
      }
    case 'QTY_ORDER_REMAINING':
      return {
        detail: `Invoiced ${amount(actual?.quantity?.value)} ${actual?.unit?.value ?? ''}. The remaining order quantity is ${amount(expected?.remaining_on_order?.value)}.`,
        guidance:
          'Ask procurement to check for an order amendment or request a corrected invoice.',
      }
    case 'UNIT_PRICE':
      return {
        detail: `Invoice unit price ${money(actual?.unit_net_price?.value)} and order unit price ${money(expected?.unit_net_price?.value)} differ by ${money(actual?.absolute_difference?.value)}. The tolerance is ${money(expected?.inclusive_tolerance?.value)}.`,
        guidance:
          'Ask procurement to update the order if the price was agreed. Otherwise, request a corrected invoice.',
      }
    case 'LINE_ARITHMETIC':
    case 'TOTALS_ARITHMETIC':
      return {
        detail:
          describeArithmetic(check) ||
          'The declared amounts differ from the calculation.',
        guidance: 'Accounts payable should compare the amounts with the PDF.',
      }
    case 'BANK_ACCOUNT':
      return {
        detail: `Invoice IBAN ${actual?.iban?.value ?? 'unknown'} is outside the supplier’s approved accounts.`,
        guidance:
          'Accounts payable should verify the account independently using supplier contact details already on file. Refer any confirmed change to the supplier directory owner.',
      }
    default:
      return { detail: 'Review the saved comparison.' }
  }
}
const entries = computed(() =>
  issues.value.map((check) => ({
    check,
    ...describe(check),
    line: isLineCheck(check)
      ? props.lines.find((line) => line.line_id === check.object_id)
      : undefined,
  })),
)
</script>

<template>
  <section
    v-if="entries.length || waiting.size"
    class="card attention"
    aria-labelledby="attention-heading"
  >
    <h2 id="attention-heading">Needs attention</h2>
    <ul class="attention-list">
      <li
        v-for="{ check, line, detail, guidance } in entries"
        :key="`${check.code}/${check.object_id}`"
      >
        <div class="check-heading">
          <strong>{{ checkTitle(check.code) }}</strong>
          <CheckVerdict :check="check" />
        </div>
        <p>
          <strong v-if="line"
            >Line {{ line.line_id }} · {{ line.description }}.
          </strong>
          {{ detail }}
        </p>
        <p v-if="guidance" class="muted">{{ guidance }}</p>
      </li>
    </ul>
    <p v-for="[code, count] in waiting" :key="code" class="muted">
      {{ count }} {{ count === 1 ? 'check is' : 'checks are' }} waiting for
      {{ checkTitle(code).toLowerCase() }}.
    </p>
  </section>
</template>
