<script setup lang="ts">
import Tag from 'primevue/tag'

defineProps<{ decision: string | null }>()

const decisions: Record<string, string> = {
  clear: 'Clear',
  needs_review: 'Needs review',
  needs_information: 'Needs information',
  blocked: 'Blocked',
}
</script>

<template>
  <Tag
    v-if="decision"
    rounded
    :severity="decision === 'clear' ? 'success' : 'warn'"
  >
    <span class="decision-dot" aria-hidden="true"></span
    >{{ decisions[decision] ?? decision }}
  </Tag>
  <Tag v-else rounded severity="secondary" value="Awaiting invoice reading" />
</template>
