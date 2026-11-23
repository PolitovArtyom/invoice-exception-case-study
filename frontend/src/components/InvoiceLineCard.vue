<script setup lang="ts">
import { computed } from 'vue'
import type {
  CheckOperand,
  InvoiceCheck,
  InvoiceFacts,
  InvoiceLine,
  InvoiceLineResult,
} from '../api'
import { actualOperands, checkTitle } from '../checkPresentation'
import { formatDecimal } from '../formatDecimal'
import CheckOperands from './CheckOperands.vue'
import CheckVerdict from './CheckVerdict.vue'
import SourceTag from './SourceTag.vue'

const props = defineProps<{
  line: InvoiceLine
  assessment: InvoiceLineResult
  checks: InvoiceCheck[]
  facts: InvoiceFacts
  confirmedBy?: string
}>()
const match = computed(() => find('LINE_MATCH'))
const price = computed(() => find('UNIT_PRICE'))
const received = computed(() => find('QTY_RECEIVED'))
const remaining = computed(() => find('QTY_ORDER_REMAINING'))
const quantityChecks = computed(() =>
  [received.value, remaining.value].filter((check) => check !== undefined),
)
const arithmetic = computed(() => find('LINE_ARITHMETIC'))
const arithmeticActual = computed(() =>
  actualOperands(props.facts, arithmetic.value),
)
const orderUnit = computed(() => match.value?.expected?.unit?.value)
const matchProvenance = computed(() => {
  if (props.assessment.match_source === 'agent_proposal')
    return 'Proposed by the agent'
  if (props.assessment.match_source === 'human_confirmation')
    return props.confirmedBy
      ? `Confirmed by ${props.confirmedBy}`
      : 'Human confirmation'

  const reason = match.value?.actual?.match_reason?.value
  if (reason === 'sku') return 'Same SKU'
  if (reason === 'explicit_alias') return 'Order alias'
  if (reason === 'description') return 'Same description'
  return 'Unresolved'
})
const allPassed = computed(() =>
  props.checks.every((check) => check.status === 'pass'),
)

function quantityWithUnit(operand: CheckOperand | undefined, unit: unknown) {
  if (typeof operand?.value !== 'string' || typeof unit !== 'string')
    return operand
  return { ...operand, value: `${formatDecimal(operand.value)} ${unit}` }
}

function assessmentQuantity(value: string | null, source: string) {
  return value === null ? undefined : { value, source }
}

function find(code: string) {
  return props.checks.find((check) => check.code === code)
}
</script>

<template>
  <details class="card line-card" :open="!allPassed">
    <summary class="assessment-summary">
      <span class="line-name">
        <span class="mono">{{ line.line_id }}</span>
        <strong>{{ line.description }}</strong>
      </span>
      <span class="line-amount">
        <SourceTag
          v-if="arithmeticActual.quantity"
          :source="arithmeticActual.quantity.source"
        />
        {{ formatDecimal(line.quantity) }} {{ line.unit }} ×
        {{ formatDecimal(line.unit_net_price) }} {{ facts.currency }} · Declared
        net
        <strong
          >{{ formatDecimal(line.declared_net_amount) }}
          {{ facts.currency }}</strong
        >
      </span>
      <span v-if="allPassed" class="check-verdict verdict-pass"
        >All checks passed</span
      >
      <span v-else class="check-verdict verdict-unknown">Needs attention</span>
    </summary>

    <div class="line-match">
      <div class="check-heading">
        <strong>Matched order line</strong>
        <span class="mono">{{ assessment.order_line_id ?? 'Unknown' }}</span>
        <SourceTag
          v-if="match?.expected?.order_line_id"
          :source="match.expected.order_line_id.source"
        />
        <CheckVerdict v-if="match" :check="match" />
      </div>
      <p class="muted">{{ matchProvenance }}</p>
      <p class="muted">
        Invoice {{ line.line_id }} · {{ line.description
        }}<template v-if="line.sku"> · SKU {{ line.sku }}</template>
      </p>
      <p v-if="match?.expected?.description" class="muted">
        Order {{ assessment.order_line_id }} ·
        {{ match.expected.description.value }}
        <template v-if="match.expected.sku?.value">
          · SKU {{ match.expected.sku.value }}</template
        >
      </p>
    </div>

    <div class="calculation-grid">
      <section class="calculation">
        <h3>Unit price</h3>
        <CheckOperands
          :operands="{
            'Invoice unit price': price?.actual?.unit_net_price,
            'Order unit price': price?.expected?.unit_net_price,
            'Absolute difference': price?.actual?.absolute_difference,
            'Inclusive tolerance': price?.expected?.inclusive_tolerance,
          }"
        />
        <div v-if="price" class="calculation-verdict">
          <CheckVerdict :check="price" />
        </div>
      </section>

      <section class="calculation">
        <h3>Quantity</h3>
        <CheckOperands
          :operands="{
            Invoiced: quantityWithUnit(
              received?.actual?.quantity ??
                remaining?.actual?.quantity ??
                assessmentQuantity(line.quantity, 'invoice'),
              line.unit,
            ),
            Ordered: quantityWithUnit(
              remaining?.expected?.ordered ??
                assessmentQuantity(assessment.ordered, 'order'),
              orderUnit,
            ),
            Received: quantityWithUnit(
              received?.expected?.received ??
                assessmentQuantity(assessment.received, 'receipts'),
              orderUnit,
            ),
            'Previously invoiced': quantityWithUnit(
              remaining?.expected?.previously_invoiced ??
                received?.expected?.previously_invoiced ??
                assessmentQuantity(assessment.previously_invoiced, 'history'),
              orderUnit,
            ),
            'Available to invoice': quantityWithUnit(
              received?.expected?.available_to_invoice ??
                assessmentQuantity(
                  assessment.available_to_invoice,
                  'calculated',
                ),
              orderUnit,
            ),
            'Remaining on order': quantityWithUnit(
              remaining?.expected?.remaining_on_order ??
                assessmentQuantity(assessment.remaining_on_order, 'calculated'),
              orderUnit,
            ),
          }"
        />
        <p class="formula">
          Available to invoice = Received − Previously invoiced
        </p>
        <p class="formula">
          Remaining on order = Ordered − Previously invoiced
        </p>
        <div
          v-for="check in quantityChecks"
          :key="check.code"
          class="calculation-verdict"
        >
          {{ checkTitle(check.code) }}
          <CheckVerdict :check="check" />
        </div>
      </section>

      <section class="calculation">
        <h3>Line arithmetic</h3>
        <CheckOperands
          :operands="{
            'Declared net': arithmeticActual.declared_net_amount,
            'Calculated net': arithmetic?.expected?.calculated_net,
            'Tax rate (fraction)': arithmeticActual.tax_rate,
            'Declared line tax': arithmeticActual.declared_tax_amount,
            'Calculated line tax': arithmetic?.expected?.calculated_tax,
          }"
        />
        <p class="formula">
          Net = quantity × invoice unit price, rounded to cents
        </p>
        <p class="formula">
          Tax = rounded net × invoice tax rate, rounded to cents
        </p>
        <div v-if="arithmetic" class="calculation-verdict">
          <CheckVerdict :check="arithmetic" />
        </div>
      </section>
    </div>
  </details>
</template>
