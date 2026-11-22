<script setup lang="ts">
import type { CheckOperand } from '../api'
import { formatMoney } from '../money'
import SourceTag from './SourceTag.vue'

defineProps<{ operands: Record<string, CheckOperand> }>()

const monetaryOperands = new Set([
  'unit_net_price',
  'absolute_difference',
  'inclusive_tolerance',
  'declared_net_amount',
  'declared_tax_amount',
  'declared_subtotal',
  'declared_tax_total',
  'declared_total',
  'calculated_net',
  'calculated_tax',
  'calculated_subtotal',
  'calculated_tax_total',
  'calculated_total',
])

function display(name: string, value: unknown): string {
  if (typeof value === 'string' && monetaryOperands.has(name))
    return formatMoney(value)
  if (Array.isArray(value)) return value.length ? value.join(', ') : 'None'
  if (typeof value === 'object' && value !== null)
    return Object.values(value).join(' / ')
  return value == null ? '—' : String(value)
}
</script>

<template>
  <dl class="operands">
    <div v-for="(operand, name) in operands" :key="name">
      <dt>{{ name.replaceAll('_', ' ') }}</dt>
      <dd>
        <span class="mono">{{ display(name, operand.value) }}</span>
        <SourceTag :source="operand.source" />
      </dd>
    </div>
  </dl>
</template>
