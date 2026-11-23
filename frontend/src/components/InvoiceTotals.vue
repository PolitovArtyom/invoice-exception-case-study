<script setup lang="ts">
import { computed } from 'vue'
import type { InvoiceCheck } from '../api'
import CheckVerdict from './CheckVerdict.vue'
import SourceTag from './SourceTag.vue'
import { formatAmount } from '../formatDecimal'

const props = defineProps<{
  currency: string | null
  check: InvoiceCheck
}>()
const amounts = computed(() => [
  {
    label: 'Net subtotal',
    declared: props.check.actual?.declared_subtotal,
    calculated: props.check.expected?.calculated_subtotal,
  },
  {
    label: 'Tax total',
    declared: props.check.actual?.declared_tax_total,
    calculated: props.check.expected?.calculated_tax_total,
  },
  {
    label: 'Invoice total',
    declared: props.check.actual?.declared_total,
    calculated: props.check.expected?.calculated_total,
  },
])
</script>

<template>
  <details class="card" :open="check.status !== 'pass'">
    <summary class="assessment-summary">
      <strong>Invoice totals</strong>
      <CheckVerdict :check="check" />
    </summary>
    <div class="table-scroll">
      <table class="totals-comparison">
        <thead>
          <tr>
            <th scope="col">Amount</th>
            <th scope="col">Declared</th>
            <th scope="col">Calculated</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="{ label, declared, calculated } in amounts" :key="label">
            <th scope="row">{{ label }}</th>
            <td class="number">
              {{ formatAmount(declared?.value, currency) }}
              <SourceTag v-if="declared" :source="declared.source" />
            </td>
            <td class="number">
              {{ formatAmount(calculated?.value, currency) }}
              <SourceTag v-if="calculated" :source="calculated.source" />
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <p class="formula">
      Subtotal sums the rounded line nets. Tax total sums the rounded line
      taxes. The calculated invoice total adds those two sums.
    </p>
  </details>
</template>
