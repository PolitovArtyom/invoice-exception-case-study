<script setup lang="ts">
import type { PresentedOperand } from '../checkPresentation'
import { formatDecimal } from '../formatDecimal'
import SourceTag from './SourceTag.vue'

defineProps<{ operands: Record<string, PresentedOperand | undefined> }>()

function display(operand: PresentedOperand | undefined): string {
  const value = operand?.value
  if (value === null && operand?.missingReason)
    return operand.missingReason === 'illegible'
      ? 'Not on invoice (illegible)'
      : 'Not on invoice'

  if (typeof value === 'string' && /^-?\d+(?:\.\d+)?$/.test(value))
    return formatDecimal(value)
  if (Array.isArray(value)) return value.length ? value.join(', ') : 'None'
  if (typeof value === 'object' && value !== null)
    return Object.values(value).join(' / ')
  return value == null ? 'Unknown' : String(value)
}
</script>

<template>
  <dl class="operands">
    <div v-for="(operand, name) in operands" :key="name">
      <dt>{{ name.replaceAll('_', ' ') }}</dt>
      <dd>
        <span class="mono">{{ display(operand) }}</span>
        <SourceTag v-if="operand" :source="operand.source" />
      </dd>
    </div>
  </dl>
</template>
