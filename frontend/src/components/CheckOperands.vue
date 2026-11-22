<script setup lang="ts">
import type { CheckOperand } from '../api'
import SourceTag from './SourceTag.vue'

defineProps<{ operands: Record<string, CheckOperand> }>()

function display(value: unknown): string {
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
        <span class="mono">{{ display(operand.value) }}</span>
        <SourceTag :source="operand.source" />
      </dd>
    </div>
  </dl>
</template>
