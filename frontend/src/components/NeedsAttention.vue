<script setup lang="ts">
import { computed, ref } from 'vue'
import type {
  CaseIssue,
  CorrectableFact,
  InvoiceCheck,
  InvoiceFacts,
} from '../api'
import { checkTitle, isLineCheck, issueOwnerLabel } from '../checkPresentation'
import { formatAmount, formatDecimal } from '../formatDecimal'
import CheckVerdict from './CheckVerdict.vue'
import InvoiceFactSource from './InvoiceFactSource.vue'

const props = defineProps<{
  checks: InvoiceCheck[]
  issues: CaseIssue[]
  facts: InvoiceFacts
  caseId: string
  revision: number
  correctableFacts: CorrectableFact[]
}>()
const emit = defineEmits<{ refresh: [message: string] }>()
const editing = ref('')
const editable = computed(
  () => new Map(props.correctableFacts.map((field) => [field.path, field])),
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
    ? formatAmount(value, props.facts.currency)
    : 'unknown'
}

function describeArithmetic(facts: CaseIssue['facts']) {
  return facts
    .filter((fact) => fact.calculated_value !== null)
    .map(
      ({ label, value, calculated_value }) =>
        `${label} was read as ${money(value)} and calculated as ${money(calculated_value)}.`,
    )
    .join(' ')
}

function describe({ check, facts }: CaseIssue) {
  const actual = check.actual
  const expected = check.expected
  if (check.status === 'unknown') {
    if (check.code === 'ORDER_MATCH')
      return {
        detail: 'The invoice could not be matched to a single purchase order.',
        guidance: 'Check the PO reference and supporting delivery documents.',
      }
    if (check.code === 'LINE_MATCH')
      return {
        detail: 'This invoice line has no unique order line match.',
        guidance: 'Identify the matching order line.',
      }
    if (check.code === 'QTY_RECEIVED')
      return {
        detail: 'Goods receipt data is unavailable.',
        guidance: 'Check whether the receipt records are available.',
      }
    if (check.code === 'BANK_ACCOUNT')
      return {
        detail: 'The invoice has no recorded IBAN.',
        guidance: 'Request bank details through a known supplier contact.',
      }
    return { detail: 'Information is missing for this check.' }
  }

  switch (check.code) {
    case 'ORDER_MATCH':
      return {
        detail: 'The selected order comes from an agent proposal.',
        guidance: 'Verify the order against the delivery documents.',
      }
    case 'LINE_MATCH':
      return actual?.unit?.value !== expected?.unit?.value
        ? {
            detail: `The invoice unit is ${actual?.unit?.value} and the order unit is ${expected?.unit?.value}.`,
            guidance: 'Check the units on the order and invoice.',
          }
        : {
            detail: 'The selected line comes from an agent proposal.',
            guidance: 'Verify the line against the delivery documents.',
          }
    case 'QTY_RECEIVED':
      return {
        detail: `Invoiced ${amount(actual?.quantity?.value)} ${actual?.unit?.value ?? ''}. Available to invoice from receipts is ${amount(expected?.available_to_invoice?.value)}.`,
        guidance:
          'Check whether the goods arrived. If a receipt is missing for delivered goods, ask receiving to post it. If receipts are already posted, ask accounts payable to reconcile prior invoices. For undelivered goods, request a corrected invoice.',
      }
    case 'QTY_ORDER_REMAINING':
      return {
        detail: `Invoiced ${amount(actual?.quantity?.value)} ${actual?.unit?.value ?? ''}. The remaining order quantity is ${amount(expected?.remaining_on_order?.value)}.`,
        guidance:
          'Check for an order amendment or request a corrected invoice.',
      }
    case 'UNIT_PRICE':
      return {
        detail: `Read unit price ${money(actual?.unit_net_price?.value)} and order unit price ${money(expected?.unit_net_price?.value)} differ by ${money(actual?.absolute_difference?.value)}. The tolerance is ${money(expected?.inclusive_tolerance?.value)}.`,
        guidance:
          'Update the order if the price was agreed. Otherwise, request a corrected invoice.',
      }
    case 'LINE_ARITHMETIC':
    case 'TOTALS_ARITHMETIC':
      return {
        detail:
          describeArithmetic(facts) ||
          'The declared amounts differ from the calculation.',
        guidance: 'Compare the amounts with the PDF.',
      }
    case 'BANK_ACCOUNT':
      return {
        detail: `Invoice IBAN ${actual?.iban?.value ?? 'unknown'} is outside the supplier’s approved accounts.`,
        guidance:
          'Verify the account independently using supplier contact details already on file. Refer any confirmed change to the supplier directory owner.',
      }
    default:
      return { detail: 'Review the saved comparison.' }
  }
}
const entries = computed(() =>
  props.issues.map((issue) => {
    const { check, owner, facts } = issue
    const lineIndex = isLineCheck(check)
      ? props.facts.lines.findIndex((line) => line.line_id === check.object_id)
      : -1
    return {
      key: `${check.code}/${check.object_id}`,
      check,
      owner,
      ...describe(issue),
      line: props.facts.lines[lineIndex],
      sources: facts.map((fact) => ({
        ...fact,
        field: editable.value.get(fact.path),
      })),
    }
  }),
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
        v-for="{
          key,
          check,
          owner,
          line,
          detail,
          guidance,
          sources,
        } in entries"
        :key="key"
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
        <div
          v-for="{ path, label, value, field, quotes } in sources"
          :key="path"
        >
          <p>
            {{ label }} read as <span class="mono">{{ value }}</span>
          </p>
          <InvoiceFactSource
            :case-id="caseId"
            :revision="revision"
            :label="label"
            :value="value"
            :field="field"
            :quotes="quotes"
            :editing="editing === key + path"
            @edit="editing = key + path"
            @cancel="editing = ''"
            @refresh="emit('refresh', $event)"
          />
        </div>
        <p class="muted">
          <strong>{{ issueOwnerLabel(owner) }}</strong>
          <template v-if="guidance"> · {{ guidance }}</template>
        </p>
      </li>
    </ul>
    <p v-for="[code, count] in waiting" :key="code" class="muted">
      {{ count }} {{ count === 1 ? 'check is' : 'checks are' }} waiting for
      {{ checkTitle(code).toLowerCase() }}.
    </p>
  </section>
</template>
